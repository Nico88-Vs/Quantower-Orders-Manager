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
            throw new NotImplementedException();
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
