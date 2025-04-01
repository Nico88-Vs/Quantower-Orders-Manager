using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public interface IOrderExecutor
    {
        TradingOperationResult ExecuteMarketOrder(OrderRequestParameters req);
    }

}
