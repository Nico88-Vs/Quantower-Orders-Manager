using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using TradingPlatform.BusinessLayer;

namespace TpSlManager
{
    public class TpSlComputator<T>
    {
        private SlTpCondictionHolder<T> ListOfDelegates;
        public delegate double UpdateOrderDelegate(Object obj);
        private int tp_items;
        private int sl_items;

        public TpSlComputator(SlTpCondictionHolder<T> listOfDelegates)
        {
            this.ListOfDelegates = listOfDelegates;
            this.tp_items = listOfDelegates.TpDelegateObj.Length;
            this.sl_items = listOfDelegates.SlDelegateObj.Length;
        }

        public void PlaceOrder(Order _order, SlTpItems items)
        {
            //TODO: market Ordere only
            //TODO: probably using total quantity multiple times
            var _slOrdereType = _order.Symbol.GetAlowedOrderTypes(OrderTypeUsage.All);
            var side = _order.Side == Side.Buy ? Side.Sell : Side.Buy;

            for (var i = 0; i < sl_items; i++)
            {
                PlaceOrderRequestParameters _sl = new PlaceOrderRequestParameters()
                {
                    Account = _order.Account,
                    Symbol = _order.Symbol,
                    Price = this.ListOfDelegates.SlDelegate[i](this.ListOfDelegates.SlDelegateObj[i], items.Id),
                    TriggerPrice = this.ListOfDelegates.SlDelegate[i](this.ListOfDelegates.SlDelegateObj[i], items.Id),
                    Comment = _order.Comment != null ? _order.Comment : "Order is null",
                    OrderTypeId = _slOrdereType.FirstOrDefault(x => x.Behavior == OrderTypeBehavior.Stop).Id,
                    AdditionalParameters = new List<SettingItem>
                    {
                        new SettingItemBoolean(OrderType.REDUCE_ONLY, true)
                    },
                    TimeInForce = TimeInForce.GTC,
                    Quantity = this.RoundQuantity(_order.TotalQuantity / this.sl_items, _order.Symbol),
                    Side = side,
                };

                var resoult = Core.Instance.PlaceOrder(_sl);

                if (resoult.Status != TradingOperationResultStatus.Success)
                {
                    Core.Instance.Loggers.Log("Error placing sl ", LoggingLevel.Trading);
                }
                else
                    items.AddTemporarySl(resoult.OrderId);
            }

            for (var i = 0; i < tp_items; i++)
            {

                PlaceOrderRequestParameters _tp = new PlaceOrderRequestParameters()
                {
                    Account = _order.Account,
                    Symbol = _order.Symbol,
                    Price = this.ListOfDelegates.TpDelegate[i](this.ListOfDelegates.TpDelegateObj[i], items.Id),
                    TriggerPrice = this.ListOfDelegates.TpDelegate[i](this.ListOfDelegates.TpDelegateObj[i], items.Id),
                    Comment = _order != null ? _order.Comment : "Order is null",
                    OrderTypeId = _slOrdereType.FirstOrDefault(x => x.Behavior == OrderTypeBehavior.Limit).Id,
                    AdditionalParameters = new List<SettingItem>
                    {
                        new SettingItemBoolean(OrderType.REDUCE_ONLY, true)
                    },
                    TimeInForce = TimeInForce.GTC,
                    Quantity = this.RoundQuantity(_order.TotalQuantity / this.tp_items, _order.Symbol),
                    Side = side,
                };

                var tp_resoult = Core.Instance.PlaceOrder(_tp);

                if (tp_resoult.Status != TradingOperationResultStatus.Success)
                {
                    Core.Instance.Loggers.Log("Error placing tp", LoggingLevel.Trading);
                }
                else
                    items.AddTemporaryTp(tp_resoult.OrderId);
            }

        }
        public void UpdateOrder(UpdateOrderDelegate updateDelegaate, Object delegateParameter,  Order order)
        {
            try
            {
                var request = new ModifyOrderRequestParameters(order);

                request.Price = updateDelegaate(delegateParameter);
                request.AdditionalParameters = new List<SettingItem>
                            {
                                new SettingItemBoolean(OrderType.REDUCE_ONLY, true)
                            };
                var resoult = Core.Instance.ModifyOrder(request);

                if (resoult.Status == TradingOperationResultStatus.Failure)
                    Core.Instance.Loggers.Log("Error modifing sl or tp", LoggingLevel.Trading);

            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log(ex.Message, LoggingLevel.Error);
            }
        }
        public void UpdateOrder(List<SlTpItems> items)
        {
            foreach (SlTpItems item in items)
            {
                try
                {
                    if (item.Status != TpSlManager.PositionManagerStatus.Placed & item.Status != TpSlManager.PositionManagerStatus.Closed)
                    {
                        for (int i = 0; i < this.sl_items; i++)
                        {
                            if (item.SlItems.Count != this.tp_items)
                            {
                                Core.Instance.Loggers.Log("Unmaching Orders Items", LoggingLevel.Error);
                                return;
                            }
                            var request = new ModifyOrderRequestParameters(item.SlItems[i]);

                            request.Price = this.ListOfDelegates.SlDelegate[i](this.ListOfDelegates.SlDelegateObj[i], item.Id);
                            request.AdditionalParameters = new List<SettingItem>
                            {
                                new SettingItemBoolean(OrderType.REDUCE_ONLY, true)
                            };
                            var resoult = Core.Instance.ModifyOrder(request);

                            if (resoult.Status == TradingOperationResultStatus.Failure)
                                Core.Instance.Loggers.Log("Error modifing sl or tp", LoggingLevel.Trading);
                        }

                        for (int i = 0; i < this.tp_items; i++)
                        {
                            if (item.TpItems.Count != this.tp_items)
                            {
                                Core.Instance.Loggers.Log("Unmaching Orders Items", LoggingLevel.Error);
                                return;
                            }
                            var request = new ModifyOrderRequestParameters(item.TpItems[i]);

                            request.Price = this.ListOfDelegates.TpDelegate[i](this.ListOfDelegates.TpDelegateObj[i], item.Id);
                            request.AdditionalParameters = new List<SettingItem>
                            {
                                new SettingItemBoolean(OrderType.REDUCE_ONLY, true)
                            };
                            var resoult = Core.Instance.ModifyOrder(request);

                            if (resoult.Status == TradingOperationResultStatus.Failure)
                                Core.Instance.Loggers.Log("Error modifing sl or tp", LoggingLevel.Trading);
                        }
                    }
                       
                }
                catch (Exception ex)
                {
                    Core.Instance.Loggers.Log(ex.Message, LoggingLevel.Error);
                }
            }
        }
        private double RoundQuantity(double quantity, Symbol _Symbol)
        {
            //TODO: aggiungo un min lot per evitare l arrotondamento in difetto
            return (Math.Round(quantity / _Symbol.MinLot) * _Symbol.MinLot);
        }
    }
}
