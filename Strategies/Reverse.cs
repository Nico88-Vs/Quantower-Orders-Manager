using DivergentStrV0_1.C_Obj;
using DivergentStrV0_1.OperationSystem;
using DivergentStrV0_1.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TpSlManager;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Strategies
{
    #region Local TODO
    //TODO logic: usare solo gli high >>>>>>>>> ceck
    //TODO logic: non cancellare al cross della nuvola >>>>>>>>> done
    //TODO logic: evitare i vincoli sul colore deella slow >>>>>>>>> aborted
    //TODO logic: verificare la distanza della nuvola >>>>>>>>> ceck
    //TODO logic: un uscita al origine della slow se maggiore di x >>>>> HACK check
    //TODO logic: eliminare il fastidioso update >>>>  HACK sospeso
    //TODO logic: usare lo stop a 1 >>>> HACK: dai setting
    //TODO logic: usare una sola posizione >>>> HACK: dai setting
    //TODO logic: un uscita al cross della verde che entra nella nuvola se maggiore
    //di x con conseguente stop a 0 >>>> HACK: in progress
    //TODO logic: un uscita al segnale opposto , se innescato dal target
    //TODO logic: dare un po di respiro al ingresso
    //HACK trading: Testare ingressi su WAP o chiusura di candela del livello


    #endregion

    internal class Reverse : ConditionableBase<int>
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
        private Cloud SlowTradableCloud;

        public Reverse(Indicator ichichimokuIndicator, Indicator volumindicator, Account account, Symbol symbol, double quantity, double slPercent, double tppercent, int maxShortExpo = 1, int maxLongExpo = 1, bool useposition = true, bool allowshorts = true)
            : base(account, symbol, quantity, useposition, maxShortExpo, maxLongExpo, allowshorts)
        {
            AllowToLong = false;
            AllowToShort = false;
            _VolumIndicator = volumindicator;
            _IchimokuIndicator = ichichimokuIndicator;
            this.SlPercent = slPercent / 100;
            this.TpPercent = tppercent / 100;
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


            TpSlManager<int>.PlaceOrder(placeHoldeReq);


            var openedOppoitePosition = TpSlManager<int>.FindAllOpened(side == Side.Buy ? Side.Sell : Side.Buy);

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

            if (TpSlManager<int>.SlTpItems.Any(x => x.Status == PositionManagerStatus.PartialyFilled || x.Status == PositionManagerStatus.Filled))
            {
                var selected = TpSlManager<int>.SlTpItems.Where(x => x.Status == PositionManagerStatus.PartialyFilled || x.Status == PositionManagerStatus.Filled).ToList();

                foreach (var item in selected)
                {
                    foreach (var or in item.SlItems)
                    {
                        //TODO: non funziona per ordini parzialmente fillati
                        //HINT: sospeso per numero di ordini
                        if (or.Status == OrderStatus.Opened & obj is double)
                        {
                            //this.CondictionHolder.Computator.UpdateOrder(this.UpdateOrder, new KeyValuePair<Order, double>(or, (double)obj), or);
                        }
                    }
                }
            }
        }
        #endregion

        #region Services
        private void CloseOpposiTeTrades(Side s)
        {
            if (TpSlManager<int>.SlTpItems.Any(x => x.Status == PositionManagerStatus.PartialyFilled || x.Status == PositionManagerStatus.Filled))
            {
                var filtered = TpSlManager<int>.SlTpItems.Where(x => x.Status == PositionManagerStatus.PartialyFilled || x.Status == PositionManagerStatus.Filled).ToList();
                var selected = filtered.Where(x => x.Side == s).ToList();
                foreach (var item in selected)
                {
                    var position = Core.Instance.Positions.FirstOrDefault(x => x.Id == item.RelatedPosition.Id);

                    if ( position != null)
                    {
                        var res = position.Close();
                    }
                }
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

        /// <summary>
        /// Cancella gli ordini non fillati in base al side
        /// </summary>
        /// <param name="side"></param>
        private void CancellUslessOrder(Side side)
        {
            try
            {
                var list = TpSlManager<Cloud>.SlTpItems.Where(x => x.Side == side & x.Status == PositionManagerStatus.Placed).ToList();
                if (list.Count > 0)
                {
                    foreach (var item in list)
                        item.ClosedAll();
                }
                
            }
            catch (Exception)
            {

                throw;
            }
            
        }

        #endregion

        #region Trade Events
        private void CloudSeries_Cross(object sender, CrossEvent e)
        {
            if (e.Args == EventCrosArg.Gold_midt || e.Args == EventCrosArg.Dead_mid)
            {
                //TODO: insert a try 
                var bestprice = 0.0;
                var cloud = this._Ichimanager.CloudSeries.CloudsMid[this._Ichimanager.CloudSeries.CloudsMid.Count - 2];
                var tradable_Cloud = this._Ichimanager.CloudSeries.GetTradableCloud(TF.TimeFrame.Slow);
                var spessore = tradable_Cloud.Key.ThickList[tradable_Cloud.Value];
                var delta = Math.Abs(e.Price - tradable_Cloud.Key.FastValue[tradable_Cloud.Value]);
                bool isdeltamax = delta > Math.Abs(spessore);
                bool convinient = false;
                var price = this._Hd[0][PriceType.Close];

                switch (e.Args)
                {
                    case EventCrosArg.Gold_midt:
                        bestprice = this._Ichimanager.CloudSeries.MidCloudDictionary[cloud].Select(x => x.OriginPrice).ToList().Min();
                        convinient = (bestprice > 0 & bestprice < e.Price);
                        
                        if (AllowToLong & tradable_Cloud.Key.Color != CloudColor.green & isdeltamax & convinient)
                        {
                            this.SlowTradableCloud = tradable_Cloud.Key;
                            if (bestprice > 0 & bestprice < price)
                            {
                                Trade(Side.Buy, bestprice);
                                this.CancellUslessOrder(Side.Sell);
                            }
                        }
                        break;
                    case EventCrosArg.Dead_mid:
                        bestprice = this._Ichimanager.CloudSeries.MidCloudDictionary[cloud].Select(x => x.OriginPrice).ToList().Max();
                        convinient = (bestprice > 0 & bestprice > e.Price);
                        
                        if (AllowToShort & tradable_Cloud.Key.Color != CloudColor.red & isdeltamax & convinient)
                        {
                            this.SlowTradableCloud = tradable_Cloud.Key;
                            if (bestprice > 0 & bestprice > price)
                            {
                                Trade(Side.Sell, bestprice);
                                this.CancellUslessOrder(Side.Buy);
                            }
                        }
                        break;
                }
                AllowToLong = false;
                AllowToShort = false;
            }
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
        private SlTpCondictionHolder<int> CreateSlcondiction()
        {
            // Inizializzazione corretta del delegato per SL
            SlTpCondictionHolder<int>.DefineSl[] slDelegates = new SlTpCondictionHolder<int>.DefineSl[]
            {
                GetSl
            };

            // Inizializzazione corretta del delegato per TP (usiamo un delegato vuoto o simile)
            SlTpCondictionHolder<int>.DefineTp[] tpDelegates = new SlTpCondictionHolder<int>.DefineTp[]
            {
                GeTp,
                GeTp,
                GeTp
            };
            SlTpCondictionHolder<int> slh = new SlTpCondictionHolder<int>(new int[1] { 0 }, new int[3] { 0,1,2 }, slDelegates, tpDelegates);
            return slh;
        }
        public double GetSl(int arg, string guid)
        {
            SlTpItems sltpitem = TpSlManager<int>.SlTpItems.FirstOrDefault(x => x.Id == guid);
            Side s = sltpitem.Side;
            double resoult = 0;

            switch (s)
            {
                case Side.Buy:
                    resoult = sltpitem.EntryPrice * (1 - SlPercent);
                    break;
                case Side.Sell:
                    resoult = sltpitem.EntryPrice * (1 + SlPercent);
                    break;
            }

            return resoult;
        }
        public double GeTp(int arg, string guid)
        {
            //TODO logic: un uscita al origine della slow se maggiore di x >>>>> HACK InProgress, senza verifica
            SlTpItems sltpitem = TpSlManager<int>.SlTpItems.FirstOrDefault(x => x.Id == guid);
            Side s = sltpitem.Side;
            double defoult = 0;
            double value = 0;

            switch (s)
            {
                case Side.Buy:
                    defoult = sltpitem.EntryPrice * (1 + SlPercent);
                    break;
                case Side.Sell:
                    defoult = sltpitem.EntryPrice * (1 - SlPercent);
                    break;
            }

            switch (arg)
            {
                case 0:
                    value = this.SlowTradableCloud.OriginPrice;
                    break;
                case 1:
                    value = defoult;
                    break;
                case 2:
                    value = s == Side.Buy ? this.Symbol.High : this.Symbol.Low;
                    break;
                default:
                    value = defoult;
                    break;
            }

            return value;
        }
        /// <summary>
        /// Update current Sl tp orders
        /// </summary>
        /// <param name="price_side_dict"></param>
        /// <returns></returns>
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
                double avg_mid = (slow_mid + fast_mid) / 2;

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
