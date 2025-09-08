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

    public static class InMarketUtc
    {
        // ⬇️ Imposta questi 2 orari in UTC (tu li calcoli a monte)
        // Esempio EDT: DailyBreakStartUtc=21:00, DailyBreakEndUtc=22:00
        // Esempio EST: DailyBreakStartUtc=22:00, DailyBreakEndUtc=23:00
        public static TimeOnly DailyBreakStartUtc = new(21, 0); // inizio pausa giornaliera (chiusura)
        public static TimeOnly DailyBreakEndUtc = new(22, 0); // fine pausa (riapertura)

        // Opzionale: se vuoi tener traccia del weekend per chiarezza (non serve per l’in-market)
        public static TimeOnly WeekendFriCloseStartUtc = new(21, 0); // es. ven 21:00 UTC
        public static TimeOnly WeekendSunReopenUtc = new(22, 0); // es. dom 22:00 UTC

        /// <summary>
        /// Costruisce le finestre IN-MARKET standard:
        /// - Domenica→Giovedì: DailyBreakEndUtc → DailyBreakStartUtc (overnight)
        /// Nota: il venerdì sera scatta il weekend, quindi NON apriamo un nuovo overnight.
        /// </summary>
        public static List<SimpleSessionUtc> Build()
        {
            // In-market ricorrente: open dopo la pausa, close prima della pausa del giorno successivo
            var inMarketDays = new[]
            {
            DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday,
            DayOfWeek.Wednesday, DayOfWeek.Thursday
        };

            return new List<SimpleSessionUtc>
        {
            new SimpleSessionUtc(
                name: "InMarket Sun-Thu (UTC)",
                days: inMarketDays,
                openUtc:  DailyBreakEndUtc,   // es. 22:00
                closeUtc: DailyBreakStartUtc  // es. 21:00 (→ overnight perché close <= open)
            )
        };
        }

        /// <summary>
        /// Variante parametrica: utile se vuoi passare gli orari a runtime.
        /// </summary>
        public static List<SimpleSessionUtc> Build(TimeOnly breakStartUtc, TimeOnly breakEndUtc)
        {
            DailyBreakStartUtc = breakStartUtc;
            DailyBreakEndUtc = breakEndUtc;
            return Build();
        }
    }

        public static class OffMarketUtc
    {
        // 🔧 IMPOSTA QUI I TUOI ORARI UTC (già convertiti a monte)
        // Esempio EDT: 21:00–22:00 e weekend Fri 21:00 → Sun 22:00
        // Esempio EST: 22:00–23:00 e weekend Fri 22:00 → Sun 23:00
        public static TimeOnly DailyCloseStartUtc = new(21, 0);
        public static TimeOnly DailyCloseEndUtc = new(22, 0);
        public static TimeOnly WeekendFriStartUtc = new(21, 0);
        public static TimeOnly WeekendSunEndUtc = new(22, 0);

        public static List<SimpleSessionUtc> Build()
        {
            var list = new List<SimpleSessionUtc>();

            // Daily close (Mon–Thu) — evita sovrapposizione col blocco weekend
            list.Add(new SimpleSessionUtc("DailyClose Mon-Thu (UTC)",
                new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                DailyCloseStartUtc, DailyCloseEndUtc));

            // Weekend spezzato (aderente a SimpleSessionUtc, tutto in UTC)
            list.Add(new SimpleSessionUtc("Weekend Fri (UTC)",
                new[] { DayOfWeek.Friday },
                WeekendFriStartUtc, new TimeOnly(0, 0)));     // ven start → sab 00:00

            list.Add(new SimpleSessionUtc("Weekend Sat (UTC)",
                new[] { DayOfWeek.Saturday },
                new TimeOnly(0, 0), new TimeOnly(0, 0)));     // sab full-day (00:00→00:00)

            list.Add(new SimpleSessionUtc("Weekend Sun (UTC)",
                new[] { DayOfWeek.Sunday },
                new TimeOnly(0, 0), WeekendSunEndUtc));       // dom 00:00 → end

            return list;
        }
    }
}
