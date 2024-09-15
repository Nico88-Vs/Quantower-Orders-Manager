using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer.LocalOrders;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1
{
    public class OrderManager
    {
        #region Parameters
        private readonly string Name = "Basic Order Manager Class";
        private CancellationTokenSource cts;

        public bool Finished { get; private set; }
        private bool allowed_order_type;
        private OrderType limit_ordertype;
        private bool order_placed = false;
        private string localOrderId = string.Empty;

        public PlaceOrderRequestParameters placeOrderRequest { get; private set; }
        public string orderId { get; private set; }
        public HistoryType History_Type { get; private set; }
        public double LimitPrice { get; set; }
        public int DeltaInTick { get; set; }
        #endregion

        public OrderManager(HistoryType historyType, int deltaTick = 5)
        {
            this.History_Type = historyType;
            this.Finished = true;
            this.DeltaInTick = deltaTick;
        }


        public void PlaceNewOrder(PlaceOrderRequestParameters _placeOrderRequest)
        {
            if (!this.Finished)
                return;

            this.Finished = false;
            this.placeOrderRequest = _placeOrderRequest;

            try
            {
                this.SetOrderType();

                //HACK: invece di usare gli ordini locali 
                //HINT: devo creare qui il cts perche era in TrySave
                this.cts = new CancellationTokenSource();
                //this.TrySaveLocalOrder();
                this.OverrideOrder(_placeOrderRequest);

                if (this.History_Type == HistoryType.Last)
                {
                    placeOrderRequest.Symbol.NewLast += this.Symbol_NewLast;
                }
                else
                {
                    placeOrderRequest.Symbol.NewQuote += this.Symbol_NewQuote;
                }
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log($"Somenti Wrong With Order {this.ToString()}");
                Core.Instance.Loggers.Log($"With ex.Message {ex.Message}");
                this.OnCancel();
            }

        }


        #region utils
        private void OverrideOrder(OrderRequestParameters _overrideOrderRequest)
        {
            this.placeOrderRequest = new PlaceOrderRequestParameters()
            {
                Account = _overrideOrderRequest.Account,
                Symbol = _overrideOrderRequest.Symbol,
                Side = _overrideOrderRequest.Side,
                Quantity = _overrideOrderRequest.Quantity,
                TimeInForce = _overrideOrderRequest.TimeInForce,
                Price = _overrideOrderRequest.Price,
                OrderTypeId = this.limit_ordertype.Id,
            };
        }

        private void ProcessPrice(double price)
        {
            if (this.order_placed || this.cts.IsCancellationRequested)
                return;

            bool place = false;

            try
            {
                if (placeOrderRequest.Side == Side.Buy)
                {
                    var new_price = placeOrderRequest.Symbol.CalculatePrice(placeOrderRequest.Price, -this.DeltaInTick);
                    if (price < new_price)
                    {
                        place = true;
                    }
                }

                if (placeOrderRequest.Side == Side.Sell)
                {
                    var new_price = placeOrderRequest.Symbol.CalculatePrice(placeOrderRequest.Price, +DeltaInTick);
                    if (price > new_price)
                    {
                        place = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log($"Error In Placement {ex.Message}");
            }
            finally
            {
                if (place)
                {
                    placeOrderRequest.SendingSource = this.Name;
                    var resoult = Core.Instance.PlaceOrder(placeOrderRequest);
                    if (resoult.Status == TradingOperationResultStatus.Failure)
                        this.OnCancel();
                    if (resoult.Status == TradingOperationResultStatus.Success)
                    {
                        this.orderId = resoult.OrderId;
                        Core.Instance.Loggers.Log($"Eureca, placed", LoggingLevel.Trading);
                        this.order_placed = true;
                        Core.Instance.OrderAdded += this.Instance_OrderAdded;
                    }
                }
            }
        }

        private void SetOrderType()
        {
            var order_type = placeOrderRequest.Symbol.GetAlowedOrderTypes(OrderTypeUsage.All).FirstOrDefault(x => x.Usage == OrderTypeUsage.All && x.Behavior == OrderTypeBehavior.Limit);
            if (order_type == null)
            {
                Core.Instance.Loggers.Log("Missing Valid limit order Type", LoggingLevel.Error);
                Core.Instance.Loggers.Log("Tring Stop Order Type", LoggingLevel.Error);
                order_type = placeOrderRequest.Symbol.GetAlowedOrderTypes(OrderTypeUsage.All).FirstOrDefault(x => x.Usage == OrderTypeUsage.All && x.Behavior == OrderTypeBehavior.Stop);
                if (order_type == null)
                {
                    Core.Instance.Loggers.Log("Missing order for this connection", LoggingLevel.Error);
                    var tipi = placeOrderRequest.Symbol.GetAlowedOrderTypes(OrderTypeUsage.All);
                    order_type = placeOrderRequest.Symbol.GetAlowedOrderTypes(OrderTypeUsage.All).FirstOrDefault(x => x.Usage == OrderTypeUsage.Order && x.Behavior == OrderTypeBehavior.Market);
                    if (order_type == null)
                    {
                        allowed_order_type = false;
                        return;
                    }
                }
            }

            this.limit_ordertype = order_type;
        }

        private void TrySaveLocalOrder()
        {
            try
            {
                this.placeOrderRequest = placeOrderRequest;
                this.cts = new CancellationTokenSource();

                //HINT Create local order
                var localOrder = new LocalOrder
                {
                    Symbol = placeOrderRequest.Symbol,
                    Account = placeOrderRequest.Account,
                    Side = placeOrderRequest.Side,
                    TotalQuantity = placeOrderRequest.Quantity,
                    OrderType = this.limit_ordertype,
                    TimeInForce = placeOrderRequest.TimeInForce,
                    Price = placeOrderRequest.Price,
                    //HINT:Inutili in un limit

                    //TriggerPrice = placeOrderRequest.TriggerPrice,
                    //TrailOffset = placeOrderRequest.TrailOffset
                };
                //HINT Hold local order
                localOrderId = Core.Instance.LocalOrders.AddOrder(localOrder);
                Core.Instance.LocalOrders.Updated += LocalOrdersOnUpdated;

                if (this.History_Type == HistoryType.Last)
                {
                    placeOrderRequest.Symbol.NewLast += this.Symbol_NewLast;
                }
                else
                {
                    placeOrderRequest.Symbol.NewQuote += this.Symbol_NewQuote;
                }

                //while (!this.order_placed && !this.cts.IsCancellationRequested)
                //    Thread.Sleep(10);
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log($"Somenti Wrong in {this.ToString()}", LoggingLevel.Error);
                Core.Instance.Loggers.Log($"With Ex.message {ex.Message}", LoggingLevel.Error);
            }
        }
        #endregion

        #region events
        void LocalOrdersOnUpdated(object sender, LocalOrderEventArgs e)
        {
            var localOrder = e.LocalOrder;

            if (localOrderId != localOrder.Id)
                return;

            if (e.Lifecycle == EntityLifecycle.Removed)
            {
                this.cts?.Cancel();
                return;
            }

            placeOrderRequest.Price = localOrder.Price;
            placeOrderRequest.TriggerPrice = localOrder.TriggerPrice;
            placeOrderRequest.TrailOffset = localOrder.TrailOffset;
            placeOrderRequest.Quantity = localOrder.TotalQuantity;
            placeOrderRequest.TimeInForce = localOrder.TimeInForce;
            var placeOrderAdditionalParameters = placeOrderRequest.AdditionalParameters;
            placeOrderAdditionalParameters.UpdateValues(localOrder.AdditionalInfo);
            placeOrderRequest.AdditionalParameters = placeOrderAdditionalParameters;
        }

        private void Symbol_NewLast(Symbol symbol, Last last)
        {
            //TODO: Order is not placed

            if (!order_placed)
                this.ProcessPrice(last.Price);
            else
            {
                if (placeOrderRequest.Side == Side.Buy)
                {
                    //TODO: limitPrice e 0
                    if (last.Price < LimitPrice)
                    {
                        var resoult = Core.Instance.Orders.First(x => x.Id == orderId).Cancel();
                        if (resoult.Status == TradingOperationResultStatus.Success)
                            this.OnCancel();

                    }
                }
                else
                {
                    if (last.Price > LimitPrice)
                    {
                        var resoult = Core.Instance.Orders.First(x => x.Id == orderId).Cancel();
                        if (resoult.Status == TradingOperationResultStatus.Success)
                            this.OnCancel();
                    }
                }

            }
        }

        private void Instance_OrderAdded(Order obj)
        {
            if (!this.order_placed)
                return;
            if (obj.Id == this.orderId)
            {
                Core.Instance.LocalOrders.RemoveOrder(this.localOrderId);
                //this.Finished = true;
                this.OnCancel();
            }
        }

        private void Symbol_NewQuote(Symbol symbol, Quote quote)
        {
            double price = this.History_Type == HistoryType.Bid ? quote.Bid : quote.Ask;
            if (!order_placed)
                this.ProcessPrice(price);
            else
            {
                if (placeOrderRequest.Side == Side.Buy)
                {

                    if (price < LimitPrice)
                    {
                        var resoult = Core.Instance.Orders.First(x => x.Id == orderId).Cancel();
                        if (resoult.Status == TradingOperationResultStatus.Success)
                        {

                            this.OnCancel();
                        }

                    }
                }
                else
                {
                    if (price > LimitPrice)
                    {
                        var resoult = Core.Instance.Orders.First(x => x.Id == orderId).Cancel();
                        if (resoult.Status == TradingOperationResultStatus.Success)
                            this.OnCancel();
                    }
                }
            }
        }

        #endregion

        #region Lifecicle
        public void Dispose()
        {
            this.OnCancel();
            if (placeOrderRequest != null && placeOrderRequest.Symbol != null)
            {
                if (this.History_Type == HistoryType.Last)
                    placeOrderRequest.Symbol.NewLast -= this.Symbol_NewLast;
                else
                    placeOrderRequest.Symbol.NewQuote -= this.Symbol_NewQuote;
            }
        }
        private void OnCancel()
        {
            this.placeOrderRequest.Symbol.NewQuote -= this.Symbol_NewQuote;
            this.placeOrderRequest.Symbol.NewLast -= this.Symbol_NewLast;

            Core.Instance.OrderAdded -= this.Instance_OrderAdded;
            this.cts?.Cancel();
        }
        #endregion

    }
}
