// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using DivergentStrV0_1.OperationSystemAdv;
using DivergentStrV0_1.Strategies;
using DivergentStrV0_1.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using TradingPlatform.BusinessLayer;
using static System.Collections.Specialized.BitVector32;

namespace DivergentStrV0_1
{


    public class DivergentStrV0_1 : Strategy
    {
        #region Input / Attributi e campi
        //[InputParameter("######## Enviroment Settings ######", 0)]
        //private bool _menu_System = false;
        //[InputParameter("Atr IndicatroSettings Settings ", 0)]
        //private string _menu_Atr = "###############";
        //[InputParameter("Delta Settings ", 0)]
        //private string _menu_Delta = "###############";
        //[InputParameter("Strategy Settings ", 0)]
        //private string _menu_Strategy = "###############";

        //[InputParameter("Custom sessions count", 11, minimum: 0, maximum: 3, decimalPlaces: 0)]
        //public int _CustomSessionsCount = 0;

        // ====== Keys per i toggle master (devono combaciare con Text nelle Relation)
        private const string KEY_ENV = "######## Enviroment Settings ######";
        private const string KEY_STRAT = "######## Strategy Settings ######";
        private const string KEY_ATR = "######## Atr Indicator Settings ######";
        private const string KEY_DELTA = "######## Delta Settings ######";
        private const string KEY_SESS = "######## Sessions Settings ######";
        private const string KEY_SESS_COUNT = "######## Custom sessions count ######";

        // ====== Stato dei toggle (default ragionevoli; non dipendono dagli InputParameter)
        private bool _uiShowEnv = true;
        private bool _uiShowStrat = true;
        private bool _uiShowAtr = false;
        private bool _uiShowDelta = false;

        // ====== Backing fields UI per ATR (solo UI: collega alla tua logica quando vuoi)
        private int _uiAtrLen = 14;
        private bool _uiAtrNormalize = true;
        private double _uiAtrSlopeThr = 0.015; // 1.5%

        // ====== Backing fields UI per Delta (solo UI: collega alla tua logica quando vuoi)
        private bool _uiDeltaUseMedian = false;
        private int _uiDeltaLookback = 30;
        private double _uiDeltaThresholdMult = 2.0;
        private int _uiDeltaStrengthLookback = 30;
        private double _uiDeltaStrengthMult = 2.0;

        // ====== Backing fields UI per Sessions
        private int _CustomSessionsCount = 0;


        [InputParameter("Symbol", 0)]
        public Symbol _Symbol;

        [InputParameter("Account", 1)]
        public Account _Account;

        [InputParameter("Quantity", 3)]
        public double _Quantity = 1;

        [InputParameter("Allow Shorts", 6)]
        public bool _AllowShorts = true;

        [InputParameter("Sl Percentage", 7, 0.5, 200, 0.1)]
        public double _SlPercent = 1.00;

        [InputParameter("Tp Percentage", 8, 0.7, 200, 0.1)]
        public double _TPercentage = 2.5;

        [InputParameter("From Time", 9, 0.7, 200, 0.1)]
        public DateTime _fomTime ;

        private HistoricalData hd;
        private Indicator Ichimoku;
        private Indicator Volume;
        private Indicator CumulativeAbsorbtion;
        private StrategyTest _strategyTest;
        private IConditionable _conditionable = new RowanStrategy();
        private List<SimpleSessionUtc> _CustomSessions = new List<SimpleSessionUtc>();


        private bool Debug = false;

        private double procesPercent => this.hd != null &&
                              this.hd.VolumeAnalysisCalculationProgress != null ? this.hd.VolumeAnalysisCalculationProgress.ProgressPercent : 0;
        private bool readyToGo;
        private bool volumesLoaded => this.hd != null &&
                              this.hd.VolumeAnalysisCalculationProgress != null &&
                              this.hd.VolumeAnalysisCalculationProgress.ProgressPercent == 100;
        #endregion

        public DivergentStrV0_1()
            : base()
        {
            this.Name = this._conditionable.StrategyName;
            this.Description = "Gap Divergency ichi levels";
        }

        #region Main Methods/Lifecycle
        //HACK seams useless
        protected override void OnCreated()
        {
            Core.Instance.Loggers.Log("OnCreated", LoggingLevel.System);
        }
        protected override void OnRun()
        {

            if (!this._conditionable.Initialized)
            {
                var req = new HistoryRequestParameters()
                {
                    Aggregation = new HistoryAggregationTime(Period.MIN1, HistoryType.Last),
                    FromTime = this._fomTime,
                    ToTime = default,
                    Symbol = this._Symbol,

                };

                //this._strategyTest = new StrategyTest();
                //this._strategyTest.InjectStrategy(new FixedSlTpStrategy(0.95, 1.05));
                //this._strategyTest.Init(req, this._Account, true);
                //this._conditionable = _strategyTest;

            }

        }

        public override IList<SettingItem> Settings
        {
            get
            {
                var settings = base.Settings;

                // ===== 100x — Sessions =====
                // Master numerico: DA QUI dipendono start/end con relation
                settings.Add(new SettingItemBoolean(KEY_SESS, false)
                {
                    Text = KEY_SESS,
                    SortIndex = 1000,
                });

                settings.Add(new SettingItemInteger(KEY_SESS_COUNT, 0)
                {
                    Text = KEY_SESS_COUNT,
                    SortIndex = 1000,
                    Minimum = 0,
                    Maximum = 3,
                    Relation = new SettingItemRelationVisibility(KEY_SESS, true)
                });

                for (int i = 0; i < 3; i++)
                {
                    // Relation: mostra start/end solo quando il count lo include
                    SettingItemRelationVisibility relation =
                        (i == 0) ? new SettingItemRelationVisibility(KEY_SESS_COUNT, 1, 2, 3) :
                        (i == 1) ? new SettingItemRelationVisibility(KEY_SESS_COUNT, 2, 3) :
                                   new SettingItemRelationVisibility(KEY_SESS_COUNT, 3);

                    SimpleSessionUtc current = (this._CustomSessions.Count > i) ? this._CustomSessions[i] : null;

                    settings.Add(new SettingItemDateTime($"session{i + 1}Start", current != null
                        ? DateTime.Today.AddHours(current.Open.Hour).AddMinutes(current.Open.Minute)
                        : DateTime.UtcNow)
                    {
                        Text = $"Session {i + 1} start (UTC)",
                        SortIndex = 1001 + i * 2,
                        Relation = relation
                    });

                    settings.Add(new SettingItemDateTime($"session{i + 1}End", current != null
                        ? DateTime.Today.AddHours(current.Close.Hour).AddMinutes(current.Close.Minute)
                        : DateTime.UtcNow)
                    {
                        Text = $"Session {i + 1} end (UTC)",
                        SortIndex = 1002 + i * 2,
                        Relation = relation
                    });
                }

                // ===== 200x — Environment =====
                settings.Add(new SettingItemBoolean(KEY_ENV, _uiShowEnv)
                {
                    Text = KEY_ENV,
                    SortIndex = 2000
                });

                settings.Add(new SettingItemBoolean("Debug", this.Debug)
                {
                    Text = "Debug",
                    SortIndex = 2001,
                    Relation = new SettingItemRelationVisibility(KEY_ENV, true)
                });

                // ===== 300x — Strategy (campi già esistenti come InputParameter, riproposti in Settings) =====
                settings.Add(new SettingItemBoolean(KEY_STRAT, _uiShowStrat)
                {
                    Text = KEY_STRAT,
                    SortIndex = 3000
                });

                settings.Add(new SettingItemDouble(nameof(_Quantity), _Quantity)
                {
                    Text = "Quantity",
                    SortIndex = 3001,
                    Minimum = 0.0001,
                    Maximum = 1_000_000,
                    Increment = 0.0001,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemBoolean(nameof(_AllowShorts), _AllowShorts)
                {
                    Text = "Allow Shorts",
                    SortIndex = 3002,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemDouble(nameof(_SlPercent), _SlPercent)
                {
                    Text = "SL Percentage",
                    SortIndex = 3003,
                    Minimum = 0.1,
                    Maximum = 500,
                    Increment = 0.1,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemDouble(nameof(_TPercentage), _TPercentage)
                {
                    Text = "TP Percentage",
                    SortIndex = 3004,
                    Minimum = 0.1,
                    Maximum = 500,
                    Increment = 0.1,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemDateTime(nameof(_fomTime), _fomTime == default ? DateTime.UtcNow : _fomTime)
                {
                    Text = "From Time (UTC)",
                    SortIndex = 3005,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                // (Opzionale) Se vuoi anche Symbol/Account nel pannello:
                // settings.Add(new SettingItemSymbol(nameof(_Symbol), _Symbol) { ... Relation = new SettingItemRelationVisibility(KEY_STRAT, true) });
                // settings.Add(new SettingItemAccount(nameof(_Account), _Account) { ... Relation = new SettingItemRelationVisibility(KEY_STRAT, true) });

                // ===== 400x — ATR =====
                settings.Add(new SettingItemBoolean(KEY_ATR, _uiShowAtr)
                {
                    Text = KEY_ATR,
                    SortIndex = 4000
                });

                settings.Add(new SettingItemInteger(nameof(_uiAtrLen), _uiAtrLen)
                {
                    Text = "ATR Length",
                    SortIndex = 4001,
                    Minimum = 2,
                    Maximum = 200,
                    Relation = new SettingItemRelationVisibility(KEY_ATR, true)
                });

                settings.Add(new SettingItemBoolean(nameof(_uiAtrNormalize), _uiAtrNormalize)
                {
                    Text = "Use ATR Normalization",
                    SortIndex = 4002,
                    Relation = new SettingItemRelationVisibility(KEY_ATR, true)
                });

                settings.Add(new SettingItemDouble(nameof(_uiAtrSlopeThr), _uiAtrSlopeThr)
                {
                    Text = "Slope Threshold (norm.)",
                    SortIndex = 4003,
                    Minimum = 0.0,
                    Maximum = 1.0,
                    Increment = 0.001,
                    Relation = new SettingItemRelationVisibility(KEY_ATR, true)
                });

                // ===== 500x — Delta =====
                settings.Add(new SettingItemBoolean(KEY_DELTA, _uiShowDelta)
                {
                    Text = KEY_DELTA,
                    SortIndex = 5000
                });

                settings.Add(new SettingItemBoolean(nameof(_uiDeltaUseMedian), _uiDeltaUseMedian)
                {
                    Text = "Delta: Use Median",
                    SortIndex = 5001,
                    Relation = new SettingItemRelationVisibility(KEY_DELTA, true)
                });

                settings.Add(new SettingItemInteger(nameof(_uiDeltaLookback), _uiDeltaLookback)
                {
                    Text = "Delta: Lookback",
                    SortIndex = 5002,
                    Minimum = 5,
                    Maximum = 1000,
                    Relation = new SettingItemRelationVisibility(KEY_DELTA, true)
                });

                settings.Add(new SettingItemDouble(nameof(_uiDeltaThresholdMult), _uiDeltaThresholdMult)
                {
                    Text = "Delta: Threshold Multiplier",
                    SortIndex = 5003,
                    Minimum = 0.1,
                    Maximum = 20.0,
                    Increment = 0.1,
                    Relation = new SettingItemRelationVisibility(KEY_DELTA, true)
                });

                settings.Add(new SettingItemInteger(nameof(_uiDeltaStrengthLookback), _uiDeltaStrengthLookback)
                {
                    Text = "Delta Strength: Lookback",
                    SortIndex = 5004,
                    Minimum = 5,
                    Maximum = 1000,
                    Relation = new SettingItemRelationVisibility(KEY_DELTA, true)
                });

                settings.Add(new SettingItemDouble(nameof(_uiDeltaStrengthMult), _uiDeltaStrengthMult)
                {
                    Text = "Delta Strength: Threshold Multiplier",
                    SortIndex = 5005,
                    Minimum = 0.1,
                    Maximum = 20.0,
                    Increment = 0.1,
                    Relation = new SettingItemRelationVisibility(KEY_DELTA, true)
                });

                return settings;
            }
            set
            {
                base.Settings = value;

                // ===== Sessions =====
                if (value.TryGetValue(KEY_SESS, out bool sessBool))
                    if (sessBool == true)
                        if (value.TryGetValue(KEY_SESS_COUNT, out int sessCount))
                        {
                            this._CustomSessionsCount = 0; // disabilita tutte le sessioni
                            this._CustomSessionsCount = Math.Max(0, Math.Min(3, sessCount));

                            //📝 TODO: [Required] build sessions obj

                        }
                

                this._CustomSessions.Clear();
                for (int i = 0; i < this._CustomSessionsCount; i++)
                {
                    TimeOnly start = default;
                    TimeOnly end = default;
                    value.TryGetValue($"session{i + 1}Start", out start);
                    value.TryGetValue($"session{i + 1}End", out end);
                    this._CustomSessions.Add(new SimpleSessionUtc($"session{i + 1}", new List<DayOfWeek> { DayOfWeek.Monday }, start, end));
                }

                // ===== Env =====
                if (value.TryGetValue(KEY_ENV, out bool showEnv)) _uiShowEnv = showEnv;
                if (value.TryGetValue("Debug", out bool dbg)) this.Debug = dbg;

                // ===== Strategy =====
                if (value.TryGetValue(KEY_STRAT, out bool showStrat)) _uiShowStrat = showStrat;
                if (value.TryGetValue("Quantity", out double qty)) _Quantity = qty;
                if (value.TryGetValue("Allow Shorts", out bool allowShorts)) _AllowShorts = allowShorts;
                if (value.TryGetValue("SL Percentage", out double slp)) _SlPercent = slp;
                if (value.TryGetValue("TP Percentage", out double tpp)) _TPercentage = tpp;
                if (value.TryGetValue("From Time (UTC)", out DateTime ft)) _fomTime = ft;

                // ===== ATR =====
                if (value.TryGetValue(KEY_ATR, out bool showAtr)) _uiShowAtr = showAtr;
                if (value.TryGetValue("ATR Length", out int atrLen)) _uiAtrLen = atrLen;
                if (value.TryGetValue("Use ATR Normalization", out bool atrNorm)) _uiAtrNormalize = atrNorm;
                if (value.TryGetValue("Slope Threshold (norm.)", out double atrThr)) _uiAtrSlopeThr = atrThr;

                // ===== Delta =====
                if (value.TryGetValue(KEY_DELTA, out bool showDelta)) _uiShowDelta = showDelta;
                if (value.TryGetValue("Delta: Use Median", out bool dMed)) _uiDeltaUseMedian = dMed;
                if (value.TryGetValue("Delta: Lookback", out int dLb)) _uiDeltaLookback = dLb;
                if (value.TryGetValue("Delta: Threshold Multiplier", out double dTh)) _uiDeltaThresholdMult = dTh;
                if (value.TryGetValue("Delta Strength: Lookback", out int dSLb)) _uiDeltaStrengthLookback = dSLb;
                if (value.TryGetValue("Delta Strength: Threshold Multiplier", out double dSTh)) _uiDeltaStrengthMult = dSTh;
            }
        }



        protected override void OnStop()
        {
            this.readyToGo = false;
        }
        protected override void OnRemove()
        {

            //📝 TODO: [REQUIRED] Dispose Evrithing
            //📝 TODO: [VERIFY] Flush Log


        }
        #endregion

        protected override void OnInitializeMetrics(Meter meter)
        {
            base.OnInitializeMetrics(meter);

            try
            {
                this._conditionable.Metrics.ExportToMeter(meter);
            }
            catch (Exception)
            {

                throw;
            }
        }
    }
}