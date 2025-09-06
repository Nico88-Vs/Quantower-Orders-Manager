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
        [InputParameter("Custom sessions count", 11, minimum: 0, maximum: 3, decimalPlaces: 0)]
        public int _CustomSessionsCount = 0;

        private List<SimpleSessionUtc> _CustomSessions = new List<SimpleSessionUtc>();

        [InputParameter("Symbol", 0)]
        public Symbol _Symbol;

        [InputParameter("Account", 1)]
        public Account _Account;

        [InputParameter("Tick delay", 2, 0, 100, increment: 1)]
        public int entry_tick_delay = 5;

        [InputParameter("Quantity", 3)]
        public double _Quantity = 1;

        [InputParameter("Max Short Expo", 4, minimum: 1, maximum: 10, decimalPlaces: 0)]
        public int _MaxShortExpo = 3;

        [InputParameter("Max Long Expo", 5, minimum: 1, maximum: 10, decimalPlaces: 0)]
        public int _MaxLongExpo = 3;

        [InputParameter("Allow Shorts", 6)]
        public bool _AllowShorts = true;

        [InputParameter("Sl Percentage", 7, 0.5, 200, 0.1)]
        public double _SlPercent = 1.00;

        [InputParameter("Tp Percentage", 8, 0.7, 200, 0.1)]
        public double _TPercentage = 2.5;

        [InputParameter("From Time", 9, 0.7, 200, 0.1)]
        public DateTime _fomTime ;

        [InputParameter("HD Preload required Dais", 10)]
        public int _HdRequireDais = 31;
        
        private HistoricalData hd;
        private Indicator Ichimoku;
        private Indicator Volume;
        private Indicator CumulativeAbsorbtion;
        private StrategyTest _strategyTest;
        private IConditionable _conditionable = new StrategyTest();

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

            #region 🧯 DEPRECATED [Da rimuovere nella prossima versione]
            /*
             * ⚠️ Questo blocco è deprecato
             * TODO: sostituire o rimuovere
            //this.readyToGo = false;
            //this._Symbol.NewLast += this._Symbol_NewLast;
            //this._Symbol.NewQuote += this._Symbol_NewQuote;
             */
            #endregion

            if (!this._conditionable.Initialized)
            {
                var req = new HistoryRequestParameters()
                {
                    Aggregation = new HistoryAggregationTime(Period.MIN1, HistoryType.Last),
                    FromTime = this._fomTime,
                    ToTime = default,
                    Symbol = this._Symbol,

                };

                this._strategyTest = new StrategyTest();
                this._strategyTest.InjectStrategy(new FixedSlTpStrategy(0.95, 1.05));
                this._strategyTest.Init(req, this._Account, true);
                this._conditionable = _strategyTest;

            }

        }

        public override IList<SettingItem> Settings
        {
            get
            {
                var settings = base.Settings;

                var sessionCountItem = new SettingItemInteger(nameof(_CustomSessionsCount), this._CustomSessionsCount)
                {
                    Text = "Custom sessions count",
                    SortIndex = 1000,
                    Minimum = 0,
                    Maximum = 3
                };
                settings.Add(sessionCountItem);

                for (int i = 0; i < 3; i++)
                {
                    SimpleSessionUtc current = (this._CustomSessions.Count > i) ? this._CustomSessions[i] : null;

                    SettingItemRelationVisibility relation;
                    if (i == 0)
                        relation = new SettingItemRelationVisibility(nameof(_CustomSessionsCount), 1, 2, 3);
                    else if (i == 1)
                        relation = new SettingItemRelationVisibility(nameof(_CustomSessionsCount), 2, 3);
                    else
                        relation = new SettingItemRelationVisibility(nameof(_CustomSessionsCount), 3);

                    settings.Add(new SettingItemDateTime($"session{i + 1}Start", current != null ? DateTime.Today.
                        AddHours(current.Open.Hour).AddMinutes(current.Open.Minute) : DateTime.UtcNow)
                    {
                        Text = $"Session {i + 1} start (UTC)",
                        SortIndex = 1001 + i * 2,
                        Relation = relation
                    });

                    settings.Add(new SettingItemDateTime($"session{i + 1}End", current != null ? DateTime.Today.
                        AddHours(current.Close.Hour).AddMinutes(current.Close.Minute)  : DateTime.UtcNow)
                    {
                        Text = $"Session {i + 1} end (UTC)",
                        SortIndex = 1002 + i * 2,
                        Relation = relation
                    });
                }

                return settings;
            }
            set
            {
                base.Settings = value;

                if (value.TryGetValue(nameof(_CustomSessionsCount), out int count))
                    this._CustomSessionsCount = Math.Max(0, Math.Min(3, count));

                this._CustomSessions.Clear();
                for (int i = 0; i < this._CustomSessionsCount; i++)
                {
                    TimeOnly start = default;
                    TimeOnly end = default;
                    value.TryGetValue($"session{i + 1}Start", out start);
                    value.TryGetValue($"session{i + 1}End", out end);
                    this._CustomSessions.Add(new SimpleSessionUtc("session{i + 1}", new List<DayOfWeek> {DayOfWeek.Monday },  start, end));
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