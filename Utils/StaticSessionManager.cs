using DivergentStrV0_1.OperationSystemAdv.DDDCore;
using System;
using System.Collections.Generic;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Utils
{
    /// <summary>
    /// Item singolo di livello (nome + high/low).
    /// Include il "PrevDay" come Name = "__PREV_DAY__" per distinguerlo dalle sessioni.
    /// </summary>
    public sealed class TPLevelItem
    {
        public string Name { get; }
        public double High { get; }
        public double Low { get; }

        public TPLevelItem(string name, double high, double low)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "UNNAMED" : name;
            High = high;
            Low = low;
        }
    }

    /// <summary>
    /// DTO contenente tutti i livelli calcolati (prev day + target sessions).
    /// </summary>
    public sealed class TPLevelsDto
    {
        public IReadOnlyList<TPLevelItem> Levels { get; }

        public TPLevelsDto(List<TPLevelItem> levels = null)
        {
            Levels = levels ?? new List<TPLevelItem>();
        }

        /// <summary>Helper per recuperare un item per nome.</summary>
        public TPLevelItem? GetByName(string name) =>
            Levels.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public enum SessionType
    {
        Trade,
        Target
    }


    //📝 TODO: [Logs]


    public static class StaticSessionManager
    {
        public static List<SimpleSessionUtc> TargetSessions { get; set; } = new();
        public static List<SimpleSessionUtc> TradeSessions { get; set; } = new();

        private static HystoryDataProvider _dataProvider;
        public static TPLevelsDto TpLevels { get; private set; } = new TPLevelsDto();
        public static bool IsInitialized => _dataProvider != null;
        public static event EventHandler<Status> TradeSessionsStatusChanged;
        public static Status CurrentStatus
        {
            get
            {
                if (TradeSessions.Any(s => s.Status == Status.Active))
                    return Status.Active;
                return Status.Inactive;
            }
        }

        public static void Initialize(HystoryDataProvider dataProvider)
        {
            _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
        }

        public static void AddSession(SimpleSessionUtc s, SessionType t)
        {
            if (t == SessionType.Target)
            {
                TargetSessions.Add(s);
                s.StatusChanged += StaticSessionManager.OnSessionStatusChanged;
            }
            else 
            {
                s.StatusChanged += StaticSessionManager.OnTradeSessionStatusChanged;
                TradeSessions.Add(s); 
            }

                
        }

        private static void OnTradeSessionStatusChanged(object sender, Status e)
        {
            TradeSessionsStatusChanged?.Invoke(sender, e);
        }

        private static void OnSessionStatusChanged(object sender, Status e)
        { 
            CalculateTPLevels();
        }

        public static void Update(IHistoryItem item)
        {
            foreach (var s in TargetSessions) s.UpdateStatus(item);
            foreach (var s in TradeSessions) s.UpdateStatus(item);
        }

        public static void RemoveSession(string name, SessionType t)
        {
            var list = (t == SessionType.Target) ? TargetSessions : TradeSessions;
            var found = list.FirstOrDefault(x => x.Name == name);
            if (found != null)
            {
                if (t == SessionType.Target)
                    found.StatusChanged -= StaticSessionManager.OnSessionStatusChanged;
                else
                    found.StatusChanged -= StaticSessionManager.OnTradeSessionStatusChanged;
                list.Remove(found);
            }
        }

        public static void Dispose()
        {
            foreach (var s in TargetSessions)
                s.StatusChanged -= StaticSessionManager.OnSessionStatusChanged;
            foreach (var s in TradeSessions)
                s.StatusChanged -= StaticSessionManager.OnTradeSessionStatusChanged;
            TargetSessions.Clear();
            
            TradeSessions.Clear();
        }

        /// <summary>
        /// Restituisce un TPLevelsDto con:
        /// - 1 item "__PREV_DAY__" (High/Low del giorno precedente su DAY1)
        /// - 1 item per ciascuna sessione in TargetSessions con High/Low del RANGE PRECEDENTE
        ///   rispetto all'HistoricalData corrente (stesso Period di aggregazione).
        /// </summary>
        public static void CalculateTPLevels()
        {
            HistoricalData currentHistoricalData = _dataProvider?.HistoricalData;
            if (currentHistoricalData == null)
                throw new ArgumentNullException(nameof(currentHistoricalData));
            if (currentHistoricalData.Symbol == null)
                throw new InvalidOperationException("HistoricalData.Symbol is null.");

            var symbol = currentHistoricalData.Symbol;
            var items = new List<TPLevelItem>();

            // --- Prev day (DAY1) ---
            {
                DateTime toTime = currentHistoricalData[0].TimeLeft;

                TimeSpan span = toTime - currentHistoricalData.FromTime;

                if (span.TotalDays < 2)
                {
                    Core.Instance.Loggers.Log("HistoricalData range too small to calculate PrevDay.", LoggingLevel.Error);
                    throw new InvalidOperationException("HistoricalData range too small to calculate PrevDay.");
                }

                DateTime tempTime = toTime.AddDays(-3);
                DateTime fromTime = tempTime >= currentHistoricalData.FromTime ? tempTime : currentHistoricalData.FromTime;
                var hdDay = symbol.GetHistory(Period.DAY1, fromTime);
                try
                {
                    // hdDay[1] = giorno precedente (assumendo [0] = corrente o ultimo disponibile)
                    double prevHigh = hdDay[1][PriceType.High];
                    double prevLow = hdDay[1][PriceType.Low];
                    items.Add(new TPLevelItem("__PREV_DAY__", prevHigh, prevLow));
                }
                finally { hdDay?.Dispose(); }
            }

            // --- Target sessions (range PRECEDENTE) ---
            if (TargetSessions.Count > 0)
            {
                var agg = (HistoryAggregationTime)currentHistoricalData.Aggregation;
                var period = agg.Period;

                foreach (var sess in TargetSessions)
                {
                    if (sess == null) continue;

                    var prev = sess.GetPreviousSessionRangeUtc(currentHistoricalData);
                    if (prev is { } rng)
                    {
                        var (startUtc, endUtc) = rng;
                        var h = symbol.GetHistory(period, startUtc, endUtc);
                        try
                        {
                            items.Add(new TPLevelItem(
                                sess.Name ?? "UNNAMED",
                                h.High(),
                                h.Low()
                            ));
                        }
                        finally { h?.Dispose(); }
                    }
                    // Se non esiste range precedente → niente item per quella sessione
                }
            }
            
            TpLevels = new TPLevelsDto(items);
        }
    }
}
