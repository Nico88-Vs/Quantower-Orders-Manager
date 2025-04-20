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

    //HACK: Aggiunta classe statica per evitare sottoscrizioni multiple
    //REQ : MAke it a singleton
    //TODO: unused
    public static class EventSubscribed
    {
        public static bool IsSubscribed { get; private set; } = false;

        public static void SetSubscribed()
        {
            IsSubscribed = true;
        }
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
        //private SlTpCondictionHolder<T> _delegates;
        //private TpSlComputator<T> _computator;
        //Deprecated

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

            //HINT: debugging sottoscrizioni multiple ..... soluzioni , static , singleton , static check , skipp null comment
            Core.Instance.OrderAdded += this.Instance_OrderAdded;
            Core.Instance.OrdersHistoryAdded += this.Instance_OrdersHistoryAdded;
            Core.Instance.TradeAdded += this.Instance_TradeAdded;

        }

        #region QTEvents
        //HACK: Passed CustomTrade to Items
        private void Instance_TradeAdded(Trade trade)
        {
            var comment = this.GetSplittedComment(trade.Comment);


            if (comment != null && comment is KeyValuePair<string, OrderTypeSubcomment> parsedComment)
            {
                var match = this.MatchItems(parsedComment.Key);

                match?.RegisterTrade(trade);
                //TODO: VERIFY Direction

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
        //HINT:Order History splitted entry exit
        //TODO: Viene eseguito un ciclo d inserimento di troppo 
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
        //REQ : Inser sl e tp in un ordine
        private void Instance_OrderAdded(Order obj)
        {
            //HACK: Sembra che questo evento venga chiamato due volte quindi evito skippando quando comment e null
            //HACK: Sarebbe meglio eseguire una verifica di esistenza dell ordine a prescindere dal commento
            //TODO: Logs
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
        public void PlaceEntryOrder(PlaceOrderRequestParameters req, string comment, List<PlaceOrderRequestParameters> sl, List<PlaceOrderRequestParameters> tp, object sender = null)
        {
            req.Comment = $"{comment}.{OrderTypeSubcomment.Entry.ToString()}";

            var reqest = Core.Instance.PlaceOrder(req);
            var done = false;
            //TODo:Logs

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
                //TODo:Logs
                //TODo:Handle failure
            }
        }
        #endregion

        #region Utility
        private void CreateItem(string comment)
        {
            var item = new SlTpItems(comment);
            Items.Add(item);
            _itemsDictionary.Add(item.Id, new List<string>());
        }
        
        private SlTpItems MatchItems(string OrderComment)
        {
            //HACK: gestisco qui gli scenari  in cui non trovo l ordine nella lista di posizioni aperte
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
