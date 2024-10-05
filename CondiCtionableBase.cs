using System;
using TpSlManager;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1
{
    // Classe base astratta che fornisce funzionalità comuni
    public abstract class ConditionableBase<R> : IConditionable
    {
        // Proprietà implementate nella classe base
        public virtual double NetProfit { get; protected set; }
        public virtual double LongCount { get; protected set; }
        public virtual double ShortCount { get; protected set; }
        public virtual int LongExpo { get; protected set; }
        public virtual int ShortExpo { get; protected set; }
        public virtual string ConditionName { get; protected set; }
        public virtual SlTpCondictionHolder<R> CondictionHolder { get; protected set; }

        // Metodi astratti che devono essere implementati dalle classi derivate
        public abstract void Trade(Side side, double price);
        public abstract void Close();
        public abstract void GetMetrics();
        public abstract void Update(object obj);

        protected ConditionableBase()
        {
            this.SetCondictionHolder();
            this.ManagerInit();
        }

        public void ManagerInit()
        {
            TpSlManager<R>.init(this.CondictionHolder);
        }

        // Metodo che può essere sovrascritto o lasciato invariato
        public abstract void SetCondictionHolder();
    }
}
