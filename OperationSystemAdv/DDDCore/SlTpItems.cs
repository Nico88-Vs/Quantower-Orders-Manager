using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Integration;

namespace DivergentStrV0_1.OperationSystemAdv
{
    //TODO : Aggiungere una gestione di ingressi multipli
    //TODO : Aggiungere una metodo di suddivisione delle Hystory , uscita entrata tp sl
    public enum PositionManagerStatus
    {
        Created,
        Placed,
        PositionAttacheded,
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
        public List<OrderHistory> EntryOrderHistory { get; private set; }
        public List<OrderHistory> ExitOrderHistory { get; private set; }
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
            EntryOrderHistory = new List<OrderHistory>();
            ExitOrderHistory = new List<OrderHistory>();
            Status = PositionManagerStatus.Placed;
            EntryOrder = order;
            Side = order.Side;
            //TODO: Dispatch
        }

        public void AttachSlOrder(Order order)
        {
            if (order.Side == Side)
                return;
            //TODO: LOG

            else    
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

                //TODO: LOG
            }
        }

        public void AttachHistoryOrder(OrderHistory order)
        {
            if (order.Side == Side)
                EntryOrderHistory.Add(order);
            else
                ExitOrderHistory.Add(order);
        }

        public void RegisterTrade(Trade trade)
        {
            if (this.RelatedPosition == null)
                this.CloseAll();

            //TODO: Implementare
            if (trade.PositionImpactType == PositionImpactType.Open)
            {
                UpdateOpenState(trade);
            }
            //else if (trade.PositionImpactType == PositionImpactType.Close)
            //{
            //    UpdateCloseState(trade);
            //}
        }

        private void UpdateOpenState(Trade trade)
        {
            //TODO: Implementare
            //TryAttachPosition(trade);
            //if (RelatedPosition == null)
            //{
            //    //TODO: Loggable.Log($"Position not found for trade {trade.Id}");
            //    CloseAll();
            //    return;
            //}

            //if (trade.Comment == EntryOrder.Comment)
            //{
            //    FilledQuantity += trade.Quantity;
            //    bool isFilled = EntryOrder.RemainingQuantity == 0;

            //    Status = isFilled ? PositionManagerStatus.Filled : PositionManagerStatus.PartialyFilled;
            //    _domainEvents.Add(new TradeFilledEvent(Id, trade.Quantity));
            //}
        }

        private void UpdateCloseState(Trade trade)
        {
            //TODO: Implementare
            //if (SlOrders.Any(o => o.Id == trade.OrderId) || TpOrders.Any(o => o.Id == trade.OrderId))
            //{
            //    ClosedQuantity += trade.Quantity;
            //    NetProfit += trade.NetPnl.Value;
            //    Status = PositionManagerStatus.PartialyClosed;

            //    //TODO: Centraliziamo qui i log (negli eventi)
            //    _domainEvents.Add(new PartialCloseEvent(Id, trade.NetPnl.Value));
            //}
        }

        public void CloseAll()
        {
            Status = PositionManagerStatus.Closed;
            //TODO: log
            //TODO: Remove All
        }

        public void TryAttachPosition(int uniqueId)
        {
            if (RelatedPosition != null) 
                //TODO: Logs
                return;

            if (this.Status != PositionManagerStatus.Placed)
                //TODO: Logs
                return;
            var debug = Core.Instance.Positions;
            var position = Core.Instance.Positions.FirstOrDefault(x => x.Id == positionId);

            if (position != null)
            {
                this.Status = PositionManagerStatus.PositionAttacheded;
                RelatedPosition = position;
            }
            else
                this.CloseAll();
        }

        public void AddEvent(IDomainEvent domainEvent)
        {
            _domainEvents.Add(domainEvent);
        }

        public void DispatchEvents(IDomainEventDispatcher dispatcher)
        {
            foreach (var e in _domainEvents)
                dispatcher.Dispatch(e);

            _domainEvents.Clear();
        }
    }

   
}
