using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace CondictionalStrategyExample
{
    public static class StaticUtils
    {
        public static Period GetPeriod(HistoricalData history)
        {
            if (history.Aggregation is HistoryAggregationTime)
            {
                var agg1 = (HistoryAggregationTime)history.Aggregation;
                return agg1.Period;
            }
            else if (history.Aggregation is HistoryAggregationTickBars)
            {
                var agg1 = (HistoryAggregationTickBars)history.Aggregation;
                return new Period(BasePeriod.Tick, agg1.TicksCount);
            }
            else return new Period();
        }
    }
}
