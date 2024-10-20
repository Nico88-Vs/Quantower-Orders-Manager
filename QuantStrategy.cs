// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using CondictionalStrategyExample;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml.Linq;
using TpSlManager;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1
{
    //TODO dedfinire i limiti d ingresso ()
    //TODO utilizzare approccio sentiment , segnale , conferema 
    //TODO trailing Stop 
    //TODO ichimoku target
    //TODO gestire stop dinamici 
    //TODO tracciare gli incroci come e livelli 
    //TODO altro ancora 
    //TODO creare e utilizzare una libreria dedicata a ichi

    public class QuantStrategy : Strategy
    {
        #region Input / Attributi e campi
        [InputParameter("Symbol", 0)]
        public Symbol _Symbol = Core.Instance.Symbols.FirstOrDefault();
        [InputParameter("Account", 1)]
        public Account _Account;
        [InputParameter("HD Preload required Dais", 2)]
        public int _HdRequireDais = 1;
        [InputParameter("Tick delay", 3, 0, 100, increment: 1)]
        public int entry_tick_delay = 5;
        [InputParameter("Absorbtion Period", 4)]
        public Period _absorbtionPeriod = Period.MIN30;
        [InputParameter("Sma Period int", 5)]
        public int _SmaPeriodInt = 200;
        [InputParameter("Start Period", 6)]
        public Period _StartPeriod = Period.HOUR1;
        [InputParameter("EndPeriod Period", 7)]
        public Period _EndPeriod = Period.HOUR8;
        [InputParameter("HD Period", 7)]
        public Period _HdPeriod = Period.MIN30;

        private HistoricalData hd;
        private Indicator Ichimoku;
        private Indicator Volume;
        private Indicator CumulativeAbsorbtion;
        private Indicator SMA;
        //private SlTpCondictionHolder<int> condiHolder { get; set; }
        private IConditionable Conditionable { get; set; }

        private double procesPercent => this.hd != null &&
                              this.hd.VolumeAnalysisCalculationProgress != null ? this.hd.VolumeAnalysisCalculationProgress.ProgressPercent : 0;
        private bool readyToGo;
        private bool volumesLoaded => this.hd != null &&
                              this.hd.VolumeAnalysisCalculationProgress != null &&
                              this.hd.VolumeAnalysisCalculationProgress.ProgressPercent == 100;
        #endregion

        public QuantStrategy()
            : base()
        {
            this.Name = "RaphaelStrategy__StaticLogicName";
            this.Description = "Example";
            //TODO: non sto inserendo il bid ask type
        }

        #region Main Methods/Lifecycle
        //HACK seams useless
        protected override void OnCreated() { }
        protected override void OnRun()
        {
            this.readyToGo = false;
            this._Symbol.NewLast += this._Symbol_NewLast;
            this._Symbol.NewQuote += this._Symbol_NewQuote;
        }
        protected override void OnStop()
        {
            this.readyToGo = false;
            if (this.hd != null)
            {
                this.hd.NewHistoryItem -= this.Hd_NewHistoryItem;
                this.hd.VolumeAnalysisCalculationProgress.ProgressChanged -= this.VolumeAnalysisCalculationProgress_ProgressChanged;
            }
        }
        protected override void OnRemove()
        {
            TpSlManager<int>.Stop();
            //this.Conditionable.Close();
            //TODO Possibilita di flattare o simili
            try
            {
                Core.Instance.Symbols.FirstOrDefault(x => x.Name == "Whatever you Want");
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log(ex, loggingLevel: LoggingLevel.Error);
            }
            
        }
        #endregion

        #region events
        private void _Symbol_NewLast(Symbol symbol, Last last)
        {
            if (this.hd == null)
            {
                var time = last.Time;

                this.hd = this._Symbol.GetHistory(new HistoryRequestParameters()
                {
                    Aggregation = new HistoryAggregationTime(this._HdPeriod, HistoryType.Last),
                    FromTime = time.AddDays(-_HdRequireDais),
                    ToTime = default,
                    Symbol = this._Symbol,
                });

                try
                {
                    //Volume calculation Init
                    var x = this.hd.CalculateVolumeProfile(new VolumeAnalysisCalculationParameters()
                    {
                        CalculatePriceLevels = false,
                        DeltaCalculationType = _Symbol.DeltaCalculationType,
                    });

                    if (this.hd?.VolumeAnalysisCalculationProgress != null)
                    {
                        this.hd.VolumeAnalysisCalculationProgress.ProgressChanged += this.VolumeAnalysisCalculationProgress_ProgressChanged;
                    }
                }
                finally
                {
                    if (!this.readyToGo)
                        this.hd.NewHistoryItem += this.Hd_NewHistoryItem;
                    this._Symbol.NewLast -= this._Symbol_NewLast;
                }
            }
        }
        private void _Symbol_NewQuote(Symbol symbol, Quote quote)
        {
            if (this.hd == null)
            {
                var time = quote.Time;
                //var requiredDais = _HdRequireDais.TotalMinutes;


                this.hd = this._Symbol.GetHistory(new HistoryRequestParameters()
                {
                    Aggregation = new HistoryAggregationTime(this._HdPeriod, HistoryType.Last),
                    FromTime = time.AddDays(-_HdRequireDais),
                    ToTime = default,
                    Symbol = this._Symbol,
                });

                try
                {
                    //Volume Analisis data Calculation Init
                    var x = this.hd.CalculateVolumeProfile(new VolumeAnalysisCalculationParameters()
                    {
                        CalculatePriceLevels = false,
                        DeltaCalculationType = _Symbol.DeltaCalculationType,
                    });

                    if (this.hd?.VolumeAnalysisCalculationProgress != null)
                    {
                        this.hd.VolumeAnalysisCalculationProgress.ProgressChanged += this.VolumeAnalysisCalculationProgress_ProgressChanged;
                    }
                }
                finally
                {
                    if (!this.readyToGo)
                        this.hd.NewHistoryItem += this.Hd_NewHistoryItem;
                    this._Symbol.NewQuote -= this._Symbol_NewQuote;
                }
            }

        }
        private void VolumeAnalysisCalculationProgress_ProgressChanged(object sender, VolumeAnalysisTaskEventArgs e)
        {

            if (e.CalculationState == VolumeAnalysisCalculationState.Finished)
            {
                this.Log("Volumes Loading pRocess Completed", StrategyLoggingLevel.Info);
            }
           
        }
        private void Hd_NewHistoryItem(object sender, HistoryEventArgs e)
        {
            if (!this.readyToGo)
            {
                //Ichimoku = this.GenerateIndicator("IchiMTreTempi V.1");
                //Volume = this.GenerateIndicator("Volume");
                //var DeltaSettings = new List<SettingItem>()
                //{
                //     new SettingItemPeriod(name: "Moving Avarage Period", value: this._absorbtionPeriod),
                //     new SettingItemPeriod(name: "Std Period Avarage", value: this._absorbtionPeriod)
                //};

                var SmaSettings = new List<SettingItem>()
                {
                    new SettingItemInteger(name: "Period of Simple Moving Average", value: this._SmaPeriodInt),
                    new SettingItemObject(
                        name: "Sources prices for MA",
                        value: PriceType.Median
                    )
                };

                SMA = this.GenerateIndicator("Simple Moving Average", SmaSettings);


                //CumulativeAbsorbtion = this.GenerateIndicator("CumulativeAbsobtion", DeltaSettings);

                this.Conditionable = new RaphaelStrategy(this.SMA, this._StartPeriod.Duration, this._EndPeriod.Duration, this._Account, this._Symbol, 1);

                this.readyToGo = true;
            }
        }

        #endregion

        #region Utils
        private Indicator GenerateIndicator(string indi_names, IList<SettingItem> indi_settings = null)
        {
            if (this.hd == null)
                return null;

            Indicator resoult = null;
            try
            {
                var indInfo = Core.Instance.Indicators.All.First(x => x.Name == indi_names);
                Indicator indicator = Core.Instance.Indicators.CreateIndicator(indInfo);
                if (indi_settings != null) 
                    indicator.Settings = indi_settings;

                resoult = indicator;
                //HACK adding Indi Here
                this.hd.AddIndicator(indicator);
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log("Indicator Generation Failed", loggingLevel: LoggingLevel.Error);
                Core.Instance.Loggers.Log($"Failed with message : {ex.Message}", loggingLevel: LoggingLevel.Error);
            }
            return resoult;
        }

        //TODO Update Those Metrics
        protected override void OnInitializeMetrics(Meter meter)
        {
            base.OnInitializeMetrics(meter);

            meter.CreateObservableCounter("Balance", () => this._Account.Balance > 0 ? this._Account.Balance : 0);
            meter.CreateObservableCounter("FibRetrace Level", () => TryGetFibRet());
            //meter.CreateObservableCounter("Sma Level", () => TryGetSma());

        }

        private double TryGetSma()
        {
            if (this.SMA == null)
                return 0;
            else
            {
                try
                {
                    return this.SMA.GetValue();
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        private double TryGetFibRet()
        {
            if (this.Conditionable == null)
                return 0;

            try
            {
                var obj = (RaphaelStrategy)Conditionable;
                return obj.FibRetrace;
            }
            catch (Exception)
            {
                return 0;                
            }
        }
        #endregion
    }
}