using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Integration;

namespace DivergentStrV0_1.OperationSystemAdv
{
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

        #region 📘 REQ [SYSTEM]
        //REQ : Update Orders in case of orders modification
        //REQ : Implement Dispatcher ad DomainEvent
        //TODO : Aggiungere una gestione di ingressi multipli
        #endregion

        #region [Properties]
        public string Id { get; private set; }
        public PositionManagerStatus Status { get; private set; }
        public Order EntryOrder { get; private set; }
        //public List<OrderHistory> EntryOrderHistory { get; private set; }
        //public List<OrderHistory> ExitOrderHistory { get; private set; }
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
        #endregion

        public SlTpItems(string id)
        {
            Id = id;

            SlOrders = new List<Order>();
            TpOrders = new List<Order>();

            Status = PositionManagerStatus.Created;

            //TODO: Deprecated
            //EntryOrderHistory = new List<OrderHistory>();
            //ExitOrderHistory = new List<OrderHistory>();
            Status = PositionManagerStatus.Placed;
            EntryTrades = new List<Trade>();
            ExitTrades = new List<Trade>();
        }

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


        #region 🧯 DEPRECATED [NEXT]
        /*
         * ⚠️ Questo blocco è inutile
         * TODO: sostituire o rimuovere
         */

        //public void AttachHistoryOrder(OrderHistory order)
        //{
        //    if (order.Side == Side)
        //        EntryOrderHistory.Add(order);
        //    else
        //        ExitOrderHistory.Add(order);
        //}
        #endregion


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


                        #region 🧪 HACK [SYSTEM]
                        // siccome alcuni arrotondamenti creano mismatch devo chiudere la posizione quando la differenza e minore di symb.minlot
                        // devo anche forzare la quantita rimanente a 0
                        #endregion
                        bool closed = (FilledQuantity - ClosedQuantity) < trade.Symbol.MinLot;


                        if (closed)
                        {
                            Status = PositionManagerStatus.Closed;
                            this.ClosedQuantity = this.FilledQuantity;
                            this.CloseAll();
                        }
                        else
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

        //HACK: cancello tutti gli ordini appena l operazione è chiusa potrebbe chiudersi troppo presto
        public void CloseAll()
        {
            Status = PositionManagerStatus.Closed;

            if(this.RemainQuantity != 0)
            {
                var result = EntryOrder.Cancel(sendingSource:this.ToString());

            }

            foreach (var item in SlOrders)
            {
                try
                {
                    if (Core.Instance.Orders.Any(x => x.Id == item.Id))
                    {
                        var result = Core.Instance.Orders.FirstOrDefault(x => x.Id == item.Id).Cancel(sendingSource: this.ToString());
                        if (result.Status == TradingOperationResultStatus.Success)
                        {
                            //TODO: Logs
                        }
                    }
                        

                }
                catch (Exception)
                {
                    //TODO: Logs

                    throw;
                }
            }

            
            foreach (var tpitem in TpOrders)
            {
                try
                {
                    if (Core.Instance.Orders.Any(x => x.Id == tpitem.Id))
                    {
                        var result = Core.Instance.Orders.FirstOrDefault(x => x.Id == tpitem.Id).Cancel(sendingSource: this.ToString());
                        if (result.Status == TradingOperationResultStatus.Success)
                        {
                            //TODO: Logs
                        }
                    }
                        

                }
                catch (Exception)
                {
                    //TODO: Logs

                    throw;
                }
            }
            //TODO: log
            //TODO: handle the case of multiple entry orders
            //TODO: handle failures
        }

        public void UpdateTpOrders(Func<double, double> updateFunction)
        {
            try
            {
                foreach (Order order in TpOrders)
                {
                    var order_obj = Core.Instance.Orders.FirstOrDefault(x => x.Id == order.Id);

                    if (order_obj.Status == OrderStatus.Opened)
                    {

                        double new_trigger = -1;
                        double new_price = -1;

                        if (order_obj.TriggerPrice.GetType() == typeof(double))
                            new_trigger = updateFunction(order_obj.TriggerPrice);

                        if (order_obj.Price.GetType() == typeof(double))
                            new_price = updateFunction(order_obj.Price);


                        Core.Instance.ModifyOrder(order_obj, triggerPrice: new_trigger > 0 ? new_price : order_obj.TriggerPrice, price: new_price > 0 ? new_price : order_obj.Price);
                    }

                }
            }
            catch (Exception ex)
            {

                // TODO Logs;
                throw new Exception("Errore durante l'aggiornamento degli ordini TP/SL", ex);
            }
        }

        public void UpdateSlOrders(Func<double, double> updateFunction)
        {
            try
            {
                foreach (Order order in SlOrders)
                {
                    var order_obj = Core.Instance.Orders.FirstOrDefault(x => x.Id == order.Id);

                    if (order_obj.Status == OrderStatus.Opened)
                    {

                        double new_trigger = -1;
                        double new_price = -1;

                        if (order_obj.TriggerPrice.GetType() == typeof(double))
                            new_trigger = updateFunction(order_obj.TriggerPrice);

                        if (order_obj.Price.GetType() == typeof(double))
                            new_price = updateFunction(order_obj.Price);


                        Core.Instance.ModifyOrder(order_obj, triggerPrice: new_trigger > 0 ? new_price : order_obj.TriggerPrice, price: new_price > 0 ? new_price : order_obj.Price);
                    }
                }
            }
            catch (Exception ex)
            {

                // TODO Logs;
                throw new Exception("Errore durante l'aggiornamento degli ordini TP/SL", ex);
            }
        }

        //HACK: tento l aggiornamento degli ordini conscio di perdere il commento 
        //nasce perchè vengono triggerati eventi multipli d addizione , penso all aggiornamento degli ordini
        public void UpdateOrders(Order newOrder)
        {
            try
            {
                if (SlOrders.Any(x => x.Id == newOrder.Id))
                {
                    var or = SlOrders.FirstOrDefault(x => x.Id == newOrder.Id);
                    SlOrders.Remove(or);
                    SlOrders.Add(newOrder);

                }
                else if (TpOrders.Any(x => x.Id == newOrder.Id))
                {
                    var or = TpOrders.FirstOrDefault(x => x.Id == newOrder.Id);
                    TpOrders.Remove(or);
                    TpOrders.Add(newOrder);
                }
                else if (EntryOrder.Id == newOrder.Id)
                {
                    EntryOrder = newOrder;
                }
                else
                {
                    //TODO: Logs

                }
            }
            catch (Exception)
            {
                //TODO: Logs
                throw;
            }
        }
    }

   
}
