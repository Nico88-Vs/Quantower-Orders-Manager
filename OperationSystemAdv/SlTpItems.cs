using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DivergentStrV0_1.OperationSystem;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public enum PositionManagerStatus
    {
        Created,
        Placed,
        PartialyFilled,
        Filled,
        PartialyClosed,
        Closed
    }
    public class SlTpItems
    {
        public string Id { get; private set; }
        public PositionManagerStatus Status { get; private set; }
        public Order EntryOrder { get; private set; }
        public OrderHistory EntryOrderHistory { get; set; }
        public List<Order> SlOrders { get; private set; }
        public List<Order> TpOrders { get; private set; }
        public Side Side { get; private set; }

        public Position RelatedPosition { get; private set; }

        public double NetProfit { get; private set; }
        public double FilledQuantity { get; private set; }
        public double ClosedQuantity { get; private set; }


        private List<IDomainEvent> _domainEvents = new();
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        public SlTpItems(string id)
        {
            Id = id;

            SlOrders = new List<Order>();
            TpOrders = new List<Order>();

            Status = PositionManagerStatus.Created;
        }

        public void AttachEntryOrder(Order order)
        {
            EntryOrder = order;
            Side = order.Side;
            Status = PositionManagerStatus.Placed;
        }

        public void AttachSlOrder(Order order)
        {
            if (order.Side == Side)
                return;
            //TODO: LOG

            {
                switch (Side)
                {
                    case Side.Buy:
                        if (order.TriggerPrice > EntryOrder.TriggerPrice)
                            TpOrders.Add(order);  // TP se il trigger è sopra l'entry
                        else
                            SlOrders.Add(order);  // SL se il trigger è sotto l'entry
                        break;
                    case Side.Sell:
                        if (order.TriggerPrice < EntryOrder.TriggerPrice)
                            TpOrders.Add(order);  // TP se il trigger è sotto l'entry
                        else
                            SlOrders.Add(order);  // SL se il trigger è sopra l'entry
                        break;
                }
            }
        }

        public void RegisterTrade(Trade trade)
        {
            if (trade.PositionImpactType == PositionImpactType.Open)
            {
                UpdateOpenState(trade);
            }
            else if (trade.PositionImpactType == PositionImpactType.Close)
            {
                UpdateCloseState(trade);
            }
        }

        private void UpdateOpenState(Trade trade)
        {
            TryAttachPosition(trade);
            if (RelatedPosition == null)
            {
                //TODO: Loggable.Log($"Position not found for trade {trade.Id}");
                CloseAll();
                return;
            }

            if (trade.Comment == EntryOrder.Comment)
            {
                FilledQuantity += trade.Quantity;
                bool isFilled = EntryOrder.RemainingQuantity == 0;

                Status = isFilled ? PositionManagerStatus.Filled : PositionManagerStatus.PartialyFilled;
                _domainEvents.Add(new TradeFilledEvent(Id, trade.Quantity));
            }
        }

        private void UpdateCloseState(Trade trade)
        {
            if (SlOrders.Any(o => o.Id == trade.OrderId) || TpOrders.Any(o => o.Id == trade.OrderId))
            {
                ClosedQuantity += trade.Quantity;
                NetProfit += trade.NetPnl.Value;
                Status = PositionManagerStatus.PartialyClosed;

                //TODO: Centraliziamo qui i log (negli eventi)
                _domainEvents.Add(new PartialCloseEvent(Id, trade.NetPnl.Value));
            }
        }

        public void CloseAll()
        {
            Status = PositionManagerStatus.Closed;
            _domainEvents.Add(new PositionClosedEvent(Id));
        }

        private void TryAttachPosition(Trade trade)
        {
            if (RelatedPosition != null) return;

            var position = Core.Instance.Positions
                .FirstOrDefault(p => p.Account == trade.Account && p.Side == trade.Side && p.Symbol == trade.Symbol && p.Id == trade.OrderId);

            if (position != null)
            {
                RelatedPosition = position;
            }
        }
    }

}
