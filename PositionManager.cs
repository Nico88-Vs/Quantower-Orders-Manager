using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1
{
    public enum PositionManagerStatus
    {
        Waiting,
        PartialWating,
        FullyInTrade,
        OverTrade
    }
   
    public class PositionManager
    {
        #region Attributes
        private int _shortTradeLimit;
        private int _longTradeLimit;
        
        private int _shortTradeCount = 0;
        private int _longTradeCount = 0;

        public int ShortPositionsCount { get; set; } = 0;
        public int LongPositionsCount { get; set; } = 0;

        public PositionManagerStatus LongStatus 
        { 
            get 
            {
                if (this._longTradeCount == 0)
                    return PositionManagerStatus.Waiting;
                if (this._longTradeCount == this._longTradeLimit)
                    return PositionManagerStatus.FullyInTrade;
                if (this._longTradeCount > this._longTradeLimit)
                {
                    this.Log("Overtrading Detected", LoggingLevel.Error);
                    return PositionManagerStatus.OverTrade;
                }
                else
                    return PositionManagerStatus.PartialWating;
            }   
        }
        public PositionManagerStatus ShortStatus
        {
            get
            {
                if (this._shortTradeCount == 0)
                    return PositionManagerStatus.Waiting;
                if (this._shortTradeCount == this._shortTradeLimit)
                    return PositionManagerStatus.FullyInTrade;
                if (this._shortTradeCount > this._shortTradeLimit)
                {
                    this.Log("Overtrading Detected", LoggingLevel.Error);
                    return PositionManagerStatus.OverTrade;
                }
                else
                    return PositionManagerStatus.PartialWating;
            }
        }
        public List<Order> currentLongOrder { get; set; }
        public List<Order> currentShortOrder { get; set; }
        public Position currentLongPosition { get; set; }
        public Position currentShortPosition { get; set; }

        private List<PlaceOrderRequestParameters> _TempShortOrders;
        private List<PlaceOrderRequestParameters> _TempLongOrders;
        #endregion

        #region Lifecycle
        public PositionManager(int shortOrderLimit = 3, int longOrderLimit=3)
        {
            this._longTradeLimit = longOrderLimit;
            this._shortTradeLimit = shortOrderLimit;

            this._TempShortOrders = new List<PlaceOrderRequestParameters>();
            this._TempLongOrders = new List<PlaceOrderRequestParameters>();

            this.currentLongOrder = new List<Order>();
            this.currentShortOrder = new List<Order>();

            Core.Instance.PositionRemoved += this.Instance_PositionRemoved;
            Core.Instance.OrderAdded += this.Instance_OrderAdded;
            Core.Instance.TradeAdded += this.Instance_TradeAdded;
            Core.Instance.OrderRemoved += this.Instance_OrderRemoved;
        }

        private void Instance_OrderRemoved(Order obj)
        {
            switch (obj.Side)
            {
                case Side.Buy:
                    if (currentLongOrder.Contains(obj))
                    {
                        this.currentLongOrder.Remove(obj);
                        this._longTradeCount--;
                    }
                    break;
                case Side.Sell:
                    if (currentShortOrder.Contains(obj))
                    {
                        this.currentShortOrder.Remove(obj);
                        this._shortTradeCount--;
                    }
                    break;
            }
        }

        public void Stop()
        {
            Core.Instance.PositionRemoved -= this.Instance_PositionRemoved;
            Core.Instance.OrderAdded -= this.Instance_OrderAdded;
            Core.Instance.TradeAdded -= this.Instance_TradeAdded;
        }
        #endregion

        #region Events
        private void Instance_PositionRemoved(Position obj)
        {
            switch (obj.Side)
            {
                case Side.Buy:
                    if (obj == this.currentLongPosition)
                    {
                        this._longTradeCount = 0;
                        this.currentLongPosition = null;
                        this.LongPositionsCount++;
                    }
                    break;
                case Side.Sell:
                    if (obj == this.currentShortPosition)
                    {
                        this._shortTradeCount = 0;
                        this.currentShortPosition = null;
                        this.ShortPositionsCount++;
                    }
                    break;
            }
            
        }
        private void Instance_TradeAdded(Trade obj)
        {
            Order temp_or = Core.Instance.Orders.FirstOrDefault(x => x.Id == obj.Id);

            if (obj.PositionImpactType == PositionImpactType.Open)
            {
                if (temp_or.Comment != "new trade")
                    return;

                if (obj.Side == Side.Buy)
                {
                    if (!this.currentLongOrder.Contains(temp_or))
                    {
                        this.currentLongOrder.Add(temp_or);
                        this.FindPosition(temp_or, Side.Buy);
                    }

                }
                else
                {
                    if (!this.currentShortOrder.Contains(temp_or))
                    {
                        this.currentShortOrder.Add(temp_or);
                        this.FindPosition(temp_or, Side.Sell);
                    }
                }
            }
            else if (obj.PositionImpactType == PositionImpactType.Close)
            {
                Side s = temp_or.Side;
                bool contained = false;

                switch (s)
                {
                    case Side.Buy:
                        contained = this.currentLongOrder.Contains(temp_or);
                        break;
                    case Side.Sell:
                        contained = this.currentShortOrder.Contains(temp_or);
                        break;
                }

                if (contained)
                {
                    switch (s)
                    {
                        case Side.Buy:
                            this._longTradeCount--;
                            break;
                        case Side.Sell:
                            this._shortTradeCount--;
                            break;
                    }
                }
                else
                    this.Log($"Failed to find Position", LoggingLevel.Error);
            }
        }
        private void Instance_OrderAdded(Order obj)
        {
            if (obj.Comment == "new order")
                switch (obj.Side)
                {
                    case Side.Buy:
                        this._longTradeCount++;
                        break;
                    case Side.Sell:
                        this._shortTradeCount++;
                        break;
                }
        }
        #endregion

        #region Main Methods
        public void AddTemporaryOrder(Side side, PlaceOrderRequestParameters requestParameters, string comment)
        {
            var resoult = requestParameters;
            resoult.Comment = comment;

            switch (side)
            {
                case Side.Buy:
                    this._TempLongOrders.Add(resoult);
                    break;
                case Side.Sell:
                    this._TempShortOrders.Add(resoult);
                    break;
            }
        }

        public void PlaceTempOrder(string Comment, Side side)
        {
            switch (side)
            {
                case Side.Buy:
                    if (this.LongStatus == PositionManagerStatus.FullyInTrade)
                        return;
                    break;
                case Side.Sell:
                    if (this.ShortStatus == PositionManagerStatus.FullyInTrade)
                        return;
                    break;
            }
            int idx = -1;
            bool proced = true;
            try
            {
                switch (side)
                {
                    case Side.Buy:
                        idx = this._TempLongOrders.IndexOf(_TempLongOrders.FirstOrDefault(x => x.Comment == Comment));
                        break;
                    case Side.Sell:
                        idx = this._TempShortOrders.IndexOf(_TempShortOrders.FirstOrDefault(x => x.Comment == Comment));
                        break;
                }
            }
            catch (Exception ex)
            {
                proced = false;
                this.Log($"Failed to find temp order at side {side.ToString()} with error {ex.Message}", LoggingLevel.Error);
            }
            finally
            {
                if (proced)
                {
                    TradingOperationResultStatus res = TradingOperationResultStatus.Failure;
                    switch (side)
                    {
                        case Side.Buy:
                            res = Core.Instance.PlaceOrder(this._TempLongOrders.ElementAt(idx)).Status;
                            break;
                        case Side.Sell:
                            res = Core.Instance.PlaceOrder(this._TempShortOrders.ElementAt(idx)).Status;
                            break;
                    }

                    if (res == TradingOperationResultStatus.Failure)
                        this.Log("Failed to place order", logleve: LoggingLevel.Trading);
                    else
                        this.Log($"{side.ToString()} Order Placed ", logleve: LoggingLevel.Trading);
                }
            }
        }
        public void RemoveTempOrder(Order order, string aspettedComment)
        {
            try
            {
                Core.Instance.Orders.Where(x => x == order ).First(s => s.Comment == aspettedComment).Cancel();
            }
            catch (Exception ex)
            {

                this.Log($"Failed to remove order with error {ex.Message}", LoggingLevel.Error);
            }
        }
        #endregion

        #region Utils
        private void Log(string message, LoggingLevel logleve) => Core.Instance.Loggers.Log(message, logleve);
        private void FindPosition(Order order, Side side)
        {
            try
            {
                Position p = Core.Instance.Positions.First(x => x.ConnectionId == order.ConnectionId & x.Side == order.Side
                & x.Symbol.Id == order.Symbol.Id & x.OpenPrice == order.AverageFillPrice);

                switch (side)
                {
                    case Side.Buy:
                        this.currentLongPosition = p;
                        break;
                    case Side.Sell:
                        this.currentShortPosition = p;
                        break;
                }
            }
            catch (Exception ex)
            {
                this.Log($"Missing Position for order {order.Id} with error {ex.Message}", logleve:LoggingLevel.Trading);
            }
        }
        #endregion
    }

}
