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

    public class TpSlManager
    {
        #region Properties
        //private SlTpCondictionHolder<T> _delegates;
        //private TpSlComputator<T> _computator;

        public List<SlTpItems> Items { get; private set; }
        public List<SlTpItems> ClosedItems { get; private set; }
        private IDomainEventDispatcher _dispatcher;
        private int _tradeCount = 0;

        #region Metrics
        //TODO: SHARE those metrics with the domain
        public double NetProfit => Items.Sum(i => i.NetProfit)+ ClosedItems.Sum(i => i.NetProfit);
        public double GrossProfit => Items.Sum(i => i.GrossProfit)+ ClosedItems.Sum(i => i.GrossProfit);
        public double PaiedFees => Items.Sum(i => i.Fees)+ ClosedItems.Sum(i => i.Fees);
        public int N_Positive_Operations => ClosedItems.Count(i => i.GrossProfit > 0) + ClosedItems.Count(i => i.GrossProfit > 0);
        public int N_Negative_Operations => ClosedItems.Count(i => i.GrossProfit <= 0) + ClosedItems.Count(i => i.GrossProfit <= 0);
        public int N_Short => ClosedItems.Count(i => i.Side == Side.Sell) + ClosedItems.Count(i => i.Side == Side.Sell);
        public int N_Long=> ClosedItems.Count(i => i.Side == Side.Buy) + ClosedItems.Count(i => i.Side == Side.Buy);
        public int N_Positive_Longs => ClosedItems.Count(i => i.Side == Side.Buy && i.GrossProfit > 0) + ClosedItems.Count(i => i.Side == Side.Buy && i.GrossProfit > 0);
        public int N_Positive_Short => ClosedItems.Count(i => i.Side == Side.Sell && i.GrossProfit > 0) + ClosedItems.Count(i => i.Side == Side.Sell && i.GrossProfit > 0);public int N_Positive_Long => ClosedItems.Count(i => i.Side == Side.Buy && i.GrossProfit > 0) + ClosedItems.Count(i => i.Side == Side.Buy && i.GrossProfit > 0);
        public int N_Negative_Short => N_Short - N_Positive_Short;
        public int N_Negative_Long => N_Negative_Operations - N_Negative_Short;
        public int N_Operations => ClosedItems.Count() + ClosedItems.Count();

        public int TradeCount => _tradeCount;
        #endregion

        #endregion

        public TpSlManager()
        {
            Items = new List<SlTpItems>();
            ClosedItems = new List<SlTpItems>();
            
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
                    _tradeCount++;
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
        private void Instance_OrdersHistoryAdded(OrderHistory obj)
        {
            var comment = this.GetSplittedComment(obj.Comment);

            if (comment != null && comment is KeyValuePair<string, OrderTypeSubcomment> parsedComment)
            {
                var match = this.MatchItems(parsedComment.Key);
                match.AttachHistoryOrder(obj);
            }
            else
            {
                //TODO: LOG
            }
        }
        //REQ : Inser sl e tp in un ordine
        private void Instance_OrderAdded(Order obj)
        {
            var comment = this.GetSplittedComment(obj.Comment);

            if (comment != null && comment is KeyValuePair<string, OrderTypeSubcomment> parsedComment)
            {
                var selected = this.MatchItems(parsedComment.Key);

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
            else
            {
                // TODO: Dispatch
            }
        }
        public void PlaceEntryOrder(PlaceOrderRequestParameters req, string comment, List<PlaceOrderRequestParameters> sl, List<PlaceOrderRequestParameters> tp, IConditionable sender = null)
        {
            req.Comment = $"{comment}.{OrderTypeSubcomment.Entry.ToString()}";

            var reqest = Core.Instance.PlaceOrder(req);
            //TODo:Logs

            while (reqest.Status == TradingOperationResultStatus.Success)
            {
                var item = new SlTpItems(comment);
                Items.Add(item);

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
        
        private SlTpItems MatchItems(string OrderComment)
        {
            return Items.Where(x => x.Id == OrderComment).SingleOrDefault();
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
