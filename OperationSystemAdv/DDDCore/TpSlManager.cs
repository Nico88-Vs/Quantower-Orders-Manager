using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public class TpSlManager<T>
    {
        private readonly List<SlTpItems> _items = new();
        private SlTpCondictionHolder<T> _delegates;
        private TpSlComputator<T> _computator;

        public IEnumerable<SlTpItems> Items => _items.AsReadOnly();
        private IDomainEventDispatcher _dispatcher;
        private object _debug;


        public double NetProfit => _items.Sum(i => i.NetProfit);

        public TpSlManager(SlTpCondictionHolder<T> holder)
        {
            _delegates = holder;
            _computator = new TpSlComputator<T>(holder);

            Core.Instance.OrderAdded += this.Instance_OrderAdded;
            Core.Instance.OrdersHistoryAdded += this.Instance_OrdersHistoryAdded;
            Core.Instance.TradeAdded += this.Instance_TradeAdded;
            Core.Instance.PositionAdded += this.Instance_PositionAdded;
        }

        private void Instance_PositionAdded(Position obj)
        {
            _debug = obj;
        }

        #region QTEvents
        private void Instance_TradeAdded(Trade trade)
        {
            var match = this.MatchItems(trade.Comment);

            if (match != null)
            {

                if (match.RelatedPosition == null)
                {
                        //TODO: fino a qui 
                        //TODO: evitiamo l utilizzo di posizioni
                    match.TryAttachPosition(trade.UniqueId);

                    if (match.Status == PositionManagerStatus.PositionAttacheded)
                    {
                        //_computator.PlaceOrders(obj, match.Id, obj.Quantity);
                    }
                }
                match?.RegisterTrade(trade);
            }
            else
            {
                //TODO: Logs
            }
        }
        private void Instance_OrdersHistoryAdded(OrderHistory obj)
        {
            var match = this.MatchItems(obj.Comment);
            if (match != null)
            {
                match.AttachHistoryOrder(obj);
            }
        }
        private void Instance_OrderAdded(Order obj)
        {
            var selected = this.MatchItems(obj.Comment);

            if (selected != null)
            {
                switch (selected.Status)
                {
                    case PositionManagerStatus.Created:
                        selected.AttachEntryOrder(obj);
                        break;
                    case PositionManagerStatus.Placed:
                        selected.AttachSlOrder(obj);
                        break;
                   
                    default:
                        break;
                }
                
            }
            else
            {
                //TODO: Dispatch
            }
        }
        #endregion

        public void PlaceEntryOrder(PlaceOrderRequestParameters req, string comment, IConditionable sender = null)
        {
            var reqest = Core.Instance.PlaceOrder(req);

            if (reqest.Status == TradingOperationResultStatus.Success)
            {
                var item = new SlTpItems(comment);
                _items.Add(item);

                //TODO: Dispatcher Target is null
                //_dispatcher.Dispatch(new TradingOperations(reqest, sender));
            }

            else
            {
                //TODO: Dispatcher Target is null
                //_dispatcher.Dispatch(new TradingErrors(reqest, sender));
            }
        }

        private SlTpItems MatchItems(string OrderComment)
        {
            return _items.Where(x => x.Id == OrderComment).SingleOrDefault();
        }
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
