using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    //TODO: Espandere
    public class QuantowerOrderExecutor : IOrderExecutor
    {
        public TradingOperationResult ExecuteMarketOrder(OrderRequestParameters req)
        {
            var marketOrderType = req.Symbol.GetAlowedOrderTypes(OrderTypeUsage.All)
                .FirstOrDefault(x => x.ConnectionId == req.Symbol.ConnectionId && x.Behavior == OrderTypeBehavior.Market);

            if (marketOrderType == null)
            {
                Core.Instance.Loggers.Log("Market order non supportato per questo simbolo.", LoggingLevel.Error);
            }

            var request = new PlaceOrderRequestParameters
            {
                Account = req.Account,
                Symbol = req.Symbol,
                Side = req.Side,
                Quantity = req.Quantity,
                OrderTypeId = marketOrderType.Id,
                TimeInForce = TimeInForce.GTC,
                Comment = req.Comment
            };

            var result = Core.Instance.PlaceOrder(request);

            if (result.Status != TradingOperationResultStatus.Success)
                Core.Instance.Loggers.Log($"Errore ordine: {result.Message}", LoggingLevel.Error);

            return result;
        }
    }

}
