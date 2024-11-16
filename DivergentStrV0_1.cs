// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

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
using DivergentStrV0_1.C_Obj;
using DivergentStrV0_1.OperationSystem;
using DivergentStrV0_1.Strategies;
using DivergentStrV0_1.Utils;
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

    public class DivergentStrV0_1 : Strategy
    {
        #region Input / Attributi e campi
        [InputParameter("Symbol", 0)]
        public Symbol _Symbol = Core.Instance.Symbols.FirstOrDefault();
        [InputParameter("Account", 1)]
        public Account _Account;
        [InputParameter("HD Preload required Dais", 2)]
        public int _HdRequireDais = 31;
        [InputParameter("Tick delay", 3, 0, 100, increment: 1)]
        public int entry_tick_delay = 5;
        [InputParameter("Absorbtion Period", 4)]
        public Period _absorbtionPeriod = Period.MIN30;
        [InputParameter("Quantity", 5)]
        public double _Quantity = 1;
        [InputParameter("Max Short Expo", 6, minimum: 1, maximum: 10, decimalPlaces: 0)]
        public int _MaxShortExpo = 3;
        [InputParameter("Max Long Expo", 7, minimum: 1, maximum: 10, decimalPlaces: 0)]
        public int _MaxLongExpo = 3;

        public IchiManager IchiManager { get; set; }
        private HistoricalData hd;
        private Indicator Ichimoku;
        private Indicator Volume;
        private Indicator CumulativeAbsorbtion;
        private SlTpCondictionHolder<int> condiHolder { get; set; }
        private IConditionable Conditionable { get; set; }

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
            this.Name = "IChiStrV0_1";
            this.Description = "Gap Divergency ichi levels";
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

            if (this.Conditionable is OnlyBuyAtLowStrategy)
            {
                var temp = (OnlyBuyAtLowStrategy)this.Conditionable;
            }

            if (this.IchiManager != null)
            {
                this.IchiManager.Stop();
            }
        }
        protected override void OnRemove()
        {
            TpSlManager<int>.Stop();
            this.Conditionable.Close();
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
                    Aggregation = new HistoryAggregationTime(Period.MIN1, HistoryType.Last),
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

                    //this.condiHolder = this.createSlcondiction();
                    //TpSlManager<int>.init(this.condiHolder);
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
                    Aggregation = new HistoryAggregationTime(Period.MIN1, HistoryType.Last),
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
                Ichimoku = this.GenerateIndicator("IchiMTreTempi V.1");
                Volume = this.GenerateIndicator("Volume");

                this.Conditionable = new OnlyBuyAtLowStrategy(this.Ichimoku, this.Volume, this._Account, this._Symbol, this._Quantity /*maxLongExpo : this._MaxLongExpo, maxShortExpo: this._MaxShortExpo*/);

                this.readyToGo = true;
            }

            //this.IchiManager.Update();
            this.Conditionable.Update(null);
        }

        #endregion

        #region Utils

        private void TestTrade(Side side, double price, double Slprice, double tPrices = 0)
        {
            double _temp_ofset = price * 0.01;
            double _temp_price_tp = side == Side.Buy ? price * 1.01 : price * 0.99;
            double _temp_price_sl = side == Side.Buy ? price * 0.995 : price * 1.05;
            var sl = SlTpHolder.CreateSL(Slprice, isTrailing:true);
            var tp = SlTpHolder.CreateTP(_temp_price_tp);

            if (tPrices != 0)
                tp = SlTpHolder.CreateTP(tPrices);


            //HINT: gestione del lotto minimo e harcode delle quantita
            var quantity = 0.5 < this._Symbol.MinLot ? this._Symbol.MinLot : 0.5; 
             
            var placeHoldeReq = new PlaceOrderRequestParameters()
            {
                Account = this._Account,
                Symbol = this._Symbol,
                Side = side,
                Quantity = quantity,
                OrderTypeId = this._Symbol.GetAlowedOrderTypes(OrderTypeUsage.All).FirstOrDefault(x => x.Usage == OrderTypeUsage.All && x.Behavior == OrderTypeBehavior.Limit).Id,
                TimeInForce = TimeInForce.Day,
                Price = price,
                StopLoss = sl,
                TakeProfit = tp,
                Comment = "new order",
            };

            TpSlManager<int>.PlaceOrder(placeHoldeReq);

        }

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

        //private SlTpCondictionHolder<int> createSlcondiction()
        //{
        //    // Inizializzazione corretta del delegato per SL
        //    SlTpCondictionHolder<int>.DefineSl[] slDelegates = new SlTpCondictionHolder<int>.DefineSl[]
        //    {
        //        this.GetSlTp
        //    };

        //    // Inizializzazione corretta del delegato per TP (usiamo un delegato vuoto o simile)
        //    SlTpCondictionHolder<int>.DefineTp[] tpDelegates = new SlTpCondictionHolder<int>.DefineTp[]
        //    {
        //        this.GetSlTp
        //    };
        //    SlTpCondictionHolder<int> slh = new SlTpCondictionHolder<int>(new int[1] { 0 }, new int[1] { 0 }, slDelegates, tpDelegates);
        //    return slh;
        //}

        //public double GetSlTp(int lineseriesIndex)
        //{
        //    return this.Ichimoku.GetValue(lineIndex: lineseriesIndex);
        //}

        //TODO Update Those Metrics
        protected override void OnInitializeMetrics(Meter meter)
        {
            base.OnInitializeMetrics(meter);

            meter.CreateObservableCounter("Balance", () => this.Conditionable != null ? this.Conditionable.Account.Balance : -1);
            meter.CreateObservableCounter("Long on Run", () => this.Conditionable != null ? this.Conditionable.LongOpenCount : -1);
            meter.CreateObservableCounter("Net Profit", () => this.Conditionable != null & this.Conditionable.NetProfit > 0 ? this.Conditionable.NetProfit : -1);
            meter.CreateObservableCounter("LongCount", () => this.Conditionable != null ? this.Conditionable.LongCount : -1);
            meter.CreateObservableCounter("ShortCount", () => this.Conditionable != null ? this.Conditionable.ShortCount : -1);
            meter.CreateObservableCounter("PositiveShortCount", () => this.Conditionable != null ? this.Conditionable.ShortProfittableCount : -1);
            meter.CreateObservableCounter("PositiveLongCount", () => this.Conditionable != null ? this.Conditionable.LongProfittableCount : -1);
            meter.CreateObservableCounter("Short on Run", () => this.Conditionable != null & this.Conditionable.ShortOpenCount > 0 ? this.Conditionable.ShortOpenCount : -1);

        }
        #endregion
    }
}