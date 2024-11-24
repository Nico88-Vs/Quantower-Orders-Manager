using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DivergentStrV0_1.OperationSystem;
using TpSlManager;
using TradingPlatform.BusinessLayer;
namespace DivergentStrV0_1.Strategies
{
    public class TestedStrategy : ConditionableBase<Indicator>
    {
        public override double NetProfit => base.NetProfit;

        public override double LongCount => base.LongCount;

        public override double ShortCount => base.ShortCount;

        public override int LongExpo => base.LongExpo;

        public override int ShortExpo => base.ShortExpo;

        public override int LongProfittableCount => base.LongProfittableCount;

        public override int ShortProfittableCount => base.ShortProfittableCount;

        public override string ConditionName => base.ConditionName;

        public override string Description => base.Description;

        public override Account Account => base.Account;

        public override Symbol Symbol => base.Symbol;

        public override double Quantity => base.Quantity;

        public override int MaxShortExo => base.MaxShortExo;

        public override int MaxLongExo => base.MaxLongExo;

        //TODO: manca use position

        public TestedStrategy(Indicator ichichimokuIndicator, Account account, Symbol symbol, double quantity, int maxShortExpo = 1, int maxLongExpo = 1)
            : base(account, symbol, quantity, true, maxShortExpo, maxLongExpo)
        {

        }

        public override SlTpCondictionHolder<Indicator> CondictionHolder { get => base.CondictionHolder; protected set => base.CondictionHolder = value; }


        public override void GetMetrics() => throw new NotImplementedException();
        public override void SetCondictionHolder() => throw new NotImplementedException();
        public override string ToString() => base.ToString();
        public override void Trade(Side side, double price) => throw new NotImplementedException();
        public override void Update(object obj) => throw new NotImplementedException();
    }
}
