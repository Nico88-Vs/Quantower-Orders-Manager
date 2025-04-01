using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TpSlManager;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public class TpSlManager<T> : ITpSlManager<T>
    {
        private readonly List<SlTpItems> _items = new();
        private SlTpCondictionHolder<T> _delegates;
        private TpSlComputator<T> _computator;

        private int _maxShortExpo;
        private int _maxLongExpo;

        public IEnumerable<SlTpItems> Items => _items.AsReadOnly();

        public double NetProfit => _items.Sum(i => i.NetProfit);

        public void Init(SlTpCondictionHolder<T> holder, int maxShort = 3, int maxLong = 3)
        {
            _delegates = holder;
            _computator = new TpSlComputator<T>(holder);
            _maxShortExpo = maxShort;
            _maxLongExpo = maxLong;

            Core.Instance.OrderAdded += OnOrderAdded;
            Core.Instance.OrdersHistoryAdded += OnOrderHistoryAdded;
            Core.Instance.TradeAdded += OnTradeAdded;
            Core.Instance.PositionRemoved += OnPositionRemoved;
        }

        public SlTpItems PlaceEntryOrder(string id)
        {
            var item = new SlTpItems(id);
            _items.Add(item);
            return item;
        }
        #region QTEvents
        private void OnOrderAdded(Order order)
        {
            var match = _items.FirstOrDefault(x => x.Id == order.Comment & x.Status == PositionManagerStatus.Created);
            if (match != null)
            {
                if (match.Status == PositionManagerStatus.Created)
                {
                    if (match.EntryOrder != null)
                        return;
                    //TODO: LOG

                    match.AttachEntryOrder(order);
                }

                if (match.Status != PositionManagerStatus.Placed)
                {
                    if (match.EntryOrder == null)
                        return;
                    //TODO: LOG

                    match.AttachSlOrder(order);
                }
            }
        }

        private void OnOrderHistoryAdded(OrderHistory history)
        {
            var match = _items.FirstOrDefault(x => x.EntryOrder.Id == history.Id);
            if (match != null)
            {
                match.EntryOrderHistory = history;
            }
        }

        private void OnTradeAdded(Trade trade)
        {
            var match = _items.FirstOrDefault(x => x.Id == trade.Comment);
            match?.RegisterTrade(trade);
        }

        private void OnPositionRemoved(Position position)
        {
            foreach (var item in _items.Where(x => x.RelatedPosition?.Id == position.Id && x.Status != PositionManagerStatus.Closed))
            {
                item.CloseAll();
            }
        }
        #endregion

        public void RegisterTrade(Trade trade) => OnTradeAdded(trade);

        public void RegisterPositionRemoved(Position position) => OnPositionRemoved(position);
    }

}
