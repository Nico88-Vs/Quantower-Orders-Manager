using System.Collections.Generic;
using System.Linq;
using System;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Utils
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

        public static Indicator GenerateIndicator(string indi_names, HistoricalData hd, IList<SettingItem> indi_settings = null)
        {
            if (hd == null)
                return null;

            Indicator resoult = null;
            try
            {
                var indInfo = Core.Instance.Indicators.All.First(x => x.Name == indi_names);
                Indicator indicator = Core.Instance.Indicators.CreateIndicator(indInfo);
                if (indi_settings != null)
                    indicator.Settings = indi_settings;

                resoult = indicator;
                //HACK adding Indi Here
                hd.AddIndicator(indicator);
            }
            catch (Exception ex)
            {
                Core.Instance.Loggers.Log("Indicator Generation Failed", loggingLevel: LoggingLevel.Error);
                Core.Instance.Loggers.Log($"Failed with message : {ex.Message}", loggingLevel: LoggingLevel.Error);
            }
            return resoult;
        }
    }
}
