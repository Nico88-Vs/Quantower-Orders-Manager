using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv.DDDCore
{
    public enum HistoryUpdAteType
    {
        NewItem,
        UpdateItem,
        VolumeUpdate,
    }

    public static class HystoryDataProviderFactory
    {
        public static HystoryDataProvider Create(
            HistoryRequestParameters request,
            Symbol symbol,
            List<HistoryUpdAteType> updateTypes,
            VolumeAnalysisCalculationParameters volumeRequest = null,
            bool async = false,
            Action<HistoryEventArgs> onUpdate = null,
            Action onVolumeReady = null,
            int timeoutMs = 10000,
            int retryDelayMs = 500)
        {
            var provider = new HystoryDataProvider(
                request,
                symbol,
                updateTypes,
                volumeRequest,
                async,
                timeoutMs,
                retryDelayMs);


            #region 🧯 DEPRECATED [Da rimuovere nella prossima versione]
            /*
             * ⚠️ Questo blocco è deprecato
             * //
             * TODO: sostituire o rimuovere
             */
            #endregion

            if (onUpdate != null)
            {
                provider.HistoryItemUpdate += onUpdate;
            }

            if (onVolumeReady != null)
            {

                #region 🐞 BUG [RESOLVE] #1
                //capita che questo evento venga chiamato poco dopo la creazione quindi il link fallisce perche successivo al evento
                #endregion

                provider.VolumeAnalysisCompleted += onVolumeReady;
            }
            return provider;
        }
    }

    public class HystoryDataProvider : IDisposable
    {
        #region 📘 REQ [SYSTEM]
        //BUILD history data request
        //Subscribe Events
        //Load Volume
        //Share data with other modules
        #endregion

        public HistoricalData HistoricalData { get; private set; }
        public Symbol Symbol { get; }
        public bool LoadVolume { get; }
        public bool LoadAsAsync { get; }
        public bool VolumeDataReady { get; private set; } = false;

        public event Action<HistoryEventArgs> HistoryItemUpdate;
        public event Action VolumeAnalysisCompleted;

        public IVolumeAnalysisCalculationProgress VolumeAnalysisCalProgress { get; private set; }

        private List<HistoryUpdAteType> _historyUpdateReqType;
        private Thread worker;
        private bool AsyncRunner = true;
        private CancellationTokenSource CancToken;
        private readonly int _timeoutMs;
        private readonly int _retryDelayMs;
        private int _elapsedMs = 0;

        public HystoryDataProvider(
            HistoryRequestParameters request,
            Symbol symbol,
            List<HistoryUpdAteType> historyUpdateReqType,
            VolumeAnalysisCalculationParameters volReq = null,
            bool loadAsAsdync = false,
            int timeoutMs = 10000,
            int retryDelayMs = 500)
        {
            this.HistoricalData = symbol.GetHistory(request);
            this.Symbol = symbol;
            this.LoadVolume = volReq != null;
            this._historyUpdateReqType = historyUpdateReqType;
            this.LoadAsAsync = loadAsAsdync;
            this._timeoutMs = timeoutMs;
            this._retryDelayMs = retryDelayMs;

            if (this.LoadVolume)
            {
                if (loadAsAsdync)
                {
                    this.CancToken = new CancellationTokenSource();
                    this.ExecuteAsync(volReq, this.CancToken.Token);
                    this.WaitForReady(_timeoutMs);
                }
                else
                {
                    this.LoadVolumeSync(volReq);
                }
            }

            if (_historyUpdateReqType.Contains(HistoryUpdAteType.NewItem))
            {

                this.HistoricalData.NewHistoryItem += this.HistoricalData_NewHistoryItem;
            }

            if (_historyUpdateReqType.Contains(HistoryUpdAteType.UpdateItem))
            {

                this.HistoricalData.HistoryItemUpdated += this.HistoricalData_HistoryItemUpdated;
            }
        }

        private void HistoricalData_HistoryItemUpdated(object sender, HistoryEventArgs e) => this.HandleUpdate(e);
        private void HistoricalData_NewHistoryItem(object sender, HistoryEventArgs e) => this.HandleUpdate(e);

        private void LoadVolumeSync(VolumeAnalysisCalculationParameters req)
        {
            this.VolumeAnalysisCalProgress = Core.Instance.VolumeAnalysis.CalculateProfile(this.HistoricalData, req);
            this.VolumeAnalysisCalProgress.ProgressChanged += this.VolumeAnalysisCalProgress_ProgressChanged;
        }

        public Task ExecuteAsync(VolumeAnalysisCalculationParameters req, CancellationToken token)
        {
            try
            {
                this.worker = new Thread(() =>
                {
                    try
                    {
                        this.VolumeAnalysisCalProgress = Core.Instance.VolumeAnalysis.CalculateProfile(this.HistoricalData, req);
                        this.VolumeAnalysisCalProgress.ProgressChanged += this.VolumeAnalysisCalProgress_ProgressChanged;
                    }
                    catch (Exception ex)
                    {
                        Core.Instance.Loggers.Log("Errore nel thread di VolumeAnalysis: " + ex.Message, LoggingLevel.Error);
                    }
                });

                this.worker.IsBackground = true;
                this.worker.Name = "VolumeProfileWatcher";
                this.worker.Start();

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log("Errore durante ExecuteAsync: " + ex.Message, LoggingLevel.Error);
                return Task.CompletedTask;
            }
        }

        public void Dispose()
        {
            if (this.worker != null)
            {
                this.CancToken?.Cancel();
                this.AsyncRunner = false;
                this.worker.DisableComObjectEagerCleanup();
                this.worker = null;
            }

            if (this.HistoricalData != null)
            {
                try
                {
                    this.HistoricalData.NewHistoryItem -= this.HistoricalData_NewHistoryItem;
                }
                catch (Exception) { throw; }
                try
                {
                    this.HistoricalData.HistoryItemUpdated -= this.HistoricalData_HistoryItemUpdated;
                }
                catch (Exception) { throw; }
                finally
                {
                    this.HistoricalData.Dispose();
                    this.HistoricalData = null;
                }
            }

            if (this.VolumeAnalysisCalProgress != null)
            {
                this.VolumeAnalysisCalProgress.ProgressChanged -= this.VolumeAnalysisCalProgress_ProgressChanged;
                this.VolumeAnalysisCalProgress.Dispose();
            }
        }

        private void HandleUpdate(HistoryEventArgs e)
        {
            if (this.LoadAsAsync && !this.VolumeDataReady)
                this.WaitForReady(_timeoutMs);

            this.HistoryItemUpdate?.Invoke(e);
        }

        private void WaitForReady(int maxWaitMs)
        {
            Core.Instance.Loggers.Log($"⏳ Attesa VolumeDataReady ({this._elapsedMs} ms)...", LoggingLevel.System);

            this._elapsedMs = 0;
            while (!this.VolumeDataReady && !this.CancToken.IsCancellationRequested && _elapsedMs < maxWaitMs)
            {
                Thread.Sleep(_retryDelayMs);
                _elapsedMs += _retryDelayMs;
            }

            Core.Instance.Loggers.Log($"⏳Fine Attesa VolumeDataReady ({this._elapsedMs} ms)...", LoggingLevel.System);

        }

        private void VolumeAnalysisCalProgress_ProgressChanged(object sender, VolumeAnalysisTaskEventArgs e)
        {
            if (e.ProgressPercent == 100)
            {
                this.VolumeDataReady = true;
                this.VolumeAnalysisCompleted?.Invoke();
            }

            Core.Instance.Loggers.Log("✅ VolumeDataReady ricevuto", LoggingLevel.System);

        }
    }
}
