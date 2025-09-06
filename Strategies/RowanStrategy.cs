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
        #region ToDo
        //📝 TODO: [CRITICAL] Creare le sessioni di trading con conversione EST e gestione DST
        //📝 TODO: [CRITICAL] Implementare calcolo RVOL con smoothing HMA
        //📝 TODO: [CRITICAL] Implementare calcolo Volume Delta ratios (APAVD/CPVD)
        //📝 TODO: [CRITICAL] Implementare Volume Delta Strength calculation
        //📝 TODO: [CRITICAL] Implementare Custom HMA con lunghezza divisa per ATR
        //📝 TODO: [CRITICAL] Implementare Volume Delta to Volume ratio
        //📝 TODO: [CRITICAL] Implementare Volume Delta divergence detection
        //📝 TODO: [CRITICAL] Manca Lo Slipage basato su ATR?
        //📝 TODO: [HIGH] Aggiungere parametri configurabili per tutti gli indicatori
        //📝 TODO: [HIGH] Implementare sistema di selezione parametri attivi per entry/exit

        //📝 TODO: [MEDIUM] load async disabilitato - valutare se riabilitare per performance
        //📝 TODO: [HIGH] Implementare inizializzazione completa degli indicatori
        //📝 TODO: [HIGH] Configurare parametri di lookback per tutti i calcoli
        //📝 TODO: [MEDIUM] Aggiungere validazione dei parametri di input
        //📝 TODO: [HIGH] Implementare logging completo per session status changes
        //📝 TODO: [CRITICAL] Fixare: Non Funziona se viene riavviata la strategia durante la sessione
        //📝 TODO: [HIGH] Implementare recovery logic per restart durante sessione attiva
        //📝 TODO: [MEDIUM] Salvare stato sessione su storage persistente
        //📝 TODO: [CRITICAL] Implementare calcolo di tutti gli indicatori per la candela corrente
        //📝 TODO: [CRITICAL] Calcolare RVOL smoothed = (RvolShort + RvolLong + HMA)/3
        //📝 TODO: [CRITICAL] Calcolare Average Price Move to Volume Delta ratio
        //📝 TODO: [CRITICAL] Calcolare Volume Delta Strength con soglie configurabili
        //📝 TODO: [CRITICAL] Calcolare Custom HMA con length/ATR
        //📝 TODO: [CRITICAL] Calcolare Volume Delta to Volume ratio
        //📝 TODO: [CRITICAL] Rilevare Volume Delta divergence da price movement

        //📝 TODO: [CRITICAL] Implementare logica di entry signals
        //📝 TODO: [CRITICAL] Verificare che almeno X parametri su N selezionati siano true per entry
        //📝 TODO: [CRITICAL] Verificare che siamo dentro le time frames selezionate
        //📝 TODO: [CRITICAL] Implementare entry per BUY quando longokay conditions sono soddisfatte
        //📝 TODO: [CRITICAL] Implementare entry per SELL quando shortokay conditions sono soddisfatte

        //📝 TODO: [CRITICAL] Implementare logica di exit signals
        //📝 TODO: [CRITICAL] Verificare che almeno Y parametri su M selezionati siano true per exit
        //📝 TODO: [CRITICAL] Chiudere posizioni quando usciamo dalle time frames selezionate
        //📝 TODO: [CRITICAL] Implementare position reversal sulla stessa candela

        //📝 TODO: [HIGH] Implementare order stacking se abilitato
        //📝 TODO: [HIGH] Verificare max loss limit prima di ogni trade
        //📝 TODO: [HIGH] Implementare slippage calculation basato su ATR

        //📝 TODO: [MEDIUM] Aggiungere logging dettagliato ogni 3 candele
        //📝 TODO: [MEDIUM] Loggare valori correnti di tutti i parametri
        //📝 TODO: [MEDIUM] Loggare closest high/low TP points
        //📝 TODO: [MEDIUM] Loggare previous candle max/min + ATR values
        #endregion

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
        private int _maxOpen;
        private double _totalQuantity;
        private double _maxSessionLos;
        private double _startSessionLoss;        private int _minTradeSign;
        private int _minCloseSign;
        private bool _sessionClosed = false;
        private bool _strategyActive = true;
        private int _verbosityFreq = 0;
        private int _verbosityFreqCount = 0;

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
            int minTradeSign, int minCloseSign, double maxSessionLosUsd, int verbosity_frequency) : base()
        {

            this._atrIndicator = atrsIndicator;
            this._deltaBaseIndicator = DeltaBaseIndicator;
            this._maxOpen = max_open;
            this._totalQuantity = totalquantity;
            this._maxSessionLos = maxSessionLosUsd;
            this._minTradeSign = minTradeSign;
            this._minCloseSign = minCloseSign;
            this._verbosityFreq = verbosity_frequency;
        }


        public override void Init(HistoryRequestParameters req, Account account, bool loadAsync = false, string description = "", bool allowHeavyMetrics = false)
        {
            base.Init(req, account, false, description, allowHeavyMetrics);
            StaticSessionManager.TradeSessionsStatusChanged += StaticSessionManager_TradeSessionsStatusChanged;
        }

        private void StaticSessionManager_TradeSessionsStatusChanged(object sender, Status e)
        {
            if (e == Status.Active)
            {

                //📝 TODO: [Debug Required]
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

        public override double SetQuantity() => this._totalQuantity / this._maxOpen;

        //📝 TODO: [Critical] passare un MarketData object con tutti i dati necessari per le decisioni di trade
        public override void Update(object obj)
        {
            if (!_strategyActive)
                return;
            try
            {
                //📝 TODO: [Critical] spostare nella stratetegia principale
                HistoryEventArgs item = (HistoryEventArgs)obj;
                HistoryItem data = (HistoryItem)item.HistoryItem;
                StaticSessionManager.Update(data);

                if (!this.AllowToTrade)
                {

                    //📝 TODO: [Debuggare] session closing

                    //🧠 HINT: [Richiesta  Soddisfatta] out on marketClose
                    //🧠 HINT: [Richiesta  Soddisfatta] out on maxLoss
                    if (StaticSessionManager.CurrentStatus == Status.Active && !_sessionClosed)
                    {
                        Core.Instance.Loggers.Log("Max session loss reached ", LoggingLevel.Trading);
                        this.ForceClosePositions(5);
                        _sessionClosed = true;
                    }
                    return;
                }

                //🧠 HINT: [Flusso] ripeto con Market Data
                SlTpData marketData = (SlTpData)obj;
               

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
                        Core.Instance.Loggers.Log("Exposed on Both Sides positions ll be closed and strategy aborted", LoggingLevel.Trading);
                        this.ForceClosePositions(5);
                        this._strategyActive = false;
                        break;
                    case ExpositionSide.Unexposed:
                        if (signal == TradeSignal.OpenBuy)
                            action = TradeAction.Buy;
                        else if (signal == TradeSignal.OpenSell)
                            action = TradeAction.Sell;
                        break;
                }


                //📝 TODO: [LOGS] IMPLEMENTARE LOGGING SPECIFICO PER I SEGTNALI

                if(action == TradeAction.Buy || action == TradeAction.Sell)
                {
                    if (this.Metrics.ExposedCount >= _maxOpen)
                        Core.Instance.Loggers.Log($"[TRADE SIGNAL] AVOIDED DUE MAX EXPO REACHED Signal={signal}, Action={action}", LoggingLevel.Trading);
                    else
                    {
                        this.ComputeTradeAction(marketData, action == TradeAction.Buy ? Side.Buy : Side.Sell);
                        Core.Instance.Loggers.Log($"[TRADE SIGNAL] Signal={signal}, Action={action}", LoggingLevel.Trading);
                    }
                }


                //📝 TODO: [DEBUG] check if this logic works and correctly effects on items

                if (action == TradeAction.Close)
                {
                    bool res = this.ForceClosePositions(5);

                    if (res)
                        Core.Instance.Loggers.Log($"[TRADE SIGNAL] ALL POSITIONS CLOSED Signal={signal}, Action={action}", LoggingLevel.Trading);
                    else
                    {
                        Core.Instance.Loggers.Log($"[TRADE SIGNAL] FAILED TO CLOSE POSITIONS, STRATEGY STOPPED Signal={signal}, Action={action}", LoggingLevel.Error);
                        this._strategyActive = false;
                    }
                }

                //📝 TODO: [DEBUG] check if this logic works and correctly effects on items
                if (action == TradeAction.Revert)
                {
                    bool res = this.ForceClosePositions(5);
                    if (res)
                    {
                        Core.Instance.Loggers.Log($"[TRADE SIGNAL] ALL POSITIONS CLOSED FOR REVERSAL Signal={signal}, Action={action}", LoggingLevel.Trading);
                        this.ComputeTradeAction(marketData, signal == TradeSignal.OpenBuy ? Side.Buy : Side.Sell);
                        Core.Instance.Loggers.Log($"[TRADE SIGNAL] NEW POSITION OPENED AFTER REVERSAL Signal={signal}, Action={action}", LoggingLevel.Trading);
                    }
                    else
                    {
                        Core.Instance.Loggers.Log($"[TRADE SIGNAL] FAILED TO CLOSE POSITIONS FOR REVERSAL, STRATEGY STOPPED Signal={signal}, Action={action}", LoggingLevel.Error);
                        this._strategyActive = false;
                    }
                }

                if (action == TradeAction.Wait)
                    this.UpdateSlTp(marketData, true);

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
            catch (Exception)
            {

                //📝 TODO: [Log]

                throw;
            }
        }

        private void ComputeTradeAction(SlTpData data, Side side ) => this.Trade(side, data.currentPrice, data, data);

        private bool ForceClosePositions(int max_attempt)
        {
            int attempt = 0;
            var positions = Core.Instance.Positions.Where(p => p.Symbol == this.Symbol && p.Account == this.Account).ToList();

            foreach (var pos in positions)
                pos.Close();

            while (Core.Instance.Positions.Where(p => p.Symbol == this.Symbol && p.Account == this.Account).Any() && attempt < max_attempt)
            {

                //📝 TODO: [Debug Required] check if items closed

                System.Threading.Thread.Sleep(500);
                attempt++;

                if (attempt >= max_attempt)
                {
                    Core.Instance.Loggers.Log($"[CRITICAL] Unable to close all positions after {max_attempt} attempts!", LoggingLevel.Trading);
                    return false;
                }
            }

            attempt = 0;

            var orders = Core.Instance.Orders.Where(o => o.Symbol == this.Symbol && o.Account == this.Account &&
            o.Status == OrderStatus.Opened || o.Status == OrderStatus.PartiallyFilled).ToList();

            foreach (Order ord in orders)
                ord.Cancel();

            while (Core.Instance.Orders.Where(o => o.Symbol == this.Symbol && o.Account == this.Account &&
                o.Status == OrderStatus.Opened || o.Status == OrderStatus.PartiallyFilled).Any() && attempt < max_attempt)
            {
                //📝 TODO: [Debug Required] check if items closed

                System.Threading.Thread.Sleep(500);
                attempt++;

                if (attempt >= max_attempt)
                {
                    Core.Instance.Loggers.Log($"[CRITICAL] Unable to cancel orders {max_attempt} attempts!", LoggingLevel.Trading);
                    return false;
                }
            }

            return true;
        }

        private TradeSignal CalculateTradeSignal()
        {
            int logSignCount = 0;
            int shortSignCount = 0;
            foreach (LineSeries sign in this._deltaBaseIndicator.LinesSeries)
            {
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
                else if (shortSignCount >= this._minCloseSign)
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
