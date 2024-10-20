using DivergentStrV0_1.OrdersManagerClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TpSlManager;
using TradingPlatform.BusinessLayer;

namespace CondictionalStrategyExample
{
    
    public enum RaplaelStrategyStatus
    {
        waitingFibonacci,
        trade
    }
    public class RaphaelStrategy : ConditionableBase<Indicator>
    {
        public static string StaticLogicName = "RaplaelStrategy";
        public static string StaticDesription = "it' s too long";
        private HistoricalData HD {  get; set; }
        private TimeSpan StarTimeOfDay {  get; set; }
        private TimeSpan EndTimeOfDay {  get; set; }
        public Indicator SmaIndicator { get; set; }
        private RaplaelStrategyStatus _Status = RaplaelStrategyStatus.waitingFibonacci;
        private double _FibRetracPercent;
        public double FibRetrace { get; set; }
        private bool _TradeShort;
        
        public RaphaelStrategy(Indicator smaIndi, TimeSpan start, TimeSpan end, Account account, Symbol symbol, double quantity, int maxShortExpo = 1, int maxLongExpo = 1, double fibRetracePercent = 0.5, bool tradeShort = false)
             : base(account, symbol, quantity, maxShortExpo, maxLongExpo)
        {
            this._TradeShort = tradeShort;
            this.SmaIndicator = smaIndi;
            this.HD = this.SmaIndicator.HistoricalData;
            this._FibRetracPercent = fibRetracePercent;
            this.StarTimeOfDay = start;
            this.EndTimeOfDay = end;
            this._TradeShort = tradeShort;
            ConditionName = StaticLogicName;
            Description = StaticDesription;

            try
            {
                this.CalulateFibRetrace(Core.Instance.TimeUtils.ConvertFromUTCToSelectedTimeZone(this.HD.Symbol.LastDateTime));
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log(ex.Message, LoggingLevel.Error);
            }

            this.HD.NewHistoryItem += this.HD_NewHistoryItem;

            this.SetCondictionHolder();

        }

        private void HD_NewHistoryItem(object sender, HistoryEventArgs e)
        {
            this.SetStatus();

            if (this._Status == RaplaelStrategyStatus.trade & this.FibRetrace > 0)
            {
                if (EvaluateTrade(this.HD[1], Side.Buy))
                    this.Trade(Side.Buy, e.HistoryItem[PriceType.Close]);
                if (this._TradeShort)
                    if (EvaluateTrade(this.HD[1], Side.Sell))
                        this.Trade(Side.Sell, e.HistoryItem[PriceType.Close]);
            }
        }
        public override void Close()
        {
            base.Close();
            this.HD.NewHistoryItem -= this.HD_NewHistoryItem;
        }
        public override bool Equals(object obj) => base.Equals(obj);
        public override int GetHashCode() => base.GetHashCode();
        public override void GetMetrics() => throw new NotImplementedException();
        public override void InitializeCondictionHolder(Indicator[] stopLossIndicators, Indicator[] takeProfitIndicators, SlTpCondictionHolder<Indicator>.DefineSl[] slDelegates, SlTpCondictionHolder<Indicator>.DefineTp[] tpDelegates) => base.InitializeCondictionHolder(stopLossIndicators, takeProfitIndicators, slDelegates, tpDelegates);
        public override void SetCondictionHolder() => base.InitializeCondictionHolder(new Indicator[1] { this.SmaIndicator }, new Indicator[1] { this.SmaIndicator }, new SlTpCondictionHolder<Indicator>.DefineSl[1] { this.GetSl }, new SlTpCondictionHolder<Indicator>.DefineTp[1] { this.GeTp });
        public override string ToString() => base.ToString();
        public override void Trade(Side side, double price) => base.Trade(side, price);
        public override void Update(object obj) => throw new NotImplementedException();

        #region utils
        private void CalulateFibRetrace(DateTime utcNow)
        {
            TimeSpan start_delta = TimeSpan.Zero;
            bool isbefore = utcNow.TimeOfDay > this.StarTimeOfDay ? false : true;
            if (!isbefore)
                start_delta = utcNow.TimeOfDay - this.StarTimeOfDay;
            else
                start_delta = utcNow.TimeOfDay - this.StarTimeOfDay + TimeSpan.FromDays(1);

            double delta = EndTimeOfDay > StarTimeOfDay ? Math.Abs(EndTimeOfDay.TotalSeconds - this.StarTimeOfDay.TotalSeconds) : GetReversSpan(StarTimeOfDay, EndTimeOfDay).TotalSeconds;

            HistoricalData temPHd = this.Symbol.GetHistory(StaticUtils.GetPeriod(this.HD), utcNow.AddSeconds(-start_delta.TotalSeconds), toTime: utcNow.AddSeconds(-start_delta.TotalSeconds+delta));

            double min = temPHd.Min(x => x[PriceType.Low]);
            double max = temPHd.Max(x => x[PriceType.High]);

            this.FibRetrace = min + ((max - min) * this._FibRetracPercent);
        }

        private TimeSpan GetReversSpan(TimeSpan start, TimeSpan end)
        {
            if (start < end)
                return TimeSpan.Zero;
            return TimeSpan.FromDays(1) - start + end;
        }

        private void SetStatus()
        {
            DateTime dt = this.HD.Symbol.LastDateTime;
            RaplaelStrategyStatus temStatus = RaplaelStrategyStatus.waitingFibonacci;

            if (StarTimeOfDay < EndTimeOfDay)
            {
                if (dt.TimeOfDay > StarTimeOfDay & dt.TimeOfDay < EndTimeOfDay)
                    temStatus = RaplaelStrategyStatus.waitingFibonacci;
                else
                    temStatus = RaplaelStrategyStatus.trade;
            }
            else
            {
                if (dt.TimeOfDay > StarTimeOfDay || dt.TimeOfDay < EndTimeOfDay)
                    temStatus = RaplaelStrategyStatus.waitingFibonacci;
                else
                    temStatus = RaplaelStrategyStatus.trade;
            }

            if (this._Status == RaplaelStrategyStatus.waitingFibonacci & temStatus == RaplaelStrategyStatus.trade)
                this.CalulateFibRetrace(dt);

            this._Status = temStatus;
        }

        private bool EvaluateTrade(IHistoryItem item, Side side)
        {
            var low = item[PriceType.Low];
            var high = item[PriceType.High];
            var open = item[PriceType.Open];
            var close = item[PriceType.Close];
            var indi = this.SmaIndicator.GetValue();

            bool resoult = false;

            switch (side)
            {
                case Side.Buy:
                    if (open > this.FibRetrace & low < this.FibRetrace & close > indi & close > FibRetrace)
                        resoult = true;
                    break;
                case Side.Sell:
                    if (open < this.FibRetrace & high > this.FibRetrace & close < indi & close < FibRetrace)
                        resoult = true;
                    break;
            }

            return resoult;
        }
        #endregion

        #region logic
        public override double GeTp(Indicator indicator, string guidOrdersReference)
        {
            SlTpItems item = base.GetSlTpItemById(guidOrdersReference);

            double resoult = 0;

            switch (item.Side)
            {
                case Side.Buy:
                    resoult = item.EntryPrice * 1.01;
                    break;
                case Side.Sell:
                    resoult = item.EntryPrice * 0.99;
                    break;
            }

            return resoult;
        }
        public override double GetSl(Indicator indicator, string guidOrdersReference)
        {
            SlTpItems item = base.GetSlTpItemById(guidOrdersReference);

            double resoult = 0;

            switch (item.Side)
            {
                case Side.Buy:
                    resoult = item.EntryPrice * 0.99;
                    break;
                case Side.Sell:
                    resoult = item.EntryPrice * 1.01;
                    break;
            }

            return resoult;
        }
        #endregion
    }
}
