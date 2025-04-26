// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using DivergentStrV0_1.OperationSystemAdv;
using DivergentStrV0_1.Strategies;
using DivergentStrV0_1.Utils;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1
{

    #region Kaci Imput Parameters

    //Custom attributes for visual grouping in the settings
    //==================================================================
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class VisualGroupAttribute : Attribute
    {
        public string GroupName { get; }

        public VisualGroupAttribute(string groupName)
        {
            GroupName = groupName;
        }
    }
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class SeparatorGroupAttribute : Attribute
    {
        public string GroupName { get; }

        public SeparatorGroupAttribute(string groupName)
        {
            GroupName = groupName;
        }
    }
    #endregion
    //TODO trailing Stop 
    //TODO ichimoku target
    //TODO gestire stop dinamici 
    //TODO tracciare gli incroci come e livelli 
    //TODO altro ancora 
    //TODO creare e utilizzare una libreria dedicata a ichi

    public class DivergentStrV0_1 : Strategy
    {
        #region Input / Attributi e campi
        [VisualGroup("Enviroment Settings")]    
        [InputParameter("Symbol", 0)]
        public Symbol _Symbol;

        [VisualGroup("Enviroment Settings")]
        [InputParameter("Account", 1)]
        public Account _Account;

        [SeparatorGroup("Strategy Settings")]

        [VisualGroup("Strategy Settings")]
        [InputParameter("Tick delay", 2, 0, 100, increment: 1)]
        public int entry_tick_delay = 5;

        [VisualGroup("Strategy Settings")]
        [InputParameter("Quantity", 3)]
        public double _Quantity = 1;

        [VisualGroup("Strategy Settings")]
        [InputParameter("Max Short Expo", 4, minimum: 1, maximum: 10, decimalPlaces: 0)]
        public int _MaxShortExpo = 3;

        [VisualGroup("Strategy Settings")]
        [InputParameter("Max Long Expo", 5, minimum: 1, maximum: 10, decimalPlaces: 0)]
        public int _MaxLongExpo = 3;

        [VisualGroup("Strategy Settings")]
        [InputParameter("Allow Shorts", 6)]
        public bool _AllowShorts = true;

        [VisualGroup("Strategy Settings")]
        [InputParameter("Sl Percentage", 7, 0.5, 200, 0.1)]
        public double _SlPercent = 1.00;

        [VisualGroup("Strategy Settings")]
        [InputParameter("Tp Percentage", 8, 0.7, 200, 0.1)]
        public double _TPercentage = 2.5;

        [SeparatorGroup("HD Settings")]

        [VisualGroup("HD Settings")]
        [InputParameter("From Time", 9, 0.7, 200, 0.1)]
        public DateTime _fomTime ;

        [VisualGroup("HD Settings")]
        [InputParameter("HD Preload required Dais", 10)]
        public int _HdRequireDais = 31;


        public IchiManager IchiManager { get; set; }
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
            //TODO: non sto inserendo il bid ask type
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
        protected override void OnStop()
        {

            #region 🧯 DEPRECATED [Da rimuovere nella prossima versione]
            /*
             * ⚠️ Questo blocco è deprecato
             * if (this.hd != null)
            {
                this.hd.NewHistoryItem -= this.Hd_NewHistoryItem;
                this.hd.VolumeAnalysisCalculationProgress.ProgressChanged -= this.VolumeAnalysisCalculationProgress_ProgressChanged;
            }

            if (this.IchiManager != null)
            {
                this.IchiManager.Stop();
            }
             * TODO: sostituire o rimuovere
             */
            #endregion

            this.readyToGo = false;
            
        }
        protected override void OnRemove()
        {
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

        #region 🧯 DEPRECATED [Da rimuovere nella prossima versione]
        /*
         * ⚠️ Questo blocco è deprecato
         * private void _Symbol_NewLast(Symbol symbol, Last last)
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
           
            else
            {
                var trade = this.hd[1][PriceType.Open] < this.hd[1][PriceType.Close] ? true : false;

                if (this._conditionable.Metrics.Exposed)
                    trade = false;

                this._conditionable.Update(new TradeData(trade, this.hd[0][PriceType.Open]));
            }
        }
         * TODO: sostituire o rimuovere
         */
        #endregion




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


        //TODO Update Those Metrics
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

            ////HACK:remove this debug val 
            //meter.CreateObservableGauge("abb", () => this._Account.Balance, "description");
        }
        #endregion


        #region Kaci Imput Parameters
        public void PerformSettingItemsGroup(ref IList<SettingItem> settings)
        {
            var membersWithAttribute = GetType().GetMembers()
                .Where(member => member.GetCustomAttributes(typeof(Attribute), false).Length > 0)
                .ToList();

            foreach (var member in membersWithAttribute)
            {
                var attribute = member.GetCustomAttributes<Attribute>();

                // Visual group
                if (attribute.Any(a => a is VisualGroupAttribute))
                {
                    var visualGroupAttribute = attribute.First(a => a is VisualGroupAttribute) as VisualGroupAttribute;
                    if (visualGroupAttribute != null)
                    {
                        var memberInputPrameter = attribute.FirstOrDefault(a => a is InputParameterAttribute) as InputParameterAttribute;
                        var memberDescription = memberInputPrameter?.Name;
                        SettingItemVisualGroup vgroup = new SettingItemVisualGroup(visualGroupAttribute.GroupName);
                        // Find the corresponding SettingItem in the settings list
                        SettingItem item = settings.FirstOrDefault(x => x.Name == memberDescription);
                        if (item != null)
                        {
                            item.VisualGroup = vgroup;
                        }
                    }

                }
                // Separator group
                else if (attribute.Any(a => a is SeparatorGroupAttribute))
                {
                    var separatorGroupAttribute = attribute.First(a => a is SeparatorGroupAttribute) as SeparatorGroupAttribute;
                    if (separatorGroupAttribute != null)
                    {
                        var memberInputPrameter = attribute.FirstOrDefault(a => a is InputParameterAttribute) as InputParameterAttribute;
                        var memberDescription = memberInputPrameter?.Name;
                        SettingItemSeparatorGroup sgroup = new SettingItemSeparatorGroup(separatorGroupAttribute.GroupName);
                        // Find the corresponding SettingItem in the settings list
                        SettingItem item = settings.FirstOrDefault(x => x.Name == memberDescription);
                        if (item != null)
                        {
                            item.SeparatorGroup = sgroup;
                        }
                    }

                }


            }
        }

        public override IList<SettingItem> Settings
        {
            get
            {
                IList<SettingItem> settings = base.Settings;

                PerformSettingItemsGroup(ref settings);

                return settings;
            }
            set
            {
                base.Settings = value;
            }
        }

        #endregion
    }
}