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
        public PositionManagerStatus Status { get; set; }
        public string Id { get; set; }
        public Order EntryOrder { get; set; }
        public List<Order> SlItems { get; set; }
        public List<Order> TpItems { get; set; }
        public string Comment { get; set; }
        public double NetProfit { get; set; }
        private List<string> UnAddedSl;
        private List<string> UnAddedTp;
        public double ClosedQuantity{ get; set; } = 0;
        //TODO: scenario partialy filled missing
        public double Quantity => this.EntryOrder.TotalQuantity;
        private double FilledQuantity = 0;
        public Side Side => this.EntryOrder.Side;
        public double EntryPrice => this.EntryOrder.Price;

        public SlTpItems(Order order, string guid, string comment = "")
        {
            this.Status = PositionManagerStatus.Placed;
            this.Id = guid;
            this.EntryOrder = order;
            this.SlItems = new List<Order>();
            this.TpItems = new List<Order>();
            this.Comment = comment;
            this.NetProfit = 0;
            this.ClosedQuantity = 0;
            this.UnAddedSl = new List<string>();
            this.UnAddedTp = new List<string>();
        }

        public void AddSl(Order order) => SlItems.Add(order);
        public void AddTp(Order order) => TpItems.Add(order);
        public void UpdateaStatus(Trade trade)
        {
            if (this.Status == PositionManagerStatus.Closed)
                return;

            if (trade.OrderId == EntryOrder.Id)
            {
                bool ramain = EntryOrder.RemainingQuantity == 0;
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

            if (trade.PositionImpactType == PositionImpactType.Close)
            {
                if (SlItems.Any(x => x.Id ==  trade.OrderId))
                {
                    Order _o = SlItems.FirstOrDefault(x => x.Id == trade.OrderId);

                    this.Status = PositionManagerStatus.PartialyClosed;
                    this.ClosedQuantity += trade.Quantity;

                    this.NetProfit += trade.NetPnl.Value;
                }
                
                if (TpItems.Any(x => x.Id ==  trade.OrderId))
                {
                    Order _o = TpItems.FirstOrDefault(x => x.Id == trade.OrderId);

                    this.Status = PositionManagerStatus.PartialyClosed;
                    this.NetProfit += trade.NetPnl.Value;
                    this.ClosedQuantity += trade.Quantity;

                }

                if (this.FilledQuantity > 0)
                    if (this.ClosedQuantity >= this.FilledQuantity)
                //if (this.GetClosedQtity())
                        this.ClosedAll();
            }
        }

        public void UpdateOrder(OrderHistory history)
        {
            if (this.EntryOrder.Id == history.Id)
            {
                var or = Core.Instance.GetOrderById(history.Id);
                if (or != null) 
                    this.EntryOrder = Core.Instance.GetOrderById(history.Id);
            }

            if (this.SlItems.Any(x => x.Id == history.Id))
            {
                var obj = Core.Instance.GetOrderById(history.Id);
                this.SlItems[this.SlItems.IndexOf(obj)] = obj;
            }
            
            if (this.TpItems.Any(x => x.Id == history.Id))
            {
                var obj = Core.Instance.GetOrderById(history.Id);
                this.TpItems[this.TpItems.IndexOf(obj)] = obj;
            }
        }

        private bool GetClosedQtity()
        {
            if (this.EntryOrder.FilledQuantity == 0)
                return true;

            List<Order> orders = this.SlItems.Concat(this.TpItems).ToList();
            double filledQtity = 0;
            
            foreach (var item in orders)
            {
                filledQtity += item.FilledQuantity;
            }

            if (filledQtity < this.EntryOrder.FilledQuantity)
            {
                this.ClosedQuantity = filledQtity;
                return false;
            }
            else
                return true;
        }

        private void ClosedAll()
        {
            this.Status = PositionManagerStatus.Closed;

            try
            {
                Core.Instance.CancelOrder(this.EntryOrder);

                try
                {
                    foreach (Order order in SlItems)
                        Core.Instance.CancelOrder(order);
                }
                catch (Exception)
                {
                    this.DeepOrderCanceling(this.SlItems);
                }

                try
                {
                    foreach (Order order in TpItems)
                        Core.Instance.CancelOrder(order);
                }
                catch (Exception)
                {
                    this.DeepOrderCanceling(this.TpItems);
                }
            }
            catch (Exception ex)
            {

                Core.Instance.Loggers.Log(ex.Message, LoggingLevel.Trading);
            }

            TpItems.Clear();
            SlItems.Clear();
           
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

                    Core.Instance.CancelOrder(order);
                }
                catch (Exception ex)
                {

                    Core.Instance.Loggers.Log($"Failed to cancel reamain{ex.Message}");
                }
                
            }
        }
    }
}
