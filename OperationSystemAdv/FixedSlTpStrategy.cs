using System.Collections.Generic;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv
{
    public class FixedSlTpStrategy<T> : ISlTpStrategy<T> where T : Quote
    {
        private readonly double _slPercent;
        private readonly double _tpPercent;

        public FixedSlTpStrategy(double slPercent, double tpPercent)
        {
            _slPercent = slPercent;
            _tpPercent = tpPercent;
        }

        public double CalculateSl(T marketData, string itemId)
        {
            return marketData.Bid * (1.0 - _slPercent);
        }

        public double CalculateTp(T marketData, string itemId)
        {
            return marketData.Bid * (1.0 + _tpPercent);
        }

        public IEnumerable<IDomainEvent> GenerateExitEvents(T marketData, string itemId)
        {
            yield break; // implementazione base vuota
        }
    }
}
