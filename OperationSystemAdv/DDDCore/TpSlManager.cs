using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;
using DivergentStrV0_1.OperationSystemAdv.DDDCore;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public enum OrderTypeSubcomment
    {
        Entry,
        StopLoss,
        TakeProfit
    }

    /// <summary>
    /// Signleton class to manage the global instance of TpSlManager.
    /// </summary>
    public sealed class GlobalTpSlManager
    {
        private static readonly Lazy<TpSlManager> lazyInstance = new(() => new TpSlManager());

        public static TpSlManager Instance => lazyInstance.Value;

        // Prevent instantiation
        private GlobalTpSlManager() { }
    }

    public sealed class TpSlManagerFactory<T,I> 
        where T : ITpSlItems 
        where I : IPositionManager<T>
    {
        Type typeParameterType = typeof(T);
        Type typeParameterType2 = typeof(I);
        public static I Instance => lazyInstance.Value;

        private static Lazy<I> lazyInstance;
        public static I CreateInstance()
        {
            if (lazyInstance == null)
            {
                lazyInstance = new(() => (I)Activator.CreateInstance(typeof(I))!);
            }
            return Instance;
        }
    }


    public class TpSlManager : PositionManagerBase<SlTpItems>, IDisposable
    {
        #region Properties
        public override int TradeCount { get { return this._tradeCount; } }
        private int _tradeCount = 0;
        public override double ExposedAmmount => throw new NotImplementedException();


        public override event EventHandler QuitAll;
        #endregion

        public TpSlManager()
        {
            Core.Instance.OrdersHistoryAdded += this.Instance_OrderAdded;
            Core.Instance.TradeAdded += this.Instance_TradeAdded;
        }


        #region 🐞 BUG [NOT IMPLEMENTED YET]
        //public void RevertAll()
        //{
        //    foreach (var item in Items)
        //    {

        //        item.CloseAll();
        //    }
        //}
        #endregion


        #region QTEvents
        private void Instance_TradeAdded(Trade trade)
        {
            var comment = this.GetSplittedComment(trade.Comment);


            if (comment != null && comment is KeyValuePair<string, OrderTypeSubcomment> parsedComment)
            {
                var match = this.MatchItems(parsedComment.Key);

                match?.RegisterTrade(trade);
                //TODO: [FUTURE] Verifica Secondaria Sulla Direzione

                if (match != null)
                {
                    this._tradeCount++;
                    if (match.Status == PositionManagerStatus.Closed || match.Status == PositionManagerStatus.Aborted)
                    {
                        this.CloseItem(match);
                    }
                }
            }
            else
            {
                //TODO: Logs
            }
        }

        //TODO: Sarebbe meglio eseguire una verifica di esistenza dell ordine a prescindere dal commento
        //TODO: Logs
        private void Instance_OrderAdded(OrderHistory obj)
        {
            if (obj.Status != OrderStatus.Opened || string.IsNullOrEmpty(obj?.Comment))
            {
                var modifiedKey = _itemsDictionary.FirstOrDefault(kvp => kvp.Value.Contains(obj.Id)).Key;
                //TODO: A volte e nullo forse perche e stato spostato
                try
                {
                    var item = Items.FirstOrDefault(x => x.Id == modifiedKey);

                    if (item == null)
                        item = ClosedItems.FirstOrDefault(x => x.Id == modifiedKey);

                    item.UpdateOrders(obj);
                }
                catch (Exception)
                {
                    //TODO: Logga
                    throw;
                }

            }
            else
            {
                var comment = this.GetSplittedComment(obj.Comment);

                if (comment != null && comment is KeyValuePair<string, OrderTypeSubcomment> parsedComment)
                {
                    lock (_lockObj)
                    {
                        this.CreateItem(parsedComment.Key);

                        var selected = this.MatchItems(parsedComment.Key);
                        _itemsDictionary[selected.Id].Add(obj.Id);

                        try
                        {
                            // NEXT: gestione incoerente in caso di ingressi multipli
                            // a meno che non si raggruppi tramite un id condiviso
                            switch (parsedComment.Value)
                            {
                                case OrderTypeSubcomment.Entry:
                                    selected.AttachEntryOrder(obj);
                                    break;
                                case OrderTypeSubcomment.StopLoss:
                                    selected.AttachSlOrder(obj);
                                    break;
                                case OrderTypeSubcomment.TakeProfit:
                                    selected.AttachTpOrder(obj);
                                    break;
                            }
                        }
                        catch (Exception)
                        {
                            //TODO: Logs
                            throw;
                        }
                    }
                }
                else
                {
                    // TODO: Dispatch
                }
            }
        }
        #endregion

        #region Utility


        #region 📘 REQ [NEXT]

        public override void UpdateSl(SlTpItems item, Func<double, double> updateFunction)
        {
            item.UpdateSlOrders(updateFunction);
        }

        public override void UpdateTp(SlTpItems item, Func<double, double> updateFunction)
        {
            item.UpdateTpOrders(updateFunction);
        }

        public void UpdateTp(string OrderComment, Func<double> updateFunction)
        {
            try
            {
                SlTpItems item = MatchItems(OrderComment);
            }
            catch (Exception ex)
            {
                //  TODO : Log
                throw ex;
            }
        }

        // HAndle Dispatcher
        public override void PlaceEntryOrder(PlaceOrderRequestParameters req, string comment, List<PlaceOrderRequestParameters> sl, List<PlaceOrderRequestParameters> tp, object sender = null)
        {
            req.Comment = $"{comment}.{OrderTypeSubcomment.Entry.ToString()}";

            var reqest = Core.Instance.PlaceOrder(req);
            var done = false;
            //TODO:Logs

            while (reqest.Status == TradingOperationResultStatus.Success && !done)
            {
                foreach (var slOrder in sl)
                {
                    slOrder.Comment = $"{comment}.{OrderTypeSubcomment.StopLoss.ToString()}";
                    reqest = Core.Instance.PlaceOrder(slOrder);
                }

                foreach (var tpOrder in tp)
                {
                    tpOrder.Comment = $"{comment}.{OrderTypeSubcomment.TakeProfit.ToString()}";
                    reqest = Core.Instance.PlaceOrder(tpOrder);
                }

                done = true;

                //ISSUE: Dispatcher Target is null
                //_dispatcher.Dispatch(new TradingOperations(reqest, sender));
            }

            if (reqest.Status == TradingOperationResultStatus.Success)
            {
                //ISSUE: Dispatcher Target is null
                //_dispatcher.Dispatch(new TradingErrors(reqest, sender));
            }

            else
            {
                //TODO:Logs
                //TODO:Handle failure
            }
        }
        #endregion

        // todo: Crea un metodo che reverta le posizioni aperte , prima chiude tutte quelle aperte e poi le gira nell altro lato    

        private void CloseItem(SlTpItems item)
        {
            lock (_lockObj)
            {
                if (!ClosedItems.Any(x => x.Id == item.Id))
                {
                    ClosedItems.Add(item);
                    Items.Remove(item);
                    //TODO:Core.Instance.Loggers.Log($"✅ Item chiuso e spostato in ClosedItems: {item.Id}", LoggingLevel.Info);
                }
                else
                {
                    //TODO:Core.Instance.Loggers.Log($"⚠️ Tentativo di chiudere un item già chiuso: {item.Id}", LoggingLevel.Warning);
                }
            }
        }

        public override void CreateItem(string comment)
        {
            if (!_itemsDictionary.ContainsKey(comment))
            {
                base.CreateItem(comment);
                //TODO: Core.Instance.Loggers.Log($"✅ Creato nuovo SlTpItems con ID: {comment}", LoggingLevel.Info);
            }
            else
            {
                //TODO:Core.Instance.Loggers.Log($"⚠️ Item con ID {comment} già esistente, non ricreato.", LoggingLevel.Warning);
            }
        }

        protected override SlTpItems CreateNewItem(string comment) => new SlTpItems(comment);

        /// <summary>
        /// Esegue una verifica di esistenza dell'oggetto su entrambe le liste
        /// </summary>
        /// <param name="OrderComment"></param>
        /// <returns></returns>
        private SlTpItems MatchItems(string OrderComment)
        {
            var result = Items.Where(x => x.Id == OrderComment).SingleOrDefault();
            if (result == null)
                // Piu di un oggetto
                result = ClosedItems.Where(x => x.Id == OrderComment).SingleOrDefault();

            return result;
        }

        public override void Dispose()
        {
            //TODO: chiudere tutte le posizioni
            Items = null;
            ClosedItems = null;

            Core.Instance.OrdersHistoryAdded -= this.Instance_OrderAdded;
            Core.Instance.TradeAdded -= this.Instance_TradeAdded;
        }
        #endregion
    }
   
}
