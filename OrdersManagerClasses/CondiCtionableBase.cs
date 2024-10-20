using System;
using System.Linq;
using TpSlManager;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OrdersManagerClasses
{
    // Classe base astratta che fornisce funzionalità comuni
    public abstract class ConditionableBase<R> : IConditionable
    {
        // Proprietà implementate nella classe base
        public virtual double NetProfit => TpSlManager<R>.NetProfit;
        public virtual double LongCount => TpSlManager<R>.LongCount;
        public virtual double ShortCount => TpSlManager<R>.ShortCount;
        public virtual int LongExpo => TpSlManager<R>.LongExpo;
        public virtual int ShortExpo => TpSlManager<R>.ShortExpo;

        public static string ConditionName { get; set; }
        public static string Description { get; set; }
        public virtual Account Account { get; }
        public virtual Symbol Symbol { get; }
        public virtual double Quantity { get; }
        public virtual int MaxShortExo { get; }
        public virtual int MaxLongExo { get; }
        public virtual SlTpCondictionHolder<R> CondictionHolder { get; protected set; }

        public virtual void Trade(Side side, double price)
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

            TpSlManager<R>.PlaceOrder(placeHoldeReq);
        }
        public virtual void Close()
        {
            TpSlManager<R>.Stop();
        }
        public abstract void GetMetrics();
        public abstract void Update(object obj);

        protected ConditionableBase(Account account, Symbol symbol, double quantity, int maxShortExpo = 1, int maxLongExpo = 1)
        {
            MaxShortExo = maxShortExpo;
            MaxShortExo = maxLongExpo;
            Account = account;
            Symbol = symbol;
            Quantity = quantity;
        }

        public void ManagerInit()
        {
            TpSlManager<R>.init(CondictionHolder);
        }

        /// <summary>
        /// Here we have to call Initilize Condiction Holder
        /// </summary>
        public virtual void SetCondictionHolder()
        {
        }

        protected SlTpCondictionHolder<R> CreateCondictionHolder(
        R[] stopLossIndicators,
        R[] takeProfitIndicators,
        SlTpCondictionHolder<R>.DefineSl[] slDelegates,
        SlTpCondictionHolder<R>.DefineTp[] tpDelegates)
        {
            return new SlTpCondictionHolder<R>(stopLossIndicators, takeProfitIndicators, slDelegates, tpDelegates);
        }

        public virtual void InitializeCondictionHolder(
        R[] stopLossIndicators,
        R[] takeProfitIndicators,
        SlTpCondictionHolder<R>.DefineSl[] slDelegates,
        SlTpCondictionHolder<R>.DefineTp[] tpDelegates)
        {
            this.CondictionHolder = CreateCondictionHolder(stopLossIndicators, takeProfitIndicators, slDelegates, tpDelegates);
        }

        public abstract double GeTp(R indicator, string guidOrdersReference);

        public abstract double GetSl(R indicator, string guidOrdersReference);

        protected SlTpItems GetSlTpItemById(string guid)
        {
            try
            {
                SlTpItems sltpitem = TpSlManager<R>.SlTpItems.FirstOrDefault(x => x.Id == guid);
                return sltpitem;
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log(ex.Message, LoggingLevel.Error);
                return null;
            }
        }
    }
}
