using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv.DDDCore
{
    public class TpSlItemPosition : ITpSlItems
    {
        public double ClosedQuantity
        {
            get
            {
                if (this.Position != null && this.EntryOrder != null)
                    return this.EntryOrder.FilledQuantity - this.Position.Quantity;
                else
                    return 0;
            }
        }

        public Order EntryOrder { get; private set; }

        public List<Trade> EntryTrades { get; private set; }

        public List<Trade> ExitTrades{ get; private set; }

        public bool Exposed => this.Position != null && this.Position.Quantity != 0;

        public double ExposedQuantity
        {
            get
            {
                if (this.Position != null)
                    return Math.Abs(this.Position.Quantity);
                else
                    return 0;
            }
        }

        public double Fees => this.Position != null ? this.Position.Fee.Value : 0;

        public double FilledQuantity
        {
            get 
            { 
                return this.EntryOrder != null ? Core.Instance.Orders.FirstOrDefault(x => x.Id == this.EntryOrder.Id).FilledQuantity : 
                    this.Position != null && Math.Abs(this.Position.Quantity) > 0 ? Math.Abs(this.Position.Quantity) : 0;
            }
        }

        public double GrossProfit
        {
            get
            {
                if (this.Position != null)
                    return this.Position.GrossPnL.Value;
                else
                    return 0;
            }
        }

        public string Id { get; private set; }

        public double NetProfit
        {
            get
            {

                #region 🧪 HACK [Soluzione temporanea]
                // evito i NetPnl nulli tentando di capire se sono loro a lanciare un eccezzione che cancella gli ordini
                #endregion

                if (this.Position != null)
                   return this.Position.NetPnL != null ? this.Position.NetPnL.Value : 0;
                else
                    return 0;
            }
        }

        public Position Position { get; private set; }

        public double Quantity { get; private set; }

        public Side Side { get; private set; } 

        private PositionManagerStatus _status = PositionManagerStatus.Created;

        public void SetEntryOrder(Order order)
        {
            this.EntryOrder = order;
            this.Quantity = order.TotalQuantity;
            this.Side = order.Side;
        }

        public void SetPosition(Position position)
        {
            this.Position = position;
        }


        #region 🐞 BUG [Bug noto da risolvere #6]
        //Lo stato non puo aggiornarsi correttamente perche usa proprieta nn tracciate 
        #endregion

        public PositionManagerStatus Status
        {
            get
            {
                if (this.EntryOrder != null && this.Position == null)
                    return PositionManagerStatus.Placed;
                else if (this.EntryOrder != null && this.Position != null)
                {
                    var pos = Core.Instance.Positions.FirstOrDefault(x => x.Id == this.Position.Id);
                    if (pos != null && pos.Quantity != 0)
                    {
                        this.Position = pos;
                        switch (Core.Instance.Orders.Any(x => x.Id == this.EntryOrder.Id))
                        {
                            case true:
                                this.EntryOrder = Core.Instance.Orders.FirstOrDefault(x => x.Id == this.EntryOrder.Id);
                                return PositionManagerStatus.PartialyFilled;
                            case false:
                                if (pos.Quantity < this.EntryOrder.TotalQuantity)
                                    return PositionManagerStatus.PartialyClosed;
                                else
                                    return PositionManagerStatus.Filled;
                        }
                    }
                    else
                        return PositionManagerStatus.Closed;
                }
                else
                    return PositionManagerStatus.Created;
            }
        }

        public Symbol Symbol { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public event EventHandler<PositionManagerStatus[]> ItemClosed;
        public event EventHandler QuitAll;

        public TpSlItemPosition(string id)
        {
            this.Id = id;

            EntryTrades = new List<Trade>();
            ExitTrades = new List<Trade>();

        }

        public void Quit()
        {
           if (Core.Instance.Positions.Any(x => x.Id == this.Position.Id))
                Core.Instance.Positions.FirstOrDefault(x => x.Id == this.Position.Id).Close();
            else 
                Core.Instance.Loggers.Log($"PositionManager {Id} tried to close a position that is not present anymore in the account", LoggingLevel.Error);

           if (Core.Instance.Orders.Any(x => x.Symbol == this.EntryOrder.Symbol && x.Account == this.EntryOrder.Account))
           {
                var ordersToCancel = Core.Instance.Orders.Where(x => x.Symbol == this.EntryOrder.Symbol && x.Account == this.EntryOrder.Account).ToList();
                foreach (var order in ordersToCancel)
                    order.Cancel();
           }
           else
                Core.Instance.Loggers.Log($"PositionManager {Id} tried to cancel orders that are not present anymore in the account", LoggingLevel.Error);
        }

        public void TryUpdateStatus(bool force = false)
        {
            var oldStatus = _status;
            _status = this.Status;


            if (oldStatus != _status || force)
            {
                Core.Instance.Loggers.Log($"PositionManager {Id} status changed to {_status}", LoggingLevel.Trading);
                if (_status == PositionManagerStatus.Closed || force)
                {
                    this.Quit();
                    ItemClosed?.Invoke(this, new PositionManagerStatus[2] { oldStatus, _status });
                }

            }
        }

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

        public TpSlItems2 TryUpdateTrade(Trade trade)
        {
            throw new NotImplementedException();
        }

        public void UpdateSlOrders(Func<double, double> updateFunction)
        {
            throw new NotImplementedException();
        }

        public void UpdateTpOrders(Func<double, double> updateFunction)
        {
            throw new NotImplementedException();
        }
    }
}
