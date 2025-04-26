using System;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv.DDDCore
{
    public interface IHistoryDataProvider
    {
        Task<HistoricalData> LoadAsync(DateTime from, Period period, CancellationToken token);
        Task WaitForReadyAsync(CancellationToken token);
        event Action OnNewData;
    }
}
