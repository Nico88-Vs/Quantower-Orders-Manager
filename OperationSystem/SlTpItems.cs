using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace TpSlManager
{
    public enum PositionManagerStatus
    {
        Placed,
        PartialyFilled,
        Filled,
        PartialyClosed,
        Closed
    }

    public class SlTpItems
    {
        #region attributi
        public PositionManagerStatus Status { get; set; }
        public string Id { get; set; }
        public Order EntryOrder { get; set; }
        public OrderHistory EntryOrderHistory { get; set; }
        public List<Order> SlItems { get; set; }
        public List<Order> TpItems { get; set; }
        public string Comment { get; set; }
        public Position RelatedPosition { get; set; }
        private double StartingAmmount { get; set; }
        public bool UsePosition { get; set; }
        public double NetProfit { get; set; } = 0;
        private List<string> UnAddedSl;
        private List<string> UnAddedTp;
        public double ClosedQuantity{ get; set; } = 0;
        //TODO: scenario partialy filled missing
        public double Quantity => this.EntryOrderHistory == null ? this.EntryOrder.TotalQuantity : this.EntryOrderHistory.TotalQuantity;
        private double FilledQuantity = 0;
        public Side Side => this.EntryOrder.Side;
        public double EntryPrice => this.EntryOrderHistory == null ? this.EntryOrder.Price : this.EntryOrderHistory.AverageFillPrice;
        #endregion

        public SlTpItems(Order order, string guid, bool useposition, string comment = "")
        {
            this.Status = PositionManagerStatus.Placed;
            this.UsePosition = useposition;
            this.Id = guid;
            this.EntryOrder = order;
            this.SlItems = new List<Order>();
            this.TpItems = new List<Order>();
            this.Comment = comment;
            this.NetProfit = 0;
            this.ClosedQuantity = 0;
            this.UnAddedSl = new List<string>();
            this.UnAddedTp = new List<string>();

            //Core.Instance.PositionRemoved += this.Instance_PositionRemoved;
        }

        //private void Instance_PositionRemoved(Position obj)
        //{
        //    //if (this.EntryOrder.Status == OrderStatus.Opened)
        //    //    return;
        //    //TODO:generalizzare a tutti gli item
        //    if (this.RelatedPosition.Id == obj.Id & UsePosition)
        //    {
        //        //HACK: provo con i tick
        //        var cost = this.EntryOrder.Symbol.GetTickCost(this.EntryOrder.Price);
        //        this.NetProfit = obj.GrossPnLTicks*cost;
        //        this.ClosedAll();
        //    }
        //}

        public void ClosePosition()
        {
            try
            {
                OrderStatus or_status = this.EntryOrderHistory == null ? this.EntryOrder.Status : EntryOrderHistory.Status;
                if (or_status != OrderStatus.Opened)
                    //TODO:spesso e nullo
                    Core.Instance.Positions.FirstOrDefault(x => x.Id == this.RelatedPosition.Id).Close();
                this.ClosedAll();
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log(ex.Message, LoggingLevel.Error);
            }
        }

        private void CheckForPosition(Trade trade)
        {
            try
            {
                Position temPosition = Core.Instance.Positions.FirstOrDefault(x => x.Account == trade.Account & x.State == BusinessObjectState.Normal &
                x.Side == trade.Side & x.Symbol == trade.Symbol);

                if (temPosition != null)
                {
                    this.RelatedPosition = temPosition;
                    this.StartingAmmount = this.RelatedPosition.Quantity - trade.Quantity;
                }
                //HACK: aggiungo un controllo per chiudere tutto se nn trovo la posizione
                else if (this.UsePosition & temPosition == null)
                    this.ClosedAll();
            }
            catch (Exception)
            {
                Core.Instance.Loggers.Log("Postion not fouded");
                throw;
            }
        }

        public void AddSl(Order order) => SlItems.Add(order);
        public void AddTp(Order order) => TpItems.Add(order);
        public void UpdateaStatus(Trade trade)
        {
            if (this.Status == PositionManagerStatus.Closed)
                return;

            if (trade.PositionImpactType == PositionImpactType.Open)
            {
                if (this.RelatedPosition == null)
                    this.CheckForPosition(trade);

                if (UsePosition)
                {
                    if(this.RelatedPosition == null)
                    {
                        this.ClosedAll();
                        return;
                    }
                }
                if (trade.OrderId == EntryOrder.Id)
                {
                    bool ramain = this.EntryOrderHistory == null ? EntryOrder.RemainingQuantity == 0 : this.EntryOrderHistory.RemainingQuantity == 0;
                    this.FilledQuantity += trade.Quantity;

                    switch (ramain)
                    {
                        case true:
                            this.Status = PositionManagerStatus.Filled;
                            break;

                        case false:
                            this.Status = PositionManagerStatus.PartialyFilled;
                            break;
                    }
                }
            }


            if (trade.PositionImpactType == PositionImpactType.Close)
            {
                if (SlItems.Any(x => x.Id == trade.OrderId))
                {
                    Order _o = SlItems.FirstOrDefault(x => x.Id == trade.OrderId);

                    this.Status = PositionManagerStatus.PartialyClosed;
                    this.ClosedQuantity += trade.Quantity;

                    this.NetProfit += trade.NetPnl.Value;
                }

                if (TpItems.Any(x => x.Id == trade.OrderId))
                {
                    Order _o = TpItems.FirstOrDefault(x => x.Id == trade.OrderId);

                    this.Status = PositionManagerStatus.PartialyClosed;
                    this.NetProfit += trade.NetPnl.Value;
                    this.ClosedQuantity += trade.Quantity;

                }

                //HINT: SOSPESO PERCHE NN LE CHIUDE TUTTE UTILIZZO UN SEMPLICE POSITION CHECK CHE SARA INFLUENZATO DA TRADE ESTERNI ALLA STAREGIA
                if (this.FilledQuantity > 0 & !UsePosition)
                    if (this.ClosedQuantity >= this.FilledQuantity)
                        while(this.Status != PositionManagerStatus.Closed)
                            this.ClosedAll();

            }
        }

        public void UpdateOrder(OrderHistory history)
        {
            if (this.EntryOrder.Id == history.Id)
            {
                this.EntryOrderHistory = history;
            }

            if (this.SlItems.Any(x => x.Id == history.Id))
            {
                if (history.Status == OrderStatus.Filled)
                    return;
                var obj = Core.Instance.GetOrderById(history.Id);
                var idx = this.SlItems.IndexOf(obj);
                this.SlItems[idx] = obj;
            }
            
            if (this.TpItems.Any(x => x.Id == history.Id))
            {
                if (history.Status == OrderStatus.Filled)
                    return;
                var obj_1 = Core.Instance.GetOrderById(history.Id);
                var idx2 = this.TpItems.IndexOf(obj_1);
                this.TpItems[idx2] = obj_1;
            }
        }
        public void ClosedAll()
        {
            this.Status = PositionManagerStatus.Closed;

            try
            {
                var orders = Core.Instance.Orders.Where(x => x.Comment == this.Id || x.Comment == "Order is null");
                foreach ( var order in orders)
                {
                    var resoult = Core.Instance.CancelOrder(order);

                    if (resoult.Status == TradingOperationResultStatus.Failure)
                    {
                        var cacca = "cacca";
                    }
                }


                #region Deprecated
                //TODO: manca una verifica
                //Core.Instance.CancelOrder(this.EntryOrder);

                //try
                //{
                //    foreach (Order order in SlItems)
                //    {
                //        var r = Core.Instance.CancelOrder(order);
                //    }
                //}
                //catch (Exception)
                //{
                //    this.DeepOrderCanceling(this.SlItems);
                //}

                //try
                //{
                //    foreach (Order order in TpItems)
                //    {
                //        var y = Core.Instance.CancelOrder(order);
                //    }
                //}
                //catch (Exception)
                //{
                //    this.DeepOrderCanceling(this.TpItems);
                //}
                #endregion
            }
            catch (Exception ex)
            {

                Core.Instance.Loggers.Log(ex.Message, LoggingLevel.Trading);
            }
            finally
            {
                TpItems.Clear();
                SlItems.Clear();
            }


        }
        public void AddTemporarySl(string orderId) => this.UnAddedSl.Add(orderId);
        public void AddTemporaryTp(string orderId) => this.UnAddedTp.Add(orderId);
        public void ConverTemIdIntOrder(Order order)
        {
            if (this.UnAddedTp.Contains(order.Id))
            {
                this.TpItems.Add(order);
                this.UnAddedTp.Remove(order.Id);
            }
            else if (this.TpItems.Any(x => x.Id == order.Id))
            {
                var _or = this.TpItems.FirstOrDefault(x => x.Id == order.Id);
                var idx = this.TpItems.IndexOf(_or);
                this.TpItems[idx] = order;
            }

            if (this.UnAddedSl.Contains(order.Id))
            {
                this.SlItems.Add(order);
                this.UnAddedSl.Remove(order.Id);
            }
            else if (this.SlItems.Any(x => x.Id == order.Id))
            {
                var _or = this.SlItems.FirstOrDefault(x => x.Id == order.Id);
                var idx = this.SlItems.IndexOf(_or);
                this.SlItems[idx] = order;
            }
        }
        private void DeepOrderCanceling(List<Order> orders)
        {
            foreach (Order order in orders)
            {

                try
                {
                    Order _o = Core.Instance.Orders.Where(x => x.Account == order.Account & x.Symbol == order.Symbol
                             & x.Side == order.Side & x.RemainingQuantity == order.RemainingQuantity & x.Price == order.Price & x.AdditionalInfo == order.AdditionalInfo).FirstOrDefault();

                    var resoult = Core.Instance.CancelOrder(order);
                }
                catch (Exception ex)
                {

                    Core.Instance.Loggers.Log($"Failed to cancel reamain{ex.Message}");
                }

            }
        }
    }
}
