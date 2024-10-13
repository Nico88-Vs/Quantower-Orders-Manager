// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace TpSlManager
{
   
    public static class TpSlManager<T>
    {
        public static List<SlTpItems> SlTpItems { get; private set; }
        private static List<string> UnfilledIds { get; set; }
        //private static List<Order> Orders { get; set; }
        public static SlTpCondictionHolder<T> ListOfDelegates { get; set; }
        private static List<string> FilledIds { get; set; }
        private static List<Guid> PartialiFilledIds { get; set; }
        private static Dictionary<Symbol,OrderType> OrderTypes { get; set; }
        private static int MaxShortExo;
        private static int MaxLongExo;

        public static double NetProfit { 
            get
            {
                double sum = 0;
                try
                {
                    foreach (var item in SlTpItems)
                        sum += item.NetProfit;
                }
                catch (Exception ex)
                {

                    Core.Instance.Loggers.Log(ex);
                }
                return sum;
            }
        }
        public static double LongCount
        {
            get
            {
                double sum = 0;
                try
                {
                    sum = SlTpItems.Where(x => x.Side == Side.Buy).Count();
                }
                catch (Exception ex)
                {

                    Core.Instance.Loggers.Log(ex);
                }
               
                return sum;
            }
        }
        public static double ShortCount
        {
            get
            {
                double sum = 0;
                try
                {
                    sum = SlTpItems.Where(x => x.Side == Side.Sell).Count();
                }
                catch (Exception ex)
                {

                    Core.Instance.Loggers.Log(ex);
                }
                return sum;
            }
        }
        public static int LongExpo { 
            get 
            {
                int sum = 0;
                try
                {
                    sum = SlTpItems.Where(x => x.Side == Side.Buy & x.Status != PositionManagerStatus.Closed).Count();
                }
                catch (Exception ex)
                {

                    Core.Instance.Loggers.Log(ex);
                }
                return sum;
            } 
        }
        public static int ShortExpo
        {
            get
            {
                int sum = 0;
                try
                {
                    sum = SlTpItems.Where(x => x.Side == Side.Sell & x.Status != PositionManagerStatus.Closed).Count();
                }
                catch (Exception ex)
                {

                    Core.Instance.Loggers.Log(ex);
                }
                return sum;
            }
        }

        public static void init(SlTpCondictionHolder<T> listOfDelegates, int maxshortexpo = 3, int maxlongexpo = 3)
        {
            UnfilledIds = new List<string>();
            //Orders = new List<Order>();
            OrderTypes = new Dictionary<Symbol,OrderType>();
            PartialiFilledIds = new List<Guid>();
            FilledIds = new List<string>();
            SlTpItems = new List<SlTpItems>();
            ListOfDelegates = listOfDelegates;
            MaxLongExo = maxlongexpo;
            MaxShortExo = maxshortexpo;

            // Sottoscrizione all'evento
            Core.Instance.OrderAdded += Instance_OrderAdded;
            Core.Instance.OrdersHistoryAdded += Instance_OrdersHistoryAdded;
            Core.Instance.TradeAdded += Instance_TradeAdded;
        }

        private static void Instance_TradeAdded(Trade obj)
        {
            //if (obj.PositionImpactType == PositionImpactType.Open || Orders.Any(x => x.Id == obj.OrderId))
            //{
            //    Order _or = Orders.FirstOrDefault(x => x.Id == obj.OrderId);
            //    if (SlTpItems.Any(x => x.Id == _or.Comment))
            //    {
            //        ListOfDelegates.Computator.PlaceOrder(obj, SlTpItems.FirstOrDefault(x => x.Id == _or.Comment));
            //    }
            //}

            foreach (SlTpItems item in SlTpItems)
            {
               item.UpdateaStatus(obj);
            }
        }
        private static void Instance_OrdersHistoryAdded(OrderHistory obj)
        {
            foreach (SlTpItems item in SlTpItems)
            {
                item.UpdateOrder(obj);
            }
        }

        // Metodo statico per la gestione dell'evento
        private static void Instance_OrderAdded(Order obj)
        {
            if (UnfilledIds.Contains(obj.Comment))
            {
                UnfilledIds.Remove(obj.Comment);
                SlTpItems.Add(new SlTpItems(obj, obj.Comment));
                if (SlTpItems.Any(x => x.Id == obj.Comment))
                {
                    ListOfDelegates.Computator.PlaceOrder(obj, SlTpItems.FirstOrDefault(x => x.Id == obj.Comment));
                }
                FilledIds.Add(obj.Comment);
                //Orders.Add(obj);
            }
            else
            {
                foreach (var item in SlTpItems)
                {
                    item.ConverTemIdIntOrder(obj);
                }
            }

        }

        // Metodo statico per piazzare un ordine
        public static void PlaceOrder(PlaceOrderRequestParameters reqParameters)
        {
            if (!OrderTypes.ContainsKey(reqParameters.Symbol))
            {
                var _orType = SetOrderType(reqParameters.Symbol);
                if (_orType != null)
                    OrderTypes.Add(reqParameters.Symbol, _orType);
                else
                {
                    Core.Instance.Loggers.Log($"Invalid Order type for Symbol {reqParameters.Symbol.Name}");
                    return;
                }
            }

            Guid guid = Guid.NewGuid();

            if (MaxShortExo <= ShortExpo & reqParameters.Side == Side.Sell)
                return;
            
            if (MaxLongExo <= LongExpo & reqParameters.Side == Side.Buy)
                return;

            try
            {
                PlaceOrderRequestParameters requestParameters = new PlaceOrderRequestParameters() 
                {
                    Side = reqParameters.Side,
                    Account = reqParameters.Account,
                    Symbol = reqParameters.Symbol,
                    Price = reqParameters.Price,
                    TriggerPrice = reqParameters.TriggerPrice,
                    Comment = guid.ToString(),
                    OrderTypeId = OrderTypes[reqParameters.Symbol].Id,
                    AccountId = reqParameters.AccountId,
                    CancellationToken = reqParameters.CancellationToken,
                    TimeInForce = reqParameters.TimeInForce,
                    ExpirationTime = reqParameters.ExpirationTime,
                    Quantity = reqParameters.Quantity,
                    AdditionalParameters = new List<SettingItem>
                    {
                        new SettingItemBoolean(OrderType.POST_ONLY, true)
                    }
                };

                var resoult = Core.Instance.PlaceOrder(requestParameters);

                if (resoult.Status == TradingOperationResultStatus.Failure)
                    Core.Instance.Loggers.Log($"Placing Order FAILED {resoult.Message}", LoggingLevel.Trading);
                else
                    UnfilledIds.Add(guid.ToString());

            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log($"Placing Order FAILED {ex.Message}", LoggingLevel.Error);
            }
        }

        // Metodo statico per la pulizia e rimozione dell'evento
        public static void Dispose()
        {
            Core.Instance.OrderAdded -= Instance_OrderAdded;
        }

        private static OrderType SetOrderType(Symbol symbol)
        {
            var order_type = symbol.GetAlowedOrderTypes(OrderTypeUsage.All).FirstOrDefault(x => x.Usage == OrderTypeUsage.All && x.Behavior == OrderTypeBehavior.Limit);
            if (order_type == null)
            {
                Core.Instance.Loggers.Log("Missing Valid limit order Type", LoggingLevel.Error);
                Core.Instance.Loggers.Log("Tring Stop Order Type", LoggingLevel.Error);
                order_type = symbol.GetAlowedOrderTypes(OrderTypeUsage.All).FirstOrDefault(x => x.Usage == OrderTypeUsage.All && x.Behavior == OrderTypeBehavior.Stop);
                if (order_type == null)
                {
                    Core.Instance.Loggers.Log("Missing order for this connection", LoggingLevel.Error);
                    var tipi = symbol.GetAlowedOrderTypes(OrderTypeUsage.All);
                    order_type = symbol.GetAlowedOrderTypes(OrderTypeUsage.All).FirstOrDefault(x => x.Usage == OrderTypeUsage.Order && x.Behavior == OrderTypeBehavior.Market);
                    if (order_type == null)
                    {
                        Core.Instance.Loggers.Log("Definitive : Missing order for this connection", LoggingLevel.Error);
                        return null;
                    }
                }
            }

            return order_type;
        }

        private static bool ComputateGuid(string _guid, List<Guid> _list)
        {
            List<string> _listOfStrings = new List<string>();
            foreach (Guid item in _list)
                _listOfStrings.Add(item.ToString());

            if (_listOfStrings.Contains(_guid))
                return true;
            else
                return false;
        }

        public static void Stop()
        {
            Core.Instance.OrderAdded -= Instance_OrderAdded;
            Core.Instance.OrdersHistoryAdded -= Instance_OrdersHistoryAdded;
            Core.Instance.TradeAdded -= Instance_TradeAdded;
        }
    }
}