using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Xml.Linq;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public enum OrderTypeSubcomment
    {
        Entry,
        StopLoss,
        TakeProfit
    }

    public sealed class GlobalTpSlManager
    {
        private static readonly Lazy<TpSlManager> lazyInstance = new(() => new TpSlManager());

        public static TpSlManager Instance => lazyInstance.Value;

        // Prevent instantiation
        private GlobalTpSlManager() { }
    }


    public class TpSlManager
    {
        #region Properties 
        public List<SlTpItems> Items { get; private set; }
        public List<SlTpItems> ClosedItems { get; private set; }
        private IDomainEventDispatcher _dispatcher;
        public int TradeCount { get; private set; } = 0;
        private Dictionary<string, List<string>> _itemsDictionary;
        #endregion

        public TpSlManager()
        {
            Core.Instance.Loggers.Log("[TpSlManager] Costruttore invocato", LoggingLevel.System);
            Items = new List<SlTpItems>();
            ClosedItems = new List<SlTpItems>();
            _itemsDictionary = new Dictionary<string, List<string>>();

            #region 📘 REQ [System]
            //TODO: Unscribe: on close or somenthing
            Core.Instance.OrderAdded += this.Instance_OrderAdded;
            Core.Instance.OrdersHistoryAdded += this.Instance_OrdersHistoryAdded;
            Core.Instance.TradeAdded += this.Instance_TradeAdded;
            #endregion


            #region 🧪 HACK [Debug]
            //TODO: debug remove: Sottoscrizione Temporanea ai fini di debugging
            Core.Instance.PositionAdded +=this.Instance_PositionAdded;
            Core.Instance.PositionRemoved +=this.Instance_PositionRemoved;

        }

        //TODO: debug remove:
        private void Instance_PositionRemoved(Position obj)
        {
            var x = obj;
        }
        private void Instance_PositionAdded(Position obj)
        {
            var x = obj;
        }
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
                    TradeCount++;
                    if (match.Status == PositionManagerStatus.Closed || match.Status == PositionManagerStatus.Aborted)
                    {
                        ClosedItems.Add(match);
                        Items.Remove(match);
                    }
                }
            }
            else
            {
                //TODO: Logs
            }
        }


        
        private void Instance_OrdersHistoryAdded(OrderHistory obj)
        {
            var comment = this.GetSplittedComment(obj.Comment);

            if (comment != null && comment is KeyValuePair<string, OrderTypeSubcomment> parsedComment)
            {
                var match = this.MatchItems(parsedComment.Key);

                try
                {
                    match.AttachHistoryOrder(obj);
                }
                catch (Exception)
                {
                    //TODO: Logs
                    throw;
                }
            }
            else
            {
                //TODO: LOG
            }
        }


        #region 🐞 BUG [OrdersUpdate]
        //BUG: VERIFICARE >>>> Viene eseguito un ciclo d inserimento di troppo 
        //TODO: Sarebbe meglio eseguire una verifica di esistenza dell ordine a prescindere dal commento
        //TODO: Logs
        private void Instance_OrderAdded(Order obj)
        {
            if (string.IsNullOrEmpty(obj?.Comment))
            {
                var modifiedKey = _itemsDictionary.FirstOrDefault(kvp => kvp.Value.Contains(obj.Id)).Key;
                Items.FirstOrDefault(x => x.Id == modifiedKey).UpdateOrders(obj);
                return;
            }

            var comment = this.GetSplittedComment(obj.Comment);

            if (comment != null && comment is KeyValuePair<string, OrderTypeSubcomment> parsedComment)
            {
                if (!_itemsDictionary.Keys.Contains(parsedComment.Key))
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
            else
            {
                // TODO: Dispatch
            }
        }
        #endregion


        #region 📘 REQ [NEXT]
        // HAndle Dispatcher
        public void PlaceEntryOrder(PlaceOrderRequestParameters req, string comment, List<PlaceOrderRequestParameters> sl, List<PlaceOrderRequestParameters> tp, object sender = null)
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
        #endregion

        #region Utility
        private void CreateItem(string comment)
        {
            var item = new SlTpItems(comment);
            Items.Add(item);
            _itemsDictionary.Add(item.Id, new List<string>());
        }



        /// <summary>
        /// Match Items Senza distinguere fra Items apertio e chiusi
        /// </summary>
        /// <param name="OrderComment"></param>
        /// <returns></returns>
        private SlTpItems MatchItems(string OrderComment)
        {
            var result = Items.Where(x => x.Id == OrderComment).SingleOrDefault();
            if (result == null)
                result = ClosedItems.Where(x => x.Id == OrderComment).SingleOrDefault();

            return result;
        }
        private KeyValuePair<string, OrderTypeSubcomment>? GetSplittedComment(string comment)
        {
            try
            {
                var splittedcomment = comment.Split('.');

                if (splittedcomment.Length == 2)
                {
                    var success = Enum.TryParse<OrderTypeSubcomment>(splittedcomment[1], out OrderTypeSubcomment type);
                    if (success)
                    {
                        return new KeyValuePair<string, OrderTypeSubcomment>(splittedcomment[0], type);
                    }
                    else
                    {
                        // TODO: LOGS
                        return null;
                    }
                }
                else
                {
                    // TODO: LOGS
                    return null;
                }
            }
            catch (Exception)
            {
                // TODO: LOGS
                return null;
            }
        }
        #endregion
    }
    //HINT : A cosa serve?
    public static class DomainEventDispatcherExtensions
    {
        public static void DispatchEventsFrom(this IDomainEventDispatcher dispatcher, SlTpItems item)
        {
            item.DispatchEvents(dispatcher);
        }
    }


}
