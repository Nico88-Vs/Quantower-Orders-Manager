// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml.Linq;
using DivergentStrV0_1.C_Obj;
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
        public int _HdRequireDais = 1;
        [InputParameter("Tick delay", 3, 0, 100, increment: 1)]
        public int entry_tick_delay = 5;
        [InputParameter("Absorbtion Period", 4)]
        public Period _absorbtionPeriod = Period.MIN30;

        OrderManager OrderPlacingManager { get; set; }
        CloudSeries CloudSeries { get; set; }
        HistoricalData hd;
        HistoryType _historyType;
        bool Hdinitilized = false;
        Indicator Ichimoku;
        Indicator Volume;
        Indicator CumulativeAbsorbtion;
        bool Trada = false;
        int signCount_Long;
        int signCount_Short;
        bool inLong = false;
        bool inShort = false;
        Order currentLongOrder;
        Order currentShortOrder;
        Position currentLongPosition;
        Position currentShortPosition;
        Trade currentLongTrade;
        Trade currentShorTrade;
        int longCount = 0;
        int shortCount = 0;


        List<TF> tFs;


        double procesPercent => this.hd != null &&
                              this.hd.VolumeAnalysisCalculationProgress != null ? this.hd.VolumeAnalysisCalculationProgress.ProgressPercent : 0;
        private bool readyToGo;
        private bool volumesLoaded => this.hd != null &&
                              this.hd.VolumeAnalysisCalculationProgress != null &&
                              this.hd.VolumeAnalysisCalculationProgress.ProgressPercent == 100;
        #endregion

        private int commutateBool(bool positionSide)
        {
            return positionSide == true ? 1 : -1;
        }
            

        public DivergentStrV0_1()
            : base()
        {
            this.Name = "IChiStrV0_1";
            this.Description = "Gap Divergency ichi levels";
            //TODO: non sto inserendo il bid ask type
        }

        #region Main Methods/Events
        //HACK seams useless
        protected override void OnCreated() { }
        protected override void OnRun()
        {
            this.signCount_Short = 0;
            this.signCount_Long = 0;

            this.readyToGo = false;
            Computator.TradeDetected += this.Computator_TradeDetected;
            this._Symbol.NewLast += this._Symbol_NewLast;
            this._Symbol.NewQuote += this._Symbol_NewQuote;
            Core.Instance.PositionAdded += Instance_PositionAdded;
            Core.Instance.PositionRemoved += this.Instance_PositionRemoved;
            Core.Instance.OrderAdded += this.Instance_OrderAdded;
            Core.Instance.TradeAdded += this.Instance_TradeAdded;
        }

        private void Instance_TradeAdded(Trade obj)
        {
            if (obj.PositionImpactType == PositionImpactType.Open)
            {
                if (obj.Side == Side.Buy)
                {
                    this.currentLongOrder = Core.Instance.Orders.FirstOrDefault(x => x.Id == obj.Id);
                    this.currentLongTrade = obj;
                }
                else
                {
                    this.currentShortOrder = Core.Instance.Orders.FirstOrDefault(x => x.Id == obj.Id);
                    this.currentShorTrade = obj;
                }
            }
        }

        #endregion

        #region events

        private void Instance_OrderAdded(Order obj)
        {
            //TODO:sostituire bool con enum
            //if (obj.Side == Side.Buy)
            //{
            //    this.inLong = true;
            //}

            //if (obj.Side == Side.Sell)
            //{
            //    this.inShort = true;
            //}
        }
        private void Instance_PositionRemoved(Position obj)
        {
            var pnlTicks = obj.GrossPnLTicks;
            if (obj.Side == Side.Buy)
                this.inLong = false;

            if (obj.Side == Side.Sell)
                this.inShort = false;
        }
        private void Computator_TradeDetected(object sender, NewTradEventArg e)
        {
        }
        private void Instance_PositionAdded(Position obj)
        {
            //var s = Core.Instance.Orders;
            //var t = this.currentShortOrder;
            //var r = this.currentLongOrder;
            //var g = s.FirstOrDefault(x => x.Id == r.Id);
            //if (obj.Side == Side.Buy && this.currentLongOrder.PositionId == obj.Id)
            //{
            //    this.inLong = true;
            //    this.currentLongPosition = obj;
            //}
            
            //if (obj.Side == Side.Sell && this.currentShortOrder.PositionId == obj.Id)
            //{
            //   this.inShort = true;
            //   this.currentShortPosition = obj;
            //}
        }
        private void _Symbol_NewLast(Symbol symbol, Last last)
        {
            if (this.hd == null)
            {
                var time = last.Time;

                this.hd = this._Symbol.GetHistory(new HistoryRequestParameters()
                {
                    Aggregation = new HistoryAggregationTime(Period.MIN1),
                    FromTime = time.AddDays(-_HdRequireDais),
                    ToTime = default,
                    Symbol = this._Symbol,
                    HistoryType = HistoryType.Last
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

                    //HACK: questo history type e inutile
                    this._historyType = this.hd.HistoryType;
                    OrderPlacingManager = new OrderManager(this.hd.HistoryType, this.entry_tick_delay);
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
                    Aggregation = new HistoryAggregationTime(Period.MIN1),
                    FromTime = time.AddDays(-_HdRequireDais),
                    ToTime = default,
                    Symbol = this._Symbol,
                    HistoryType = HistoryType.Last
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

                    //HACK: questo history type e inutile
                    this._historyType = this.hd.HistoryType;
                    OrderPlacingManager = new OrderManager(this.hd.HistoryType, this.entry_tick_delay);
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
                var DeltaSettings = new List<SettingItem>()
                {
                     new SettingItemPeriod(name: "Moving Avarage Period", value: this._absorbtionPeriod),
                     new SettingItemPeriod(name: "Std Period Avarage", value: this._absorbtionPeriod)
                };

                CumulativeAbsorbtion = this.GenerateIndicator("CumulativeAbsobtion", DeltaSettings);

                tFs = new List<TF>();
                //TODO: Settings Hardcoded 
                TF fast = new TF(TF.TimeFrame.Fast, 1, Ichimoku, Convert.ToInt32(IchiLineIndex.Senkou_SpanA0), Convert.ToInt32(IchiLineIndex.Senkou_SpanB0));
                tFs.Add(fast);
                TF mid = new TF(TF.TimeFrame.Mid, 5, Ichimoku, Convert.ToInt32(IchiLineIndex.Senkou_SpanA), Convert.ToInt32(IchiLineIndex.Senkou_SpanB));
                tFs.Add(mid);
                TF slow = new TF(TF.TimeFrame.Slow, 30, Ichimoku, Convert.ToInt32(IchiLineIndex.Senkou_SpanA2), Convert.ToInt32(IchiLineIndex.Senkou_SpanB2));
                tFs.Add(slow);


                this.CloudSeries = new CloudSeries(this.hd, fast, mid, slow);

                this.CloudSeries.GenerateCloud(tFs);

                this.readyToGo = true;
            }

            foreach (TF tf in tFs)
                this.CloudSeries.Update(tf);

            //Thread.Sleep(200); Si potrebbero evitare computazioni volumetriche 
            var buy = Computator.ComputeSignon(this.Ichimoku.LinesSeries.ToList(), ref signCount_Long, Side.Buy);
            var sell = Computator.ComputeSignon(this.Ichimoku.LinesSeries.ToList(), ref signCount_Short, Side.Sell);

            object side_trada = buy != Signed.None & sell == Signed.None? Side.Buy : sell != Signed.None & buy == Signed.None? Side.Sell : null;

            if (this.volumesLoaded & side_trada != null)
            {
                Side s = (Side)side_trada;

                //if (s == Side.Buy && (this.CloudSeries.Scenario != IchimokuCloudScenario.STRONG_BEARISH && this.CloudSeries.Scenario != IchimokuCloudScenario.MODERATELY_BEARISH))
                //    return;
                //if (s == Side.Sell && (this.CloudSeries.Scenario != IchimokuCloudScenario.STRONG_BULLISH && this.CloudSeries.Scenario != IchimokuCloudScenario.MODERATELY_BULLISH))
                //    return;

                HistoricalData historicalData = (HistoricalData)sender;

                var items = new List<IHistoryItem>();
                for (int i = 1; i < 3; i++)
                    items.Add(historicalData[i]);

                //HINT:Sto usando gli item nella lista con indici 0 e 1
                if (Computator.VolumeDetect(Volume.LinesSeries.ToList()))
                {
                    //TODO:tenkanperiod hardcoded
                    var potential_tp = this.CloudSeries.Scenario == IchimokuCloudScenario.STRONG_BULLISH || this.CloudSeries.Scenario == IchimokuCloudScenario.MODERATELY_BULLISH || this.CloudSeries.Scenario == IchimokuCloudScenario.STRONG_BULLISH || this.CloudSeries.Scenario == IchimokuCloudScenario.MODERATELY_BEARISH ? this.CloudSeries.SlowTF.ReturnCurrent(cloudLineReference.fast, 26) : 0;
                    int x = Computator.DivergenceDetect(items);
                    Core.Instance.Loggers.Log($"New Trade almost detected {items[x][PriceType.Close]}");
                    if (x >= 0)
                    {
                        Core.Instance.Loggers.Log($"New Trade at price close {items[x][PriceType.Close]}");
                        this.TestTrade(s, items[0][PriceType.Close], items[0][PriceType.Low], potential_tp);
                    }

                }
            }



            #region old
            //double new_price = 0;
            //if (isGreen)
            //    new_price = this._Symbol.CalculatePrice(historyItem[PriceType.Open], this.entry_tick_delay);
            //else
            //    new_price = this._Symbol.CalculatePrice(historyItem[PriceType.Open], -this.entry_tick_delay);

            //var placeHoldeReq = new PlaceOrderRequestParameters()
            //{
            //    Account = this._Account,
            //    Symbol = this._Symbol,
            //    Side = isGreen ? Side.Sell : Side.Buy,
            //    //HACK: quantita randomica
            //    Quantity = 1,
            //    //HACK: interessante questo TimeInForce
            //    TimeInForce = TimeInForce.Day,
            //    Price = new_price,
            //};

            //if (OrderPlacingManager.Finished)
            //{
            //    OrderPlacingManager.LimitPrice = historyItem[PriceType.Open];
            //    OrderPlacingManager.PlaceNewOrder(placeHoldeReq);
            //}
            #endregion
        }
        protected override void OnStop()
        {
            this.readyToGo = false;
            if (this.hd != null)
            {
                this.hd.NewHistoryItem -= this.Hd_NewHistoryItem;
                this.hd.VolumeAnalysisCalculationProgress.ProgressChanged -= this.VolumeAnalysisCalculationProgress_ProgressChanged;
            }

            Computator.TradeDetected -= this.Computator_TradeDetected;
            Core.Instance.PositionAdded -= this.Instance_PositionAdded;
            Core.Instance.PositionRemoved -= this.Instance_PositionRemoved;
            Core.Instance.OrderAdded -= this.Instance_OrderAdded;

            if (this.OrderPlacingManager != null)
                this.OrderPlacingManager.Dispose();
        }
        protected override void OnRemove()
        {
            //TODO Possibilita di flattare o simili
        }
        #endregion

        #region utils
      
        private void TestTrade(Side side, double price, double Slprice, double tPrices = 0)
        {
            if (side == Side.Buy && this.inLong)
                return;
            
            if (side == Side.Sell && this.inShort)
                return;

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

            var resoult = Core.Instance.PlaceOrder(placeHoldeReq);

            if (resoult.Status != TradingOperationResultStatus.Success)
            {
                Core.Instance.Loggers.Log("Cazzo errore nel post order", LoggingLevel.Error);
            }
            else
            {
                if (side == Side.Buy)
                {
                    this.inLong = true;
                    this.signCount_Long = 0;
                    this.longCount++;
                }
                else if (side == Side.Sell)
                {
                    this.inShort = true;
                    this.signCount_Short = 0;
                    this.shortCount++;
                } 
            }
                
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
        #endregion

        //TODO Update Those Metrics
        protected override void OnInitializeMetrics(Meter meter)
        {
            base.OnInitializeMetrics(meter);
            
            meter.CreateObservableCounter("Balance", () => this._Account.Balance > 0 ? this._Account.Balance : 0);
            meter.CreateObservableCounter("LongCount", () => this.longCount > 0 ? this.longCount : 0);
            meter.CreateObservableCounter("ShortCount", () => this.shortCount > 0 ? this.shortCount : 0);
            meter.CreateObservableCounter("in Long", () => this.commutateBool(this.inLong) );
            meter.CreateObservableCounter("in short", () => this.commutateBool(this.inShort), description:"balala");
            
        }
    }
}