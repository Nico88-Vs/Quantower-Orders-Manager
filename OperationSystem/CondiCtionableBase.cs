using System;
using TpSlManager;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystem
{
    // Classe base astratta che fornisce funzionalità comuni
    public abstract class ConditionableBase<R> : IConditionable
    {
        // Proprietà implementate nella classe base
        public virtual double NetProfit { get => TpSlManager<R>.NetProfit; }
        public virtual double LongCount { get => TpSlManager<R>.LongCount; }
        public virtual double ShortCount { get => TpSlManager<R>.ShortCount; }
        public virtual int LongExpo { get => TpSlManager<R>.LongExpo; }
        public virtual int ShortExpo { get => TpSlManager<R>.ShortExpo; }
        public virtual int LongProfittableCount { get => TpSlManager<R>.LongtProfittableCount; }
        public virtual int ShortProfittableCount { get => TpSlManager<R>.ShortProfittableCount; }
        public virtual string ConditionName { get; }
        public virtual string Description { get; }
        public virtual Account Account { get; }
        public virtual Symbol Symbol { get; }
        public virtual double Quantity { get; }
        public virtual int MaxShortExo { get; }
        public virtual int MaxLongExo { get; }
        public virtual SlTpCondictionHolder<R> CondictionHolder { get; protected set; }

        // Metodi astratti che devono essere implementati dalle classi derivate
        public abstract void Trade(Side side, double price);
        public virtual void Close()
        {
            TpSlManager<R>.Stop();
        }
        public abstract void GetMetrics();
        public abstract void Update(object obj);

        protected ConditionableBase(Account account, Symbol symbol, double quantity, int maxShortExpo = 1, int maxLongExpo = 1)
        {
            MaxShortExo = maxShortExpo;
            MaxLongExo = maxLongExpo;
            Account = account;
            Symbol = symbol;
            Quantity = quantity;
        }

        public void ManagerInit()
        {
            TpSlManager<R>.init(CondictionHolder, MaxShortExo, MaxLongExo);
        }

        // Metodo che può essere sovrascritto o lasciato invariato
        public abstract void SetCondictionHolder();
    }
}
