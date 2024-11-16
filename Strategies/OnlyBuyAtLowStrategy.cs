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
    internal class OnlyBuyAtLowStrategy : ConditionableBase<Indicator>
    {
        private Indicator _IchimokuIndicator;
        private Indicator _VolumIndicator;
        private bool _IchimanagerInitialized = false;
        public IchiManager _Ichimanager;
        private HistoricalData _Hd;
        private bool AllowToLong;
        private bool AllowToShort;

        public OnlyBuyAtLowStrategy(Indicator ichichimokuIndicator, Indicator volumindicator, Account account, Symbol symbol, double quantity, int maxShortExpo = 1, int maxLongExpo = 1)
            : base(account, symbol, quantity, maxShortExpo, maxLongExpo)
        {
            AllowToLong = false;
            AllowToShort = false;
            _VolumIndicator = volumindicator;
            _IchimokuIndicator = ichichimokuIndicator;
            StartIchimoku(ichichimokuIndicator.HistoricalData);
            SetCondictionHolder();
            ManagerInit();
        }
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
        }
        public override void Update(object obj)
        {
            //TODO_ logic: get the TRADE  permission from sentiment >>>> from status
            //TODO_ logic: get permission from cloud gaps >>>> SUBSRIBING eVENTS
            //TODO_ logic: wait new mid cross >>>>> 
            //TODO_ logic: enter the order


            try
            {
                _Ichimanager.Update();
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log(ex.Message, LoggingLevel.Error);
            }
        }
        public void StartIchimoku(HistoricalData hd)
        {
            _Ichimanager = new IchiManager(_IchimokuIndicator, hd);
            _Ichimanager.GapDetected += _Ichimanager_GapDetected;
            _Ichimanager.CloudSeries.Cross += CloudSeries_Cross;
            _IchimanagerInitialized = true;
            _Hd = hd;
        }
        private void CloudSeries_Cross(object sender, CrossEvent e)
        {
            if (e.Args == EventCrosArg.Gold_midt || e.Args == EventCrosArg.Dead_mid)
            {
                var support = _IchimokuIndicator.GetValue(lineIndex: Convert.ToInt32(IchiLineIndex.Tenkan_Sen));
                var item = _Hd[0][PriceType.Close];

                switch (e.Args)
                {
                    //TODO: senza un controllo rispetto alla posizione della nuvola rischio di fillare ordini fuori mercato!!!!
                    //HACK: per ora compro a mercato se il prezzo e a sfavore!!!!
                    //TODO: Cancellare ordini pendenti non fillati!!!!

                    case EventCrosArg.Gold_midt:
                        //TODO: setta l ingresso sulla media!!!!!!
                        var item2 = support < item ? support : item;
                        var price = e.Price > item2 ? item2 : e.Price;
                        if (AllowToLong)
                            Trade(Side.Buy, price);
                        break;
                    case EventCrosArg.Dead_mid:
                        var item3 = support > item ? support : item;
                        var shPrice = e.Price > item3 ? e.Price : item3;
                        if (AllowToShort)
                            Trade(Side.Sell, shPrice);
                        break;
                }
                AllowToLong = false;
                AllowToShort = false;
            }
        }
        private void _Ichimanager_GapDetected(object sender, GapEventArgs e)
        {
            if (_Ichimanager.CloudSeries.Scenario == IchimokuCloudScenario.STRONG_BULLISH &&
                _Ichimanager.CloudSeries.CurrentMidCloud.Color != CloudColor.red)
                if (!AllowToShort)
                {
                    if (Computator.VolumeDetect(_VolumIndicator.LinesSeries.ToList())) ;
                    AllowToShort = true;
                }


            if (_Ichimanager.CloudSeries.Scenario == IchimokuCloudScenario.STRONG_BEARISH &&
                _Ichimanager.CloudSeries.CurrentMidCloud.Color != CloudColor.green)
                if (!AllowToLong)
                {
                    if (Computator.VolumeDetect(_VolumIndicator.LinesSeries.ToList()))
                        AllowToLong = true;
                }
        }
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
                    resoult = sltpitem.EntryPrice * 0.99;
                    break;
                case Side.Sell:
                    resoult = sltpitem.EntryPrice * 1.01;
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
                    resoult = sltpitem.EntryPrice * 1.01;
                    break;
                case Side.Sell:
                    resoult = sltpitem.EntryPrice * 0.99;
                    break;
            }

            return resoult;
        }
    }
}

