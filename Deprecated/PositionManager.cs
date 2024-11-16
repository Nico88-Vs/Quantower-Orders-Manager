using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Deprecated
{
    public enum PositionManagerStatus
    {
        Waiting,
        PartialWaiting, // Correzione di "PartialWating"
        FullyInTrade,
        OverTrade
    }

    public enum OrderImpact
    {
        Entry,
        PartialEntry,
        TP,
        PartialTp,
        SL,
        PartialSl
    }

    public static class PositionManager
    {
        #region Attributes
        private static int _shortTradeLimit;
        private static int _longTradeLimit;

        private static int _shortTradeCount = 0;
        private static int _longTradeCount = 0;

        private static double _totalQuantityPerSide;

        private static int _UniqueId;

        public static string StrategyName { get; private set; }
        public static int ShortPositionsCount { get; set; } = 0;
        public static int LongPositionsCount { get; set; } = 0;

        public static PositionManagerStatus LongStatus
        {
            get
            {
                if (_longTradeCount == 0)
                    return PositionManagerStatus.Waiting;
                if (_longTradeCount == _longTradeLimit)
                    return PositionManagerStatus.FullyInTrade;
                if (_longTradeCount > _longTradeLimit)
                {
                    Log("Overtrading Detected", LoggingLevel.Error);
                    return PositionManagerStatus.OverTrade;
                }
                return PositionManagerStatus.PartialWaiting;
            }
        }
        public static PositionManagerStatus ShortStatus
        {
            get
            {
                if (_shortTradeCount == 0)
                    return PositionManagerStatus.Waiting;
                if (_shortTradeCount == _shortTradeLimit)
                    return PositionManagerStatus.FullyInTrade;
                if (_shortTradeCount > _shortTradeLimit)
                {
                    Log("Overtrading Detected", LoggingLevel.Error);
                    return PositionManagerStatus.OverTrade;
                }
                return PositionManagerStatus.PartialWaiting;
            }
        }

        public static List<Order> currentLongOrder { get; set; }
        public static List<Order> currentShortOrder { get; set; }
        public static Position currentLongPosition { get; set; }
        public static Position currentShortPosition { get; set; }
        public static Account _Account { get; set; }
        public static Symbol _Symbol { get; set; }

        private static List<PlaceOrderRequestParameters> _TempShortOrders;
        private static List<PlaceOrderRequestParameters> _TempLongOrders;
        #endregion

        #region Init
        public static void Init(string strategyName, double totalQuantitSide, Symbol symbol, Account account, int shortOrderLimit = 3, int longOrderLimit = 3)
        {
            _UniqueId = 0;
            StrategyName = strategyName;
            _totalQuantityPerSide = totalQuantitSide;

            _longTradeLimit = longOrderLimit;
            _shortTradeLimit = shortOrderLimit;

            _TempShortOrders = new List<PlaceOrderRequestParameters>();
            _TempLongOrders = new List<PlaceOrderRequestParameters>();

            currentLongOrder = new List<Order>();
            currentShortOrder = new List<Order>();

            _Account = account;
            _Symbol = symbol;

            Core.Instance.PositionRemoved += Instance_PositionRemoved;
            Core.Instance.OrderAdded += Instance_OrderAdded;
            Core.Instance.TradeAdded += Instance_TradeAdded;
            Core.Instance.OrderRemoved += Instance_OrderRemoved;
            Core.Instance.OrdersHistoryAdded += Instance_OrdersHistoryAdded;
        }

        private static void Instance_OrdersHistoryAdded(OrderHistory obj)
        {
            var y = obj.State;
        }
        #endregion

        #region Lifecycle
        public static void Stop()
        {
            Core.Instance.PositionRemoved -= Instance_PositionRemoved;
            Core.Instance.OrderAdded -= Instance_OrderAdded;
            Core.Instance.TradeAdded -= Instance_TradeAdded;
        }

        private static void Instance_OrderRemoved(Order obj)
        {
            switch (obj.Side)
            {
                case Side.Buy:
                    if (currentLongOrder.Contains(obj) & obj.RemainingQuantity == 0)
                    {
                        //currentLongOrder.Remove(obj);
                        _longTradeCount--;
                    }
                    break;
                case Side.Sell:
                    if (currentShortOrder.Contains(obj) & obj.RemainingQuantity == 0)
                    {
                        //currentShortOrder.Remove(obj);
                        _shortTradeCount--;
                    }
                    break;
            }
        }
        #endregion

        #region Events
        private static void Instance_PositionRemoved(Position obj)
        {
            //switch (obj.Side)
            //{
            //    case Side.Buy:
            //        if (obj == currentLongPosition)
            //        {
            //            _longTradeCount = 0;
            //            currentLongPosition = null;
            //            LongPositionsCount++;
            //        }
            //        break;
            //    case Side.Sell:
            //        if (obj == currentShortPosition)
            //        {
            //            _shortTradeCount = 0;
            //            currentShortPosition = null;
            //            ShortPositionsCount++;
            //        }
            //        break;
            //}
        }

        private static void Instance_TradeAdded(Trade obj)
        {
            //Order temp_or = Core.Instance.Orders.FirstOrDefault(x => x.Id == obj.OrderId);

            //if (obj.PositionImpactType == PositionImpactType.Open)
            //{
            //    if (temp_or.Comment != "new trade")
            //        return;

            //    if (obj.Side == Side.Buy)
            //    {
            //        if (!currentLongOrder.Contains(temp_or))
            //        {
            //            currentLongOrder.Add(temp_or);
            //            FindPosition(temp_or, Side.Buy);
            //        }

            //    }
            //    else
            //    {
            //        if (!currentShortOrder.Contains(temp_or))
            //        {
            //            currentShortOrder.Add(temp_or);
            //            FindPosition(temp_or, Side.Sell);
            //        }
            //    }
            //}
            //else if (obj.PositionImpactType == PositionImpactType.Close)
            //{
            //    Side s = temp_or.Side;
            //    bool contained = false;

            //    switch (s)
            //    {
            //        case Side.Buy:
            //            contained = currentLongOrder.Contains(temp_or);
            //            break;
            //        case Side.Sell:
            //            contained = currentShortOrder.Contains(temp_or);
            //            break;
            //    }

            //    if (contained)
            //    {
            //        switch (s)
            //        {
            //            case Side.Buy:
            //                _longTradeCount--;
            //                break;
            //            case Side.Sell:
            //                _shortTradeCount--;
            //                break;
            //        }
            //    }
            //    else
            //        Log($"Failed to find Position", LoggingLevel.Error);
            //}
        }

        private static void Instance_OrderAdded(Order obj)
        {
            //TODO: comment hard coded
            if (obj.Comment == "new order")
            {
                switch (obj.Side)
                {
                    case Side.Buy:
                        if (!currentLongOrder.Contains(obj))
                        {
                            currentLongOrder.Add(obj);
                            _longTradeCount++;
                        }
                        break;
                    case Side.Sell:
                        if (!currentLongOrder.Contains(obj))
                        {
                            currentLongOrder.Add(obj);
                            _shortTradeCount++;
                        }

                        break;
                }
            }
        }
        #endregion

        #region Main Methods
        private static void AddTemporaryOrder(PlaceOrderRequestParameters requestParameters)
        {
            switch (requestParameters.Side)
            {
                case Side.Buy:
                    _TempLongOrders.Add(requestParameters);
                    break;
                case Side.Sell:
                    _TempShortOrders.Add(requestParameters);
                    break;
            }
        }

        public static void PlaceTempOrder(string Comment, Side side)
        {
            switch (side)
            {
                case Side.Buy:
                    if (LongStatus == PositionManagerStatus.FullyInTrade)
                        return;
                    break;
                case Side.Sell:
                    if (ShortStatus == PositionManagerStatus.FullyInTrade)
                        return;
                    break;
            }

            int idx = -1;
            bool proceed = true;
            try
            {
                switch (side)
                {
                    case Side.Buy:
                        idx = _TempLongOrders.IndexOf(_TempLongOrders.FirstOrDefault(x => x.Comment == Comment));
                        break;
                    case Side.Sell:
                        idx = _TempShortOrders.IndexOf(_TempShortOrders.FirstOrDefault(x => x.Comment == Comment));
                        break;
                }
            }
            catch (Exception ex)
            {
                proceed = false;
                Log($"Failed to find temp order at side {side.ToString()} with error {ex.Message}", LoggingLevel.Error);
            }
            finally
            {
                if (proceed && idx >= 0)
                {
                    TradingOperationResultStatus res = TradingOperationResultStatus.Failure;
                    switch (side)
                    {
                        case Side.Buy:
                            res = Core.Instance.PlaceOrder(_TempLongOrders.ElementAt(idx)).Status;
                            break;
                        case Side.Sell:
                            res = Core.Instance.PlaceOrder(_TempShortOrders.ElementAt(idx)).Status;
                            break;
                    }

                    if (res == TradingOperationResultStatus.Failure)
                        Log("Failed to place order", LoggingLevel.Trading);
                    else
                        Log($"{side.ToString()} Order Placed", LoggingLevel.Trading);
                }
            }
        }

        public static void RemoveTempOrder(Order order, string expectedComment)
        {
            try
            {
                Core.Instance.Orders.Where(x => x == order).First(s => s.Comment == expectedComment).Cancel();
            }
            catch (Exception ex)
            {
                Log($"Failed to remove order with error {ex.Message}", LoggingLevel.Error);
            }
        }
        #endregion

        #region Utils
        private static void Log(string message, LoggingLevel logLevel) => Core.Instance.Loggers.Log(message, logLevel);

        private static List<PlaceOrderRequestParameters> GenerateSlTp(double slPrice, double tPrice, PlaceOrderRequestParameters order)
        {
            //TODO: make dinamic
            //TODO: enum order impact tipe unused

            var sl = new PlaceOrderRequestParameters()
            {
                Account = order.Account,
                Symbol = order.Symbol,
                Side = order.Side == Side.Buy ? Side.Sell : Side.Buy,
                Quantity = order.Quantity,
                OrderTypeId = _Symbol.GetAlowedOrderTypes(OrderTypeUsage.All).FirstOrDefault(x => x.Usage == OrderTypeUsage.All && x.Behavior == OrderTypeBehavior.Stop).Id,
                TimeInForce = TimeInForce.GTC,
                Price = slPrice,
                Comment = order.Comment,
                AdditionalParameters = new List<SettingItem>()
                {
                    new SettingItemBoolean(OrderType.REDUCE_ONLY, true),
                    new SettingItemString(name: "Order Impact Type", value: Convert.ToString(OrderImpact.SL)),
                    new SettingItemInteger(name: "Unique Session Id", value: (int)order.AdditionalParameters.First(x => x.Name == "Unique Session Id").Value),
                }
            };

            var tp = new PlaceOrderRequestParameters()
            {
                Account = order.Account,
                Symbol = order.Symbol,
                Side = order.Side == Side.Buy ? Side.Sell : Side.Buy,
                Quantity = order.Quantity,
                OrderTypeId = _Symbol.GetAlowedOrderTypes(OrderTypeUsage.All).FirstOrDefault(x => x.Usage == OrderTypeUsage.All && x.Behavior == OrderTypeBehavior.Stop).Id,
                TimeInForce = TimeInForce.GTC,
                Price = tPrice,
                Comment = order.Comment,
                AdditionalParameters = new List<SettingItem>()
                {
                    new SettingItemBoolean(OrderType.REDUCE_ONLY, true),
                    new SettingItemString(name: "Order Impact Type", value: Convert.ToString(OrderImpact.TP)),
                    new SettingItemInteger(name: "Unique Session Id", value: (int)order.AdditionalParameters.First(x => x.Name == "Unique Session Id").Value),
                }
            };

            return new List<PlaceOrderRequestParameters>() { sl, tp };

        }

        public static void CreateRequest(Side side, double entryPrice, double sl, double tp, bool place_it = true)
        {
            string comment = $"{StrategyName}";
            double quantity = side == Side.Buy ? _totalQuantityPerSide / _longTradeLimit : _totalQuantityPerSide / _shortTradeCount;

            List<SettingItem> orderSetting = new List<SettingItem>()
            {
                new SettingItemString(name : "Order Impact Type", value: Convert.ToString(OrderImpact.Entry)),
                new SettingItemInteger(name : "Unique Session Id", value: _UniqueId)
            };

            _UniqueId++;

            var placeHoldeReq = new PlaceOrderRequestParameters()
            {
                Account = _Account,
                Symbol = _Symbol,
                Side = side,
                Quantity = RoundQuantity(quantity),
                OrderTypeId = _Symbol.GetAlowedOrderTypes(OrderTypeUsage.All).FirstOrDefault(x => x.Usage == OrderTypeUsage.All && x.Behavior == OrderTypeBehavior.Limit).Id,
                TimeInForce = TimeInForce.Day,
                Price = entryPrice,
                Comment = comment,
                AdditionalParameters = orderSetting
            };

            AddTemporaryOrder(placeHoldeReq);

            if (place_it)
                PlaceTempOrder(placeHoldeReq.Comment, placeHoldeReq.Side);
        }

        private static double RoundQuantity(double quantity)
        {
            return Math.Round(quantity / _Symbol.MinLot) * _Symbol.MinLot;
        }

        private static void FindPosition(Order order, Side side)
        {
            try
            {
                Position p = Core.Instance.Positions.First(x => x.ConnectionId == order.ConnectionId && x.Side == order.Side
                && x.Symbol.Id == order.Symbol.Id && x.OpenPrice == order.AverageFillPrice);

                switch (side)
                {
                    case Side.Buy:
                        currentLongPosition = p;
                        break;
                    case Side.Sell:
                        currentShortPosition = p;
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"Missing Position for order {order.Id} with error {ex.Message}", LoggingLevel.Trading);
            }
        }
        #endregion
    }
}
