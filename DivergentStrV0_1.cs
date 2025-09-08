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
        // ====== Keys per i toggle master (devono combaciare con Text nelle Relation)
        //private const string KEY_ENV = "######## Enviroment Settings ######";
        private const string KEY_STRAT = "######## Strategy Settings ######";
        private const string KEY_ATR = "######## Atr Indicator Settings ######";
        private const string KEY_DELTA = "######## Delta Settings ######";
        private const string KEY_SESS = "######## Sessions Settings ######";
        private const string KEY_SESS_COUNT = "######## Custom sessions count ######";
        private const string KEY_SESS_USEDEFAULT = "Use Default Sessions";

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

        // ====== Strategy Parameters
        private double _quantity = 1000;
        private double _minSlInTicks = 20;
        private double _maxSlInTicks = 100;
        private double _maxTpInTicks = 1000;
        private bool _debugMode = false;

        [InputParameter("Symbol", 0)]
        public Symbol _Symbol;

        [InputParameter("Account", 1)]
        public Account _Account;

        [InputParameter("From Time (UTC)", 2)]
        public DateTime _fomTime = DateTime.UtcNow.AddDays(-30);

        [InputParameter("Async Mode", 3)]
        public bool _inputDebugMode = false;

        [InputParameter("Period", 4)]
        public Period _period = Period.MIN1;

        private HistoricalData hd;
        private Indicator AtrIndicator;
        private Indicator DeltaIndicato;
        private RowanStrategy _strategy;
        private bool _UseDefaultSessions = true;
        private IConditionable _conditionable = new RowanStrategy();
        private List<SimpleSessionUtc> _CustomSessions = new List<SimpleSessionUtc>();

        //📝 TODO: [IMPLEMENT] aggiungere metodo per gestirer le sessioni fuori sessione 

        private bool Debug = false;

        // ====== Session day mappings
        private Dictionary<int, List<DayOfWeek>> _sessionDays = new Dictionary<int, List<DayOfWeek>>();

        // ====== Public properties for accessing configured values
        public double Quantity => _quantity;
        public double MinSlInTicks => _minSlInTicks;
        public double MaxSlInTicks => _maxSlInTicks;
        public double MaxTpInTicks => _maxTpInTicks;
        public bool DebugMode => Debug;
        public IReadOnlyList<SimpleSessionUtc> CustomSessions => _CustomSessions.AsReadOnly();
        public int CustomSessionsCount => _CustomSessionsCount;

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
            this.Description = "Rowan Strategy";
        }

        #region Main Methods/Lifecycle
        //HACK seams useless
        protected override void OnCreated()
        {
            Core.Instance.Loggers.Log("OnCreated", LoggingLevel.System);
        }
        protected override void OnRun()
        {

            //📝 TODO: [REQUIRED] use settings

            this.AtrIndicator = Core.Instance.Indicators.CreateIndicator(Core.Instance.Indicators.All.FirstOrDefault(x => x.Name == "RVOL (evolved)"));
            this.DeltaIndicato = Core.Instance.Indicators.CreateIndicator(Core.Instance.Indicators.All.FirstOrDefault(x => x.Name == "DeltaBasedIndicators"));

            // Set indicator parameters
            this.DeltaIndicato.Settings = new List<SettingItem>
            {
                new SettingItemBoolean("Force Volume Ready", true)
            };

            if (!this._conditionable.Initialized)
            {
                var req = new HistoryRequestParameters()
                {
                    Aggregation = new HistoryAggregationTime(Period.MIN1, HistoryType.Last),
                    FromTime = this._fomTime,
                    ToTime = default,
                    Symbol = this._Symbol,

                };

                foreach (var s in OffMarketUtc.Build())
                    StaticSessionManager.AddSession(s, Utils.SessionType.Target);

                foreach (var sv in InMarketUtc.Build())
                    StaticSessionManager.AddSession(sv, Utils.SessionType.Trade);

                this._strategy = new RowanStrategy(this.DeltaIndicato, this.AtrIndicator, 3, 1000, 3, 2, 100, 3);
                this._strategy.InjectStrategy(new RowanSlTpStrategy(100,500));
                this._strategy.Init(req, this._Account, true);
                this._conditionable = _strategy;

            }

        }

        public override IList<SettingItem> Settings
        {
            get
            {
                var settings = base.Settings;

                #region// ===== 100x — Sessions =====

                //📝 TODO: [REQUIRED] SET relation visibility to use default

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

                settings.Add(new SettingItemBoolean(KEY_SESS_USEDEFAULT, false)
                {
                    Text = KEY_SESS_USEDEFAULT,
                    SortIndex = 1000,
                    Value = false
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

                    foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
                    {
                        settings.Add(new SettingItemBoolean($"session{i + 1}On{day}", true)
                        {
                            Text = $"Session {i + 1} on {day}",
                            SortIndex = 1003 + i * 2,
                            Relation = relation
                        });
                    }
                }
                #endregion

                #region// ===== 300x — Strategy (campi già esistenti come InputParameter, riproposti in Settings) =====
                settings.Add(new SettingItemBoolean(KEY_STRAT, _uiShowStrat)
                {
                    Text = KEY_STRAT,
                    SortIndex = 3000
                });

                settings.Add(new SettingItemDouble("Quantity", _quantity)
                {
                    Text = "Quantity",
                    SortIndex = 3001,
                    Minimum = 0.0001,
                    Maximum = 1_000_000,
                    Increment = 0.0001,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemDouble("Min SL In Ticks", _minSlInTicks)
                {
                    Text = "Min SL In Ticks",
                    SortIndex = 3003,
                    Minimum = 1,
                    Maximum = 15000,
                    Increment = 1,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemDouble("Max SL In Ticks", _maxSlInTicks)
                {
                    Text = "Max SL In Ticks",
                    SortIndex = 3004,
                    Minimum = 1,
                    Maximum = 50000,
                    Increment = 1,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                settings.Add(new SettingItemDouble("Max Tp In Ticks", _maxTpInTicks)
                {
                    Text = "Max TP In Ticks",
                    SortIndex = 3005,
                    Minimum = 1,
                    Maximum = 50000,
                    Increment = 1,
                    Relation = new SettingItemRelationVisibility(KEY_STRAT, true)
                });

                #endregion

                #region atr // ===== 400x — ATR =====
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
                #endregion

                #region// ===== 500x — Delta =====
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
                #endregion

                return settings;
            }
            set
            {
                base.Settings = value;

                try
                {
                    // ===== Sessions =====
                if (value.TryGetValue(KEY_SESS, out bool sessBool))
                {
                    if (sessBool && value.TryGetValue(KEY_SESS_COUNT, out int sessCount))
                    {
                        this._CustomSessionsCount = Math.Max(0, Math.Min(3, sessCount));
                    }
                }

                // Rebuild custom sessions based on current count
                this._CustomSessions.Clear();
                this._sessionDays.Clear();


                
                for (int i = 0; i < this._CustomSessionsCount; i++)
                {
                    try
                    {
                        // Get start and end times from DateTime settings
                        DateTime startDateTime = DateTime.UtcNow;
                        DateTime endDateTime = DateTime.UtcNow;
                        
                        if (value.TryGetValue($"session{i + 1}Start", out DateTime startDt))
                            startDateTime = Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(startDateTime, Core.Instance.TimeUtils.SelectedTimeZone);
                        if (value.TryGetValue($"session{i + 1}End", out DateTime endDt))
                            endDateTime = Core.Instance.TimeUtils.ConvertFromTimeZoneToUTC(endDateTime, Core.Instance.TimeUtils.SelectedTimeZone);

                        // Convert DateTime to TimeOnly
                        TimeOnly start = TimeOnly.FromDateTime(startDateTime);
                        TimeOnly end = TimeOnly.FromDateTime(endDateTime);

                        // Validate time range
                        if (start == end)
                        {
                            Core.Instance.Loggers.Log($"Warning: Session {i + 1} has same start and end time", LoggingLevel.Error);
                        }

                        // Collect active days for this session
                        List<DayOfWeek> activeDays = new List<DayOfWeek>();
                        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
                        {
                            if (value.TryGetValue($"session{i + 1}On{day}", out bool dayActive) && dayActive)
                            {
                                activeDays.Add(day);
                            }
                        }

                        // If no days are selected, default to Monday-Friday
                        if (activeDays.Count == 0)
                        {
                            activeDays.AddRange(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday });
                            Core.Instance.Loggers.Log($"Session {i + 1}: No days selected, defaulting to weekdays", LoggingLevel.Error);
                        }

                        // Store day mapping for reference
                        this._sessionDays[i] = activeDays;

                        // Create and add the session
                        var session = new SimpleSessionUtc($"CustomSession{i + 1}", activeDays, start, end);
                        this._CustomSessions.Add(session);
                    }
                    catch (Exception ex)
                    {
                        Core.Instance.Loggers.Log($"Error creating session {i + 1}: {ex.Message}", LoggingLevel.Error);
                    }
                }

                if (value.TryGetValue(KEY_SESS_USEDEFAULT, out bool useDefault)) _UseDefaultSessions = useDefault;

                // ===== Strategy =====
                if (value.TryGetValue(KEY_STRAT, out bool showStrat)) _uiShowStrat = showStrat;
                if (value.TryGetValue("From Time (UTC)", out DateTime ft)) _fomTime = ft;
                if (value.TryGetValue("Quantity", out double qty)) 
                {
                    _quantity = Math.Max(0.0001, qty); // Ensure positive quantity
                }
                if (value.TryGetValue("Min SL In Ticks", out double minSl)) 
                {
                    _minSlInTicks = Math.Max(1, minSl); // Ensure minimum 1 tick
                }
                if (value.TryGetValue("Max SL In Ticks", out double maxSl)) 
                {
                    _maxSlInTicks = Math.Max(_minSlInTicks, maxSl); // Ensure max >= min
                }
                if (value.TryGetValue("Max TP In Ticks", out double maxTp)) 
                {
                    _maxTpInTicks = Math.Max(1, maxTp); // Ensure positive TP
                }
                if (value.TryGetValue("Debug", out bool debugMode)) 
                {
                    _debugMode = debugMode;
                    Debug = debugMode;
                }

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
                catch (Exception ex)
                {
                    Core.Instance.Loggers.Log($"Error updating settings: {ex.Message}", LoggingLevel.Error);
                    // Reset to safe defaults on error
                    _CustomSessionsCount = 0;
                    _CustomSessions.Clear();
                    _sessionDays.Clear();
                }
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