using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TpSlManager;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1
{
    public class FirstStrategyCondiction : ConditionableBase<Indicator>
    {
        public override double NetProfit { get => TpSlManager<Indicator>.NetProfit;}
        public override double LongCount { get => TpSlManager<Indicator>.LongCount;}
        public override double ShortCount { get => TpSlManager<Indicator>.ShortCount;}
        public override int LongExpo { get => TpSlManager<Indicator>.LongExpo;}
        public override int ShortExpo { get => TpSlManager<Indicator>.ShortExpo;}
        public override string ConditionName { get; } = "First Condictional Strategy";
        public override string Description { get; } = "Trade Gaps targetting first ichi Cloud Lines";
        public override SlTpCondictionHolder<Indicator> CondictionHolder { get => _CondictionHolder;}
        public override Account Account => base.Account;
        public override Symbol Symbol => base.Symbol;
        public override double Quantity => base.Quantity;
        public override int MaxShortExo => base.MaxShortExo;
        public override int MaxLongExo => base.MaxLongExo;

        private SlTpCondictionHolder<Indicator> _CondictionHolder;
        private Indicator _IchimokuIndicator;
        private bool _IchimanagerInitialized = false;
        private IchiManager _Ichimanager;
        private HistoricalData _Hd;

        public FirstStrategyCondiction(Indicator ichichimokuIndicator, Account account, Symbol symbol, double quantity, int maxShortExpo = 1, int maxLongExpo = 1) 
            : base(account , symbol, quantity, maxShortExpo, maxLongExpo)
        {
            this._IchimokuIndicator = ichichimokuIndicator;
            this.StartIchimoku(ichichimokuIndicator.HistoricalData);
            this.SetCondictionHolder();
            base.ManagerInit();
        }

        public override void Close()
        {
            this._Ichimanager.GapDetected -= this._Ichimanager_GapDetected;
            base.Close();

        }
        public override void GetMetrics() => throw new NotImplementedException();
        public override void SetCondictionHolder() => this._CondictionHolder = CreateSlcondiction();
        public override void Trade(Side side, double price)
        {
            var placeHoldeReq = new PlaceOrderRequestParameters()
            {
                Account = this.Account,
                Symbol = this.Symbol,
                Side = side,
                Quantity = Quantity,
                OrderTypeId = this.Symbol.GetAlowedOrderTypes(OrderTypeUsage.All).FirstOrDefault(x => x.Usage == OrderTypeUsage.All && x.Behavior == OrderTypeBehavior.Limit).Id,
                TimeInForce = TimeInForce.Day,
                Price = price,
                Comment = "new order",
            };

            TpSlManager<Indicator>.PlaceOrder(placeHoldeReq);
        }

        public override void Update(object obj)
        {
            this._Ichimanager.Update();

            if (TpSlManager<Indicator>.SlTpItems.Count > 0)
                this.CondictionHolder.Computator.UpdateOrder(TpSlManager<Indicator>.SlTpItems);
        }

        public void StartIchimoku(HistoricalData hd)
        {
            this._Ichimanager = new IchiManager(this._IchimokuIndicator, hd);
            this._IchimanagerInitialized = true;
            this._Ichimanager.GapDetected += this._Ichimanager_GapDetected;
            this._Hd = hd;
        }

        private void _Ichimanager_GapDetected(object sender, C_Obj.GapEventArgs e)
        {
            this.Trade(e.Side, this._Hd[0][PriceType.Close]);
        }

        private SlTpCondictionHolder<Indicator> CreateSlcondiction()
        {
            // Inizializzazione corretta del delegato per SL
            SlTpCondictionHolder<Indicator>.DefineSl[] slDelegates = new SlTpCondictionHolder<Indicator>.DefineSl[]
            {
                this.GetSl
            };

            // Inizializzazione corretta del delegato per TP (usiamo un delegato vuoto o simile)
            SlTpCondictionHolder<Indicator>.DefineTp[] tpDelegates = new SlTpCondictionHolder<Indicator>.DefineTp[]
            {
                this.GeTp
            };
            SlTpCondictionHolder<Indicator> slh = new SlTpCondictionHolder<Indicator>(new Indicator[1] { this._IchimokuIndicator }, new Indicator[1] { _IchimokuIndicator }, slDelegates, tpDelegates);
            return slh;
        }

        public double GetSl(Indicator indicator, string guid)
        {
            SlTpItems sltpitem = TpSlManager<Indicator>.SlTpItems.FirstOrDefault(x => x.Id == guid);
            Side s = sltpitem.Side;
            List<double> ls = new List<double>();
            double resoult = 0;

            foreach (LineSeries line in indicator.LinesSeries)
            {
                if (Computator.CloudLineIndex.Contains(Array.IndexOf(indicator.LinesSeries, line)))
                {
                    ls.Add(line.GetValue());
                }
               
            }

            switch (s)
            {
                case Side.Buy:
                    resoult = this.GetClosest(ls, sltpitem.EntryPrice, true);
                    break;
                case Side.Sell:
                    resoult = this.GetClosest(ls, sltpitem.EntryPrice, false);
                    break;
            }

            return resoult;
        }

        public double GeTp(Indicator indicator, string guid)
        {
            SlTpItems sltpitem = TpSlManager<Indicator>.SlTpItems.FirstOrDefault(x => x.Id == guid);
            Side s = sltpitem.Side;
            List<double> ls = new List<double>();
            double resoult = 0;

            foreach (LineSeries line in indicator.LinesSeries)
            {
                if (Computator.CloudLineIndex.Contains(Array.IndexOf(indicator.LinesSeries, line)))
                {
                    ls.Add(line.GetValue());
                }

            }

            switch (s)
            {
                case Side.Buy:
                    resoult = this.GetClosest(ls, sltpitem.EntryPrice, false);
                    break;
                case Side.Sell:
                    resoult = this.GetClosest(ls, sltpitem.EntryPrice, true);
                    break;
            }

            return resoult;
        }

        private double GetClosest(List<double> ls, double entryprice, bool isDown)
        {
            List<double> selected = new List<double>();

            if (isDown)
            {
                selected = ls.Where(x => x < entryprice).ToList();
                selected.OrderByDescending(x => x);
            }
            else
            {
                ls.Where(x => x > entryprice).ToList();
                selected.OrderBy(x => x);
            }

            if (selected.Count > 0) 
                return selected.First();
            else
            {
                if (isDown) return entryprice * 0.99;
                else return entryprice * 1.01;
            }
        }
    }
}
