using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DivergentStrV0_1.OperationSystemAdv.DDDAsync;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv.DDDCore
{
    public class VolumeHistoryProvider : IHistoryDataProvider
    {
        private readonly Symbol _symbol;
        private HistoricalData _history;
        private IVolumeAnalysisCalculationProgress _progress;
        private readonly AsyncSignal _profileReadySignal = new();

        public event Action OnNewData;

        public VolumeHistoryProvider(Symbol symbol)
        {
            _symbol = symbol;
        }

        public async Task<HistoricalData> LoadAsync(DateTime from, Period period, CancellationToken token)
        {
            _history = _symbol.GetHistory(period, from);
            _history.NewHistoryItem += (s, e) => OnNewData?.Invoke();

            var parameters = new VolumeAnalysisCalculationParameters
            {
                DeltaCalculationType = DeltaCalculationType.AggressorFlag
            };

            _progress = Core.Instance.VolumeAnalysis.CalculateProfile(_history, parameters);
            _progress.ProgressChanged += (s, e) =>
            {
                if (e.ProgressPercent == 100)
                    _profileReadySignal.Signal();
            };

            return _history;
        }

        public Task WaitForReadyAsync(CancellationToken token)
            => _profileReadySignal.WaitAsync(token);
    }
}
