using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv.DDDCore
{
    public class TpSlItems2 : ITpSlItems
    {
        #region [Properties]
        public string Id { get; private set; }
        public PositionManagerStatus Status
        {
            get
            {
                if (this.EntryOrder != null && this.EntryTrades.Count == 0)
                    return PositionManagerStatus.Placed;
                else if (this.EntryOrder != null && this.EntryTrades.Count > 0 && this.Exposed && this.ExitTrades.Count == 0)
                    if (this.FilledQuantity == this.Quantity)
                        return PositionManagerStatus.Filled;
                    else
                        return PositionManagerStatus.PartialyFilled;
                else if (this.EntryOrder != null && this.EntryTrades.Count > 0 && this.Exposed && this.ExitTrades.Count > 0)
                    if (this.ExposedQuantity == 0)
                        return PositionManagerStatus.Closed;
                    else
                        return PositionManagerStatus.PartialyClosed;
                else if (this.EntryOrder != null && this.EntryTrades.Count > 0 && !this.Exposed)
                    return PositionManagerStatus.Closed;
                else
                    return PositionManagerStatus.Created;
            }
        }
        private PositionManagerStatus _status;
        public Order EntryOrder { get; private set; }
        public List<Trade> EntryTrades { get; private set; }
        public List<Trade> ExitTrades { get; private set; }
        public List<OrderHistory> SlOrdersHistory { get; set; }
        public List<OrderHistory> TpOrdersHistory { get; set; }
        public List<OrderHistory> EntryOrdersHistory { get; set; }
        public List<Order> SlOrders { get; private set; }
        public List<Order> TpOrders { get; private set; }

        public Side Side { get; private set; }
        public Symbol Symbol { get; set; }
        public double GrossProfit
        {
            get
            {
                try
                {
                    var entryVol = 0.0;
                    var exitVol = 0.0;
                    foreach (var item in EntryTrades)
                        entryVol += (item.Price * item.Quantity);

                    foreach (var item in ExitTrades)
                        exitVol += (item.Price * item.Quantity);

                    return this.Side == Side.Buy ? exitVol - entryVol : entryVol - exitVol;
                }
                catch (Exception)
                {
                    return double.NaN;
                }
            }
        }
        public double FilledQuantity
        {
            get
            {
                try
                {
                    var EntryQtity = 0.0;
                    foreach (var item in EntryTrades)
                        EntryQtity += item.Quantity;

                    return this.Side == Side.Buy ? EntryQtity : -EntryQtity;
                }
                catch
                {
                    return double.NaN;
                }
            }
        }
        public double Fees
        {
            get
            {
                try
                {
                    var fees = 0.0;
                    var exitFees = 0.0;
                    foreach (var item in EntryTrades)
                        fees += item.Fee.Value;

                    foreach (var item in ExitTrades)
                        exitFees += item.Fee.Value;

                    return fees - exitFees;
                }
                catch (Exception)
                {
                    return double.NaN;
                }
            }
        }
        public event EventHandler QuitAll;
        public event EventHandler<PositionManagerStatus[]> ItemClosed;
        public double NetProfit
        {
            get
            {
                try
                {
                    return this.GrossProfit - Fees;
                }
                catch (Exception)
                {
                    return double.NaN;
                }
            }
        }
        public double ExposedQuantity
        {
            get
            {
                try
                {

                    return FilledQuantity - ClosedQuantity;
                }
                catch
                {
                    return double.NaN;
                }
            }
        }
        public double ClosedQuantity
        {
            get
            {
                try
                {
                    var ExitQtity = 0.0;
                    foreach (var item in ExitTrades)
                        ExitQtity += item.Quantity;

                    return this.Side == Side.Buy ? ExitQtity : -ExitQtity;
                }
                catch
                {
                    return double.NaN;
                }
            }
        }

        public bool Exposed
        {
            get
            {
                try
                {
                    return !(this.ExposedQuantity < this.Symbol.MinLot);
                }
                catch
                {
                    return true;
                }
            }
        }

        public double Quantity => this.Status != PositionManagerStatus.Created ? this.EntryOrder.TotalQuantity : double.MaxValue;
        public Position Position { get; private set; } = null;
        #endregion

        public void SetPosition(Position position) => this.Position = position;



        //📝 TODO: [Flow]
        //verifico la chiusura sul trade e la notifico tramite evento per poter aggiornare le liste 
        //tento la chiusura forzata degli item aperti in caso di problemi 
        //faccio un doppio check sullo stato delle posizioni e ordini alla chiusura delle posizioni note
        //update stop loss e take profit
        //implementare quit all per errori
        public void TryUpdateStatus()
        {
            var oldStatus = _status;
            _status = this.Status;


            if (oldStatus != _status)
            {
                Core.Instance.Loggers.Log($"PositionManager {Id} status changed to {_status}", LoggingLevel.Trading);
                if (_status == PositionManagerStatus.Closed)
                {
                    ItemClosed?.Invoke(this, new PositionManagerStatus[2] { oldStatus, _status });
                    this.Quit();
                }

            }

        }

        public TpSlItems2(string id)
        {
            Id = id;

            SlOrders = new List<Order>();
            TpOrders = new List<Order>();

            SlOrdersHistory = new List<OrderHistory>();
            EntryOrdersHistory = new List<OrderHistory>();
            TpOrdersHistory = new List<OrderHistory>();

            EntryTrades = new List<Trade>();
            ExitTrades = new List<Trade>();
        }
#nullable enable
        public TpSlItems2? TryUpdateOrder(Order order)
        {
            if (EntryOrder != null && EntryOrder.Id == order.Id)
            {
                EntryOrder = order;
                return this;
            }
            var sl = SlOrders.Where(x => x.Id == order.Id).SingleOrDefault();
            if (sl != null)
            {
                var i = SlOrders.FindIndex(x => x.Id == order.Id);
                SlOrders[i] = order;
                return this;
            }
            var tp = TpOrders.Where(x => x.Id == order.Id).SingleOrDefault();
            if (tp != null)
            {
                var i = TpOrders.FindIndex(x => x.Id == order.Id);
                TpOrders[i] = order;
                return this;
            }
            else
                return null;
        }

        public TpSlItems2? TryUpdateTrade(Trade trade)
        {
            if (EntryOrder != null && EntryOrder.Id == trade.OrderId)
                return this;
            var sl = SlOrders.Where(x => x.Id == trade.OrderId).SingleOrDefault();
            if (sl != null)
                return this;
            var tp = TpOrders.Where(x => x.Id == trade.OrderId).SingleOrDefault();
            if (tp != null)
                return this;
            var enhistory = EntryOrdersHistory.Where(x => x.Id == trade.OrderId).SingleOrDefault();
            if (enhistory != null)
                return this;
            var slhistory = SlOrdersHistory.Where(x => x.Id == trade.OrderId).SingleOrDefault();
            if (slhistory != null)
                return this;
            var tphistory = TpOrdersHistory.Where(x => x.Id == trade.OrderId).SingleOrDefault();
            if (tphistory != null)
                return this;
            else
                return null;
        }

        public TpSlItems2? TryUpdateOrder(OrderHistory order)
        {
            if (EntryOrder != null && EntryOrder.Id == order.Id)
            {
                EntryOrdersHistory.Add(order);
                return this;
            }
            var sl = SlOrders.Where(x => x.Id == order.Id).SingleOrDefault();
            if (sl != null)
            {
                SlOrdersHistory.Add(order);
                return this;
            }
            var tp = TpOrders.Where(x => x.Id == order.Id).SingleOrDefault();
            if (tp != null)
            {
                EntryOrdersHistory.Add(order);
                return this;
            }
            else
                return this;
        }

        public void AttachEntryOrder(Order order)
        {
            if (EntryOrder != null)
                this.EntryOrder = order;
            else
            {
                EntryOrder = order;
                Side = order.Side;
                Symbol = order.Symbol;
            }
        }

        public void AttachTpOrder(Order order)
        {
            if (order.Side == Side)
                Core.Instance.Loggers.Log($"Tentativo di aggiungere ordine TP con lato errato. Ordine ID: {order.Id}, Lato Ordine: {order.Side}, Lato Posizione: {this.Side}", LoggingLevel.Error);
            else
                TpOrders.Add(order);

        }

        public void AttachSlOrder(Order order)
        {
            if (order.Side == Side)
                Core.Instance.Loggers.Log($"Tentativo di aggiungere ordine SL con lato errato. Ordine ID: {order.Id}, Lato Ordine: {order.Side}, Lato Posizione: {this.Side}", LoggingLevel.Error);
            else
                SlOrders.Add(order);
        }

        public void Quit()
        {
            if (this.ExposedQuantity != 0)
            {
                var or = Core.Instance.Orders.FirstOrDefault(x => x.Id == EntryOrder.Id);
                if (or != null)
                {
                    TradingOperationResult result = or.Cancel(sendingSource: this.ToString());
                    if (result.Status == TradingOperationResultStatus.Failure)
                        this.QuitAll.Invoke(this, EventArgs.Empty);
                }

            }

            foreach (var item in SlOrders)
            {
                try
                {
                    if (Core.Instance.Orders.Any(x => x.Id == item.Id))
                    {
                        var result = Core.Instance.Orders.FirstOrDefault(x => x.Id == item.Id).Cancel(sendingSource: this.ToString());
                        if (result.Status == TradingOperationResultStatus.Failure)
                            this.QuitAll.Invoke(this, EventArgs.Empty);
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
                            if (result.Status == TradingOperationResultStatus.Failure)
                                this.QuitAll.Invoke(this, EventArgs.Empty);
                    }


                }
                catch (Exception)
                {
                    //TODO: Logs

                    throw;
                }
            }
            this.ItemClosed.Invoke(this, new PositionManagerStatus[2] { this.Status, PositionManagerStatus.Closed });
            //TODO: log
            //TODO: handle the case of multiple entry orders
            //TODO: handle failures
        }

        public void UpdateTpOrders(Func<double, double> updateFunction)
        {
            try
            {
                foreach (OrderHistory order in TpOrders)
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
                foreach (OrderHistory order in SlOrders)
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

                        if (new_trigger > 0 && new_price > 0 && new_price != order_obj.Price && new_trigger != order_obj.TriggerPrice)
                        {
                            Core.Instance.Loggers.Log(Id.ToString(), $"Modifica Ordine SL {order_obj.Id} Trigger: {order_obj.TriggerPrice} => {new_trigger} Price: {order_obj.Price} => {new_price}", loggingLevel: LoggingLevel.Trading);
                            Core.Instance.ModifyOrder(order_obj, triggerPrice: new_trigger > 0 ? new_trigger : order_obj.TriggerPrice, price: new_price > 0 ? new_price : order_obj.Price);
                        }
                    }
                }
            }
            catch (Exception ex)
            {

                // TODO Logs;
                throw new Exception("Errore durante l'aggiornamento degli ordini TP/SL", ex);
            }
        }
    }
}
