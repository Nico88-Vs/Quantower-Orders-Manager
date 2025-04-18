using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public interface IConditionable
    {
        public double NetProfit { get; }
        public double LongCount { get; }
        public double ShortCount { get; }
        public Account Account { get; }
        public int LongExpo { get; }
        public int ShortExpo { get; }
        string ConditionName { get; }

        public void Update(object obj);
        public void GetMetrics();
        public void Close();
    }
}
