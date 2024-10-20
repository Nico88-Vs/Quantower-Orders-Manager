using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TpSlManager;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1
{
    public interface IConditionable
    {
        public double NetProfit  { get; }
        public double LongCount { get; }
        public double ShortCount { get; }
        public int LongExpo { get; }        
        public int ShortExpo { get; }
    
        public void Update(object obj);
        public void SetCondictionHolder();
        public void ManagerInit();
        public void Trade(Side side, double price);
        public void GetMetrics();
        public void Close();
    }
}
