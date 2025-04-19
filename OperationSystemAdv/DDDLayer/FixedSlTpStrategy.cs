using System;
using System.Collections.Generic;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public class FixedSlTpStrategy : ISlTpStrategy<double> 
    {
        private readonly double _slPercent;
        private readonly double _tpPercent;

        public FixedSlTpStrategy(double slPercent, double tpPercent)
        {
            _slPercent = slPercent;
            _tpPercent = tpPercent;
        }

        public double SlMarketData { get; set; }
        public double TpMarketData { get; set; }

        //TODO: works only for buy
        public List<double> CalculateSl(double marketData)
        {
            List<double> result = new List<double> { Math.Abs(marketData * (1.0 - _slPercent))};
            return result;
        }

        public List<double> CalculateTp(double marketData)
        {
            List<double> result =  new List<double>{marketData *  _tpPercent};
            return result;
        }

        //TODO : Missing Implementation
        public IEnumerable<IDomainEvent> GenerateExitEvents(double marketData, string itemId) => throw new System.NotImplementedException();
    }
}
