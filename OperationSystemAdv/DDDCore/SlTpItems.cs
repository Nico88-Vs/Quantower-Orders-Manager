using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Integration;

namespace DivergentStrV0_1.OperationSystemAdv
{
    //REQ : Update Status
    //REQ : Retrive Metrics
    //REQ : Update Orders in case of orders modification
    //TODO : Aggiungere una gestione di ingressi multipli
    //TODO : Aggiungere una metodo di suddivisione delle Hystory , uscita entrata tp sl
    public enum PositionManagerStatus
    {
        Created,
        Placed,
        ExitOrderPlaced,
        PartialyFilled,
        Filled,
        PartialyClosed,
        Closed,
        Aborted
    }
    public class SlTpItems
    {
        public string Id { get; private set; }
        public PositionManagerStatus Status { get; private set; }
        public Order EntryOrder { get; private set; }
        public List<OrderHistory> EntryOrderHistory { get; private set; }
        public List<OrderHistory> ExitOrderHistory { get; private set; }
        public List<Trade> EntryTrades { get; private set; }
        public List<Trade> ExitTrades { get; private set; }
        public List<Order> SlOrders { get; private set; }
        public List<Order> TpOrders { get; private set; }
        public Side Side { get; private set; }

        public double GrossProfit { get; private set; } = 0.0;
        public double FilledQuantity { get; private set; } = 0.0;
        public double Fees { get; private set; } = 0.0;
        public double NetProfit
        {
            get
            {
                try
                {
                    return GrossProfit - Fees;
                }
                catch (Exception)
                {
                    return double.NaN;
                }
            }
        }
        public double RemainQuantity
        {
            get
            {
                try
                {
                    return Quantity - FilledQuantity;
                }
                catch
                {
                    return double.NaN;
                }
            }
        }
        public double ClosedQuantity { get; private set; } = 0.0;
        public double Quantity { get; private set; } = 0.0;


        private List<IDomainEvent> _domainEvents = new();
        public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

        public SlTpItems(string id)
        {
            Id = id;

            SlOrders = new List<Order>();
            TpOrders = new List<Order>();

            Status = PositionManagerStatus.Created;

            EntryOrderHistory = new List<OrderHistory>();
            ExitOrderHistory = new List<OrderHistory>();
            Status = PositionManagerStatus.Placed;
            EntryTrades = new List<Trade>();
            ExitTrades = new List<Trade>();
        }

        //REQ: Update Status while adding new entry Orders
        //TODO: Make it Suitable for multiple entry orders
        public void AttachEntryOrder(Order order)
        {
            EntryOrder = order;
            Side = order.Side;

            this.Quantity += order.TotalQuantity;

            if (this.Status == PositionManagerStatus.Created)
                this.Status = PositionManagerStatus.Placed;

            //TODO: Dispatch
        }

        public void AttachTpOrder(Order order)
        {
            if (order.Side == Side)
                return;
            //TODO: LOG
            else
            {
                if (Status == PositionManagerStatus.Placed)
                    Status = PositionManagerStatus.ExitOrderPlaced;

                switch (Side)
                {
                    case Side.Buy:
                        if (order.TriggerPrice > EntryOrder.TriggerPrice)
                            TpOrders.Add(order);  // TP se il trigger è sotto l'entry
                        else
                        {
                            //TODO: LOG
                        }
                        break;

                    case Side.Sell:
                        if (order.TriggerPrice < EntryOrder.TriggerPrice)
                            TpOrders.Add(order);  // TP se il trigger è sotto l'entry
                        else
                        {
                            //TODO: LOG
                        }
                        break;
                }

                //TODO: LOG
            }
        }

        public void AttachSlOrder(Order order)
        {
            if (order.Side == Side)
                return;
            //TODO: LOG

            else    
            {
                if (Status == PositionManagerStatus.Placed)
                    Status = PositionManagerStatus.ExitOrderPlaced;

                switch (Side)
                {
                    case Side.Buy:
                        if (order.TriggerPrice > EntryOrder.TriggerPrice)
                        {
                            //TODO: LOG
                        }

                        else
                            SlOrders.Add(order);  // SL se il trigger è sotto l'entry
                        break;
                    case Side.Sell:
                        if (order.TriggerPrice < EntryOrder.TriggerPrice)
                        {
                            //TODO: LOG
                        }
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
            //TODO: Implementare
            switch (trade.PositionImpactType)
            {
                case PositionImpactType.Open:
                    EntryTrades.Add(trade);
                    FilledQuantity += trade.Quantity;
                    Fees += trade.Fee.Value;

                    if (RemainQuantity != double.NaN && RemainQuantity == 0)
                        Status = PositionManagerStatus.Filled;
                    else if (RemainQuantity != double.NaN && RemainQuantity > 0)
                        Status = PositionManagerStatus.PartialyFilled;
                    else
                    {
                        //TODO: LOG
                    }
                    break;
                
                case PositionImpactType.Close:
                    ExitTrades.Add(trade);
                    try
                    {
                        GrossProfit += trade.GrossPnl.Value;
                        Fees += trade.Fee.Value;
                        ClosedQuantity += trade.Quantity;

                        if (ClosedQuantity == FilledQuantity)
                        {
                            Status = PositionManagerStatus.Closed;
                            this.CloseAll();
                        }
                        else if (ClosedQuantity < FilledQuantity)
                            Status = PositionManagerStatus.PartialyClosed;
                    }
                    catch (Exception)
                    {
                        //TODO:LOGS
                        throw;
                    }
                    break;
            }
        }

        public void CloseAll()
        {
            Status = PositionManagerStatus.Closed;

            if(this.RemainQuantity != 0)
                EntryOrder.Cancel(sendingSource:this.ToString());
            //TODO: log
            //TODO: handle the case of multiple entry orders
            //TODO: handle failures
        }

        //REQ: hANDLE DISPATCHER
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
