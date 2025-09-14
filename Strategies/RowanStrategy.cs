using DivergentStrV0_1.OperationSystemAdv;
using DivergentStrV0_1.OperationSystemAdv.DDDCore;
using DivergentStrV0_1.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;
using TradingPlatform.BusinessLayer.Licence;

namespace DivergentStrV0_1.Strategies
{
    internal enum TradeAction
    {
        Buy,
        Sell,
        Close,
        Revert,
        Wait
    }

    internal enum TradeSignal
    {
        OpenBuy,
        OpenSell,
        CloseLong,
        CloseSell,
        Wait,
        Unknown
    }

    internal class RowanStrategy : ConditionableBase<SlTpData>
    {

        #region === TRADABLE SESSIONS (UTC) ===
        // Sessione 1
        public bool EnableTradeSession1 { get; set; } = true;
        public string TradeSession1Name { get; set; } = "SESSION-1";
        public List<DayOfWeek> TradeSession1DaysMask { get; set; }
        public string TradeSession1OpenUtc { get; set; } = "08:00";
        public string TradeSession1CloseUtc { get; set; } = "12:00";

        // Sessione 2
        public bool EnableTradeSession2 { get; set; } = true;
        public string TradeSession2Name { get; set; } = "SESSION-2";
        public List<DayOfWeek> TradeSession2DaysMask { get; set; }
        public string TradeSession2OpenUtc { get; set; } = "13:00";
        public string TradeSession2CloseUtc { get; set; } = "17:00";

        // Sessione 3
        public bool EnableTradeSession3 { get; set; } = true;
        public string TradeSession3Name { get; set; } = "SESSION-3";
        public List<DayOfWeek> TradeSession3DaysMask { get; set; }
        public string TradeSession3OpenUtc { get; set; } = "18:00";
        public string TradeSession3CloseUtc { get; set; } = "21:00";
        #endregion

        private Indicator _atrIndicator;
        private Indicator _deltaBaseIndicator;
        private Indicator _slipageAtrIndicator;
        private int _maxOpen;
        private double _totalQuantity;
        private double _maxSessionLos;
        private double _startSessionLoss;        
        private int _minTradeSign;
        private int _minCloseSign;
        private bool _sessionClosed = false;
        private bool _strategyActive = true;
        private int _verbosityFreq = 0;
        private int _verbosityFreqCount = 0;
        private bool _loadAsync;

        public bool AllowToTrade
        {
            get
            {
                var usdloss = this.Metrics.NetProfit - this._startSessionLoss;
                var maxLoss = usdloss < 0 && Math.Abs(usdloss) >= Math.Abs(_maxSessionLos);
                var sessionActive = StaticSessionManager.CurrentStatus == Status.Active;

                if (maxLoss || !sessionActive)
                    return false;
                else
                    return true;
            }
        }

        public RowanStrategy()
        {
            
        }

        public RowanStrategy(Indicator DeltaBaseIndicator, Indicator atrsIndicator, int max_open, double totalquantity,
            int minTradeSign, int minCloseSign, double maxSessionLosUsd, int verbosity_frequency, int slipageAtrPeriod) : base()
        {

            this._atrIndicator = atrsIndicator;
            this._deltaBaseIndicator = DeltaBaseIndicator;
            this._maxOpen = max_open;
            this._totalQuantity = totalquantity;
            this._maxSessionLos = maxSessionLosUsd;
            this._minTradeSign = minTradeSign;
            this._minCloseSign = minCloseSign;
            this._verbosityFreq = verbosity_frequency;
            this._slipageAtrIndicator = Core.Instance.Indicators.BuiltIn.ATR(slipageAtrPeriod, MaMode.SMA);
        }


        public override void Init(HistoryRequestParameters req, Account account, bool loadAsync = false, string description = "", bool allowHeavyMetrics = false)
        {
            this.ManagerChoice = ManagerType.PositionBased;
            base.Init(req, account, loadAsync, description, allowHeavyMetrics);
            StaticSessionManager.Initialize(this.HistoryProvider);
            StaticSessionManager.TradeSessionsStatusChanged += StaticSessionManager_TradeSessionsStatusChanged;
            this._loadAsync = loadAsync;

            if (!this._loadAsync)
            {
                this.HistoryProvider.HistoricalData.AddIndicator(this._atrIndicator);
                this.HistoryProvider.HistoricalData.AddIndicator(this._deltaBaseIndicator);
                this.HistoryProvider.HistoricalData.AddIndicator(this._slipageAtrIndicator);
            }

        }

       
        public override void OnVolumeDataReady()
        {

            //ðŸ“ TODO: [DEBUG] debug this

            base.OnVolumeDataReady();

            if (this._loadAsync)
            {
                if (this._atrIndicator.Count == 0)
                    this.HistoryProvider.HistoricalData.AddIndicator(this._atrIndicator);
                if (this._deltaBaseIndicator.Count == 0)
                    this.HistoryProvider.HistoricalData.AddIndicator(this._deltaBaseIndicator);
                if (this._slipageAtrIndicator.Count == 0)
                    this.HistoryProvider.HistoricalData.AddIndicator(this._slipageAtrIndicator);
            }

        }

        private void StaticSessionManager_TradeSessionsStatusChanged(object sender, Status e)
        {
            if (e == Status.Active)
            {

                //ðŸ“ TODO: [Debug Required]
                this._startSessionLoss = this.Metrics.NetProfit;
                this._sessionClosed = false;
            }
        }

        protected override List<HistoryUpdAteType> GetUpdateTypes()
        {
            return new List<HistoryUpdAteType>
            {
                HistoryUpdAteType.NewItem,
            };
        }

        public override void Dispose()
        {
            StaticSessionManager.TradeSessionsStatusChanged -= StaticSessionManager_TradeSessionsStatusChanged;
            this._atrIndicator.Dispose();
            this._deltaBaseIndicator.Dispose();

            base.Dispose();
        }

        public override double SetQuantity() => this._totalQuantity / this._maxOpen;

        //ðŸ“ TODO: [Critical] passare un MarketData object con tutti i dati necessari per le decisioni di trade

        #region ðŸž BUG [Bug noto da risolvere #5] 
        //BUG #5 vengono aperti short mentre la strategia e long 
        #endregion

        public override void Update(object obj)
        {

            //ðŸ§  HINT: [FLOW] ritento l inserimento degli indicatori a causa del bug noto #1
            if (this._atrIndicator.Count == 0)
                this.HistoryProvider.HistoricalData.AddIndicator(this._atrIndicator);
            if (this._deltaBaseIndicator.Count == 0)
                this.HistoryProvider.HistoricalData.AddIndicator(this._deltaBaseIndicator);
            if (this._slipageAtrIndicator.Count == 0)
                this.HistoryProvider.HistoricalData.AddIndicator(this._slipageAtrIndicator);

            //ðŸ§  HINT: [INFO] Base entrypoint dal history provider creato in condizional base tramite il costruttore statico
            HistoryEventArgs e = obj as HistoryEventArgs ?? null;
            if (e == null)
            {
                Core.Instance.Loggers.Log("Rowan Strategy error at Update casting", LoggingLevel.Error);
                Core.Instance.Loggers.Log("Strategy Will be Disabled", LoggingLevel.Error);
                this.ForceClosePositions(5);

                #region ðŸž BUG [Bug noto da risolvere]
                //BUG #4
                #endregion

                this._strategyActive = false;
                return;
            }

            HistoryItem item = (HistoryItem)e.HistoryItem;

            StaticSessionManager.Update(item);

            if (this._loadAsync && !this.HistoryProvider.VolumeDataReady) 
                return;

            
            try
            {
                if (!this.AllowToTrade)
                {

                    //ðŸ“ TODO: [Debuggare] session closing

                    //ðŸ§  HINT: [Richiesta  Soddisfatta] out on marketClose
                    //ðŸ§  HINT: [Richiesta  Soddisfatta] out on maxLoss
                    if (StaticSessionManager.CurrentStatus == Status.Active && !_sessionClosed)
                    {
                        Core.Instance.Loggers.Log("Max session loss reached ", LoggingLevel.Trading);
                        this.ForceClosePositions(5);
                        _sessionClosed = true;
                    }
                    return;
                }


                //ðŸ“ TODO: [REQUIRED] aggiungere slippage atr
                //ðŸ“ TODO: [REQUIRED] verificare se e hd [0], [1] oppure [1], [2]

                SlTpData marketData = new SlTpData()
                {
                    currentPrice = item[PriceType.Open],
                    Symbol = this.Symbol,
                    AtrInTicks = Math.Abs(this.Symbol.CalculateTicks(this._atrIndicator.GetValue()+ item[PriceType.Open], item[PriceType.Open])),
                };

                //ðŸ§  HINT: [Flusso] ripeto con Market Data
                //SlTpData marketData = (SlTpData)obj;
               

                TradeSignal signal = this.CalculateTradeSignal();
                TradeAction action = TradeAction.Wait;

                switch (this.Metrics.ExposedSide)
                {
                    case ExpositionSide.Long:
                        if (signal == TradeSignal.CloseLong)
                            action = TradeAction.Close;
                        else if (signal == TradeSignal.OpenSell)
                            action = TradeAction.Revert;
                        else if (signal == TradeSignal.OpenBuy)
                            action = TradeAction.Buy;
                        break;
                    case ExpositionSide.Short:
                        if (signal == TradeSignal.CloseSell)
                            action = TradeAction.Close;
                        else if (signal == TradeSignal.OpenBuy)
                            action = TradeAction.Revert;
                        else if (signal == TradeSignal.OpenSell)
                            action = TradeAction.Sell;
                        break;
                    case ExpositionSide.Both:
                        #region ðŸž BUG [Bug noto da risolvere #4] 
                        //BUG #4 Strategy active non cambia 
                        #endregion
                        #region ðŸž BUG [Bug noto da risolvere #5] 
                        //BUG #5 La strategia si ritrova esposta su entrambi i lati 
                        #endregion
                        Core.Instance.Loggers.Log("Exposed on Both Sides positions ll be closed and strategy aborted", LoggingLevel.Trading);
                        this.ForceClosePositions(5);
                        //this._strategyActive = false;
                        break;
                    case ExpositionSide.Unexposed:
                        if (signal == TradeSignal.OpenBuy)
                            action = TradeAction.Buy;
                        else if (signal == TradeSignal.OpenSell)
                            action = TradeAction.Sell;
                        break;
                }


                #region ðŸž BUG [Bug noto da risolvere #4] 
                //BUG #4 Strategy active non cambia 
                #endregion
                if (!_strategyActive)
                    return;


               

                if (action == TradeAction.Buy || action == TradeAction.Sell)
                {

                    //ðŸ§  HINT: [Suggerimento di flusso] il sistema continua ad aprire e chiudere items per mantenere il conto dell esposizione coerente 
                    //ðŸ§  HINT: [Da Verificare ] Gli Ordini per gli item chiusi vengono rimossi
                    //ðŸ§  HINT: [Da Verificare ] La conversione sulle quantita

                    //if (this.Metrics.ExposedCount >= _maxOpen)
                    if (this.Metrics.ExposedAmount >= _maxOpen*this.Quantity)
                        Core.Instance.Loggers.Log($"[TRADE SIGNAL] AVOIDED DUE MAX EXPO REACHED Signal={signal}, Action={action}", LoggingLevel.Trading);
                    else
                    {
                        if (action == TradeAction.Buy)
                        {
                            marketData.SlTriggerPrice = this.HistoryProvider.HistoricalData[1][PriceType.Low];
                            this.ComputeTradeAction(marketData, Side.Buy);

                        }
                        else if (action == TradeAction.Sell)
                        {
                            marketData.SlTriggerPrice = this.HistoryProvider.HistoricalData[1][PriceType.High];
                            this.ComputeTradeAction(marketData, Side.Sell);
                        }
                        Core.Instance.Loggers.Log($"[TRADE SIGNAL] Signal={signal}, Action={action}", LoggingLevel.Trading);
                    }
                }


                //ðŸ“ TODO: [DEBUG] check if this logic works and correctly effects on items

                if (action == TradeAction.Close)
                {
                    bool res = this.ForceClosePositions(5);

                    if (res)
                        Core.Instance.Loggers.Log($"[TRADE SIGNAL] ALL POSITIONS CLOSED Signal={signal}, Action={action}", LoggingLevel.Trading);
                    else
                    {
                        Core.Instance.Loggers.Log($"[TRADE SIGNAL] FAILED TO CLOSE POSITIONS, STRATEGY STOPPED Signal={signal}, Action={action}", LoggingLevel.Error);
                        #region ðŸž BUG [Bug noto da risolvere #4] 
                        //BUG #4 Strategy active non cambia 
                        #endregion
                        this._strategyActive = false;
                    }
                }

                //ðŸ“ TODO: [DEBUG] check if this logic works and correctly effects on items
                if (action == TradeAction.Revert)
                {
                    bool res = this.ForceClosePositions(5);
                    if (res)
                    {
                        marketData.SlTriggerPrice = signal == TradeSignal.OpenBuy ?
                            this.HistoryProvider.HistoricalData[1][PriceType.Low] : this.HistoryProvider.HistoricalData[1][PriceType.High];
                        Core.Instance.Loggers.Log($"[TRADE SIGNAL] ALL POSITIONS CLOSED FOR REVERSAL Signal={signal}, Action={action}", LoggingLevel.Trading);
                        this.ComputeTradeAction(marketData, signal == TradeSignal.OpenBuy ? Side.Buy : Side.Sell);
                        Core.Instance.Loggers.Log($"[TRADE SIGNAL] NEW POSITION OPENED AFTER REVERSAL Signal={signal}, Action={action}", LoggingLevel.Trading);
                    }
                    else
                    {
                        Core.Instance.Loggers.Log($"[TRADE SIGNAL] FAILED TO CLOSE POSITIONS FOR REVERSAL, STRATEGY STOPPED Signal={signal}, Action={action}", LoggingLevel.Error);
                        #region ðŸž BUG [Bug noto da risolvere #4] 
                        //BUG #4 Strategy active non cambia 
                        #endregion
                        this._strategyActive = false;
                    }
                }

                else
                {
                    // Update SL only when allowed: WAIT or aligned with exposure
                    try
                    {
                        this.UpdateSlTp(marketData, isSl: true);

                    }
                    catch { Utils.AppLog.Error("Update Sl", "Failed to update Sl"); }
                    //ðŸ“ TODO: [LOGS] IMPLEMENTARE LOGGING SPECIFICO PER I SEGTNALI
                }

                if (this._verbosityFreqCount <= this._verbosityFreq)
                {
                    this._verbosityFreqCount++;

                    if (this._verbosityFreqCount == this._verbosityFreq)
                    {
                        Core.Instance.Loggers.Log($"[VERBOSE] Strategy status: SessionActive={StaticSessionManager.CurrentStatus}, AllowToTrade={this.AllowToTrade}", LoggingLevel.System);
                        Core.Instance.Loggers.Log($"[VERBOSE] ExposedSide={this.Metrics.ExposedSide}, ExposedCount={this.Metrics.ExposedCount}, Signal={signal}, Action={action}", LoggingLevel.System);
                        Core.Instance.Loggers.Log($"[VERBOSE] Signal={signal}, Action={action}", LoggingLevel.System);
                        this._verbosityFreqCount = 0;
                    }
                }


            }
            catch (Exception ex)
            {

                //ðŸ“ TODO: [Log]
                Core.Instance.Loggers.Log($"Rowan Strategy error at Update with message : {ex.Message}", LoggingLevel.Error);
                throw;
            }
        }

        private void ComputeTradeAction(SlTpData data, Side side ) => this.Trade(side, data.currentPrice, data, data);

        private bool ForceClosePositions(int max_attempt)
        {
            //int attempt = 0;
            //var positions = Core.Instance.Positions.Where(p => p.Symbol == this.Symbol && p.Account == this.Account).ToList();

            //foreach (var pos in positions)
            //    pos.Close();

            ////bool any = Core.Instance.Positions.Where(p => p.Symbol == this.Symbol && p.Account == this.Account).Any();

            ////while (any && attempt < max_attempt)
            ////{
            ////    any = Core.Instance.Positions.Where(p => p.Symbol == this.Symbol && p.Account == this.Account).Any();
            ////    //ðŸ“ TODO: [Debug Required] check if items closed

            ////    System.Threading.Thread.Sleep(500);
            ////    attempt++;

            ////    if (attempt >= max_attempt && any)
            ////    {
            ////        Core.Instance.Loggers.Log($"[CRITICAL] Unable to close all positions after {max_attempt} attempts!", LoggingLevel.Trading);
            ////        return false;
            ////    }
            ////}

            //attempt = 0;

            //var orders = Core.Instance.Orders.Where(o => o.Symbol == this.Symbol && o.Account == this.Account &&
            //o.Status == OrderStatus.Opened || o.Status == OrderStatus.PartiallyFilled).ToList();

            //foreach (Order ord in orders)
            //    ord.Cancel();

            ////while (Core.Instance.Orders.Where(o => o.Symbol == this.Symbol && o.Account == this.Account &&
            ////    o.Status == OrderStatus.Opened || o.Status == OrderStatus.PartiallyFilled).Any() && attempt < max_attempt)
            ////{
            ////    //ðŸ“ TODO: [Debug Required] check if items closed

            ////    System.Threading.Thread.Sleep(500);
            ////    attempt++;

            ////    if (attempt >= max_attempt)
            ////    {
            ////        Core.Instance.Loggers.Log($"[CRITICAL] Unable to cancel orders {max_attempt} attempts!", LoggingLevel.Trading);
            ////        return false;
            ////    }
            ////}

            //return true;
            var posId = Core.Instance.Positions.Where(p => p.Symbol == this.Symbol && p.Account == this.Account)
                .Select(p => p.Id).ToList();
            var objs = this._manager.Items.Where(x => x.Position != null && posId.Contains(x.Position.Id)).ToList();
            foreach (var obj in objs)
            {

                #region ðŸ§ª HACK [Soluzione temporanea]
                //Sistemare questa porcheria
                #endregion

                var o = obj as TpSlItemPosition;
                o.TryUpdateStatus(true);
            }

            return true;
        }

        private TradeSignal CalculateTradeSignal()
        {
            int logSignCount = 0;
            int shortSignCount = 0;
            foreach (LineSeries sign in this._deltaBaseIndicator.LinesSeries)
            {

                #region ðŸž BUG [RESOLVE] #2
                //TUTTE LE LINESERIES RITORNANO NAN PER OGNI VALORE AD OGNI INDICE
                //var debug1 = sign.GetValue(1);
                //var debug2 = sign.GetValue(2);
                //var debug3 = sign.GetValue() == 0;
                //for (int i = 0; i < this._deltaBaseIndicator.Count; i++)
                //{
                //    if (sign[i] >= 0)
                //        Core.Instance.Loggers.Log($"DeltaBaseIndicator {sign.Name} value at {i} is {sign[i]}", LoggingLevel.Trading);
                //}
                //var debug4 = this._deltaBaseIndicator.HistoricalData;
                #endregion


                if (sign.GetValue() > 0)
                    logSignCount++;
                else if (sign.GetValue() < 0)
                    shortSignCount++;
            }

            foreach (LineSeries sign in this._atrIndicator.LinesSeries)
            {
                if (sign.GetValue() > 0)
                    logSignCount++;
                else if (sign.GetValue() < 0)
                    shortSignCount++;
            }

            if (logSignCount == shortSignCount)
                return TradeSignal.Unknown;
            else if (logSignCount > shortSignCount)
                if (logSignCount >= this._minTradeSign)
                    
                    return TradeSignal.OpenBuy;
                else if (logSignCount >= this._minCloseSign)
                    return TradeSignal.CloseSell;
                else                     
                    return TradeSignal.Wait;
            else if (shortSignCount > logSignCount)
                if (shortSignCount >= this._minTradeSign)
                    return TradeSignal.OpenSell;
                else if (logSignCount >= this._minCloseSign)
                    return TradeSignal.CloseLong;
                else
                    return TradeSignal.Wait;
            else
                return TradeSignal.Unknown;


        }
    }
}
