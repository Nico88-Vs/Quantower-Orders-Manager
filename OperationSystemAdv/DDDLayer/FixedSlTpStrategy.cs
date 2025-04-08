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

        public double CalculateSl(double marketData, string itemId)
        {
            return marketData * (1.0 - _slPercent);
        }

        public double CalculateTp(double marketData, string itemId)
        {
            return marketData * (1.0 + _tpPercent);
        }

        //TODO : Missing Implementation
        public IEnumerable<IDomainEvent> GenerateExitEvents(double marketData, string itemId) => throw new System.NotImplementedException();
    }
}
