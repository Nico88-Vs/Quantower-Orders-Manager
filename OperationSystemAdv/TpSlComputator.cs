using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public class TpSlComputator<T>
    {
        private readonly SlTpCondictionHolder<T> _holder;

        public TpSlComputator(SlTpCondictionHolder<T> holder)
        {
            _holder = holder;
        }

        //TODO: Impementare i metodi CreateStopLoss e CreateTakeProfit Dispatcher events

        public PlaceOrderRequestParameters CreateStopLoss(T data, string id, double quantity)
        {
            var price = _holder.DefineSl?.Invoke(data, id) ?? double.NaN;
            if (double.IsNaN(price)) return null;

            return new PlaceOrderRequestParameters
            {
                Symbol = _holder.Symbol,
                Side = Side.Sell,
                Price = price,
                Quantity = quantity,
                TimeInForce = TimeInForce.GTC,
                Comment = id
            };
        }

        public PlaceOrderRequestParameters CreateTakeProfit(T data, string id, double quantity)
        {
            var price = _holder.DefineTp?.Invoke(data, id) ?? double.NaN;
            if (double.IsNaN(price)) return null;

            return new PlaceOrderRequestParameters
            {
                Symbol = _holder.Symbol,
                Side = Side.Sell,
                Price = price,
                Quantity = quantity,
                OrderTypeId = "OrderType.Limit",
                TimeInForce = TimeInForce.GTC,
                Comment = id
            };
        }

        public IEnumerable<Order> PlaceOrders(T data, string id, double quantity)
        {
            var result = new List<Order>();

            var sl = CreateStopLoss(data, id, quantity);
            var tp = CreateTakeProfit(data, id, quantity);

            //if (sl != null)
            //{
            //    var r1 = Core.Instance.PlaceOrder(sl);
            //    if (r1.Status == TradingOperationResultStatus.Success)
            //        result.Add(r1.Order);
            //}

            //if (tp != null)
            //{
            //    var r2 = Core.Instance.PlaceOrder(tp);
            //    if (r2.Status == TradingOperationResultStatus.Success)
            //        result.Add(r2.Order);
            //}

            return result;
        }
    }
}
