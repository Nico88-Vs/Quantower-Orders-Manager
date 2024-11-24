using DivergentStrV0_1.C_Obj;
using DivergentStrV0_1.OperationSystem;
using DivergentStrV0_1.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TpSlManager;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Strategies
{
    #region Local TODO
    //TODO logic: impostare lo stop in profit in caso di cross slow >>> to check
    //TODO logic: chiudere posizioni opposte >>>>>> to check
    //TODO logic: evitare posizioni nella nuvola >>>> to check
    //TODO logic: annullare ordini per cross opposti prematuri
    //TODO logic: eventualmente spostare gli ingressi per situazioni strane >>>> to check
    #endregion

    internal class OnlyBuyAtLowStrategy : ConditionableBase<Indicator>
    {
        private Indicator _IchimokuIndicator;
        private Indicator _VolumIndicator;
        private bool _IchimanagerInitialized = false;
        public IchiManager _Ichimanager;
        private HistoricalData _Hd;
        private bool AllowToLong;
        private bool AllowToShort;
        private double SlPercent;
        private double TpPercent;
        private bool UsePositions;

        public OnlyBuyAtLowStrategy(Indicator ichichimokuIndicator, Indicator volumindicator, Account account, Symbol symbol, double quantity, double slPercent, double tppercent, int maxShortExpo = 1, int maxLongExpo = 1, bool useposition = true, bool allowshorts = true)
            : base(account, symbol, quantity,useposition, maxShortExpo, maxLongExpo, allowshorts)
        {
            AllowToLong = false;
            AllowToShort = false;
            _VolumIndicator = volumindicator;
            _IchimokuIndicator = ichichimokuIndicator;
            this.SlPercent = slPercent/100;
            this.TpPercent = tppercent/100;
            this.UsePositions = useposition;
            StartIchimoku(ichichimokuIndicator.HistoricalData);
            SetCondictionHolder();
            ManagerInit();
        }

        #region Overraides
        public override void Close()
        {
            base.Close();

            _Ichimanager.GapDetected -= _Ichimanager_GapDetected;
            _Ichimanager.CloudSeries.Cross -= CloudSeries_Cross;
        }
        public override void GetMetrics() => throw new NotImplementedException();
        public override void SetCondictionHolder() => CondictionHolder = CreateSlcondiction();
        public override void Trade(Side side, double price)
        {
            var placeHoldeReq = new PlaceOrderRequestParameters()
            {
                Account = Account,
                Symbol = Symbol,
                Side = side,
                Quantity = Quantity,
                OrderTypeId = Symbol.GetAlowedOrderTypes(OrderTypeUsage.All).FirstOrDefault(x => x.Usage == OrderTypeUsage.All && x.Behavior == OrderTypeBehavior.Limit).Id,
                TimeInForce = TimeInForce.Day,
                Price = price,
                Comment = "new order",
            };

            TpSlManager<Indicator>.PlaceOrder(placeHoldeReq);

            var openedOppoitePosition = TpSlManager<Indicator>.FindAllOpened(side == Side.Buy ? Side.Sell : Side.Buy);

            if (openedOppoitePosition.Any())
                foreach (var item in openedOppoitePosition)
                {
                    item.ClosePosition();
                }
        }
        public override void Update(object obj)
        {
            try
            {
                _Ichimanager.Update();
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log(ex.Message, LoggingLevel.Error);
            }

            if (TpSlManager<Indicator>.SlTpItems.Any(x => x.Status == PositionManagerStatus.PartialyFilled || x.Status == PositionManagerStatus.Filled))
            {
                var selected = TpSlManager<Indicator>.SlTpItems.Where(x => x.Status == PositionManagerStatus.PartialyFilled || x.Status == PositionManagerStatus.Filled).ToList();

                foreach (var item in selected)
                {
                    foreach (var or in item.SlItems)
                    {
                        //TODO: non funziona per ordini parzialmente fillati
                        if (or.Status == OrderStatus.Opened & obj is double)
                        {
                            this.CondictionHolder.Computator.UpdateOrder(this.UpdateOrder, new KeyValuePair<Order, double>(or, (double)obj), or);
                        }
                    }
                }
            }
        }
        #endregion

        #region Services
        public void StartIchimoku(HistoricalData hd)
        {
            _Ichimanager = new IchiManager(_IchimokuIndicator, hd);
            _Ichimanager.GapDetected += _Ichimanager_GapDetected;
            _Ichimanager.CloudSeries.Cross += CloudSeries_Cross;
            _IchimanagerInitialized = true;
            _Hd = hd;
        }
        #endregion

        #region Trade Events
        private void CloudSeries_Cross(object sender, CrossEvent e)
        {
            if (e.Args == EventCrosArg.Gold_midt || e.Args == EventCrosArg.Dead_mid)
            {
                var idx = this._Ichimanager.CloudSeries.MidTF.GetCorrectBuffer(this._Ichimanager.CloudSeries.TenkanPeriod); 
                var support = _IchimokuIndicator.GetValue(lineIndex: Convert.ToInt32(IchiLineIndex.Tenkan_Sen), offset:idx);
                var item = _Hd[0][PriceType.Close];
                var relatedColor = this._Ichimanager.CloudSeries.GetTradableCloud(TF.TimeFrame.Slow).Key.Color;

                switch (e.Args)
                {
                    case EventCrosArg.Gold_midt:
                        //HINT logic: annullare ordini per cross opposti prematuri
                        this.CancellUslessOrder(Side.Sell);
                        //TODO: setta l ingresso sulla media!!!!!!
                        var item2 = support < item ? support : item;
                        var price = e.Price > item2 ? item2 : e.Price;
                        if (AllowToLong & relatedColor != CloudColor.green)
                            Trade(Side.Buy, price);
                        break;
                    case EventCrosArg.Dead_mid:
                        //HINT logic: annullare ordini per cross opposti prematuri
                        this.CancellUslessOrder(Side.Buy);
                        var item3 = support > item ? support : item;
                        var shPrice = e.Price > item3 ? e.Price : item3;
                        if (AllowToShort & relatedColor != CloudColor.red)
                            Trade(Side.Sell, shPrice);
                        break;
                }
                AllowToLong = false;
                AllowToShort = false;
            }
        }

        private void CancellUslessOrder(Side side)
        {
            var list = TpSlManager<Indicator>.SlTpItems.Where(x => x.Side == side & x.Status == PositionManagerStatus.Placed).ToList();
            foreach (var item in list)
                item.ClosedAll();
        }
        
        private void _Ichimanager_GapDetected(object sender, GapEventArgs e)
        {
            if (_Ichimanager.CloudSeries.Scenario == IchimokuCloudScenario.STRONG_BULLISH)
                if (!AllowToShort)
                {
                    if (Computator.VolumeDetect(_VolumIndicator.LinesSeries.ToList()))
                        AllowToShort = true;
                }


            if (_Ichimanager.CloudSeries.Scenario == IchimokuCloudScenario.STRONG_BEARISH)
                if (!AllowToLong)
                {
                    if (Computator.VolumeDetect(_VolumIndicator.LinesSeries.ToList()))
                        AllowToLong = true;
                }
        }
        #endregion

        #region Delegates
        private SlTpCondictionHolder<Indicator> CreateSlcondiction()
        {
            // Inizializzazione corretta del delegato per SL
            SlTpCondictionHolder<Indicator>.DefineSl[] slDelegates = new SlTpCondictionHolder<Indicator>.DefineSl[]
            {
                GetSl
            };

            // Inizializzazione corretta del delegato per TP (usiamo un delegato vuoto o simile)
            SlTpCondictionHolder<Indicator>.DefineTp[] tpDelegates = new SlTpCondictionHolder<Indicator>.DefineTp[]
            {
                GeTp
            };
            SlTpCondictionHolder<Indicator> slh = new SlTpCondictionHolder<Indicator>(new Indicator[1] { _IchimokuIndicator }, new Indicator[1] { _IchimokuIndicator }, slDelegates, tpDelegates);
            return slh;
        }
        public double GetSl(Indicator indicator, string guid)
        {
            //recuperare la base della nuvola fast 
            //recuperare un target plausibile
            //modifiocare l ordine on run ......

            SlTpItems sltpitem = TpSlManager<Indicator>.SlTpItems.FirstOrDefault(x => x.Id == guid);
            Side s = sltpitem.Side;
            double resoult = 0;

            switch (s)
            {
                case Side.Buy:
                    resoult = sltpitem.EntryPrice * (1-SlPercent);
                    break;
                case Side.Sell:
                    resoult = sltpitem.EntryPrice * (1+SlPercent);
                    break;
            }

            return resoult;
        }
        public double GeTp(Indicator indicator, string guid)
        {
            SlTpItems sltpitem = TpSlManager<Indicator>.SlTpItems.FirstOrDefault(x => x.Id == guid);
            Side s = sltpitem.Side;
            double resoult = 0;

            switch (s)
            {
                case Side.Buy:
                    resoult = sltpitem.EntryPrice * (1+TpPercent);
                    break;
                case Side.Sell:
                    resoult = sltpitem.EntryPrice * (1-TpPercent);
                    break;
            }

            return resoult;
        }
        public double UpdateOrder(object price_side_dict)
        {
            double res = -1;

            if (price_side_dict is KeyValuePair<Order, double>)
            {
                var obj = (KeyValuePair<Order, double>)price_side_dict;

                res = obj.Key.Price;

                var cloud_idx = _Ichimanager.CloudSeries.GetTradableCloud(TF.TimeFrame.Slow);
                var cloud_idx_mid = _Ichimanager.CloudSeries.GetTradableCloud(TF.TimeFrame.Mid);

                double fast = cloud_idx.Key.FastValue[cloud_idx.Value];
                double fast_mid = cloud_idx_mid.Key.FastValue[cloud_idx_mid.Value];

                double slow = cloud_idx.Key.SlowValue[cloud_idx.Value];
                double slow_mid = cloud_idx_mid.Key.SlowValue[cloud_idx_mid.Value];

                double avg = (slow + fast) / 2; 
                double avg_mid =  (slow_mid + fast_mid )/2;

                switch (obj.Key.Side)
                {
                    case Side.Sell:
                        if (avg_mid > avg &
                            obj.Value > avg_mid)
                        {
                            var tempres = fast > slow ? slow : fast;
                            if (tempres > obj.Key.Price)
                                res = tempres;

                        }
                        break;

                    case Side.Buy:
                        if (avg_mid < avg &
                           obj.Value < avg_mid)
                        {
                            var tempres = fast < slow ? slow : fast;
                            if (tempres < obj.Key.Price)
                                res = tempres;

                        }
                        break;
                }

            }
            return res;
        }
        #endregion
    }
}

