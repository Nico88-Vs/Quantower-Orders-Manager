using System;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv.DDDCore
{
    [AttributeUsage(AttributeTargets.Property)]
    public class MetricAttribute : Attribute
    {
        public string Category { get; }
        public string DisplayName { get; }
        public string Unit { get; }

        public MetricAttribute(string category, string displayName = null, string unit = "")
        {
            Category = category;
            DisplayName = displayName;
            Unit = unit;
        }
    }

    public class PerformanceMetrics
    {
        private readonly TpSlManager manager = GlobalTpSlManager.Instance;

        #region 📘 REQ [NEXT]
        // TODO: Ottimizzare in futuro con calcolo asincrono o caching.
        //TODO: SHARE those metrics with the domain
        #endregion

        public bool EnableHeavyMetrics { get; set; }
        public Account Account { get; private set; }

        public void SetPerformanceMetrics(bool enableHavy, string strategyTag, Account account)
        {
            this.EnableHeavyMetrics = enableHavy;
            this.StrategyTag = strategyTag;
            this.Account = account;
        }

        public PerformanceMetrics()
        {
            this.EnableHeavyMetrics = false;
        }

        #region Properties
        public string StrategyTag { get; private set; }

        [Metric("System", "Enable Heavy Metrics")]
        public bool EnableHeavyMetricsFlag => EnableHeavyMetrics;

        [Metric("Meta", "Strategy Tag")]
        public string StrategyTagDisplay => StrategyTag;

        [Metric("Base", "AccountBalance", "$")]
        public double AccountBalance => this.Account != null ? this.Account.Balance : 0;

        [Metric("Base", "Net Profit", "$")]
        public double NetProfit => manager.Items.Sum(i => i.NetProfit) + manager.ClosedItems.Sum(i => i.NetProfit);

        [Metric("Base", "Gross Profit", "$")]
        public double GrossProfit => manager.Items.Sum(i => i.GrossProfit) + manager.ClosedItems.Sum(i => i.GrossProfit);

        [Metric("Base", "Fees Paid", "$")]
        public double PaiedFees => manager.Items.Sum(i => i.Fees) + manager.ClosedItems.Sum(i => i.Fees);

        [Metric("Base", "Positive Operations")]
        public int PositiveOperations => manager.Items.Count(i => i.GrossProfit > 0) + manager.ClosedItems.Count(i => i.GrossProfit > 0);

        [Metric("Base", "Negative Operations")]
        public int NegativeOperations => manager.Items.Count(i => i.GrossProfit <= 0) + manager.ClosedItems.Count(i => i.GrossProfit <= 0);

        [Metric("Base", "Long Count")]
        public int LongCount => manager.Items.Count(i => i.Side == Side.Buy) + manager.ClosedItems.Count(i => i.Side == Side.Buy);

        [Metric("Base", "Short Count")]
        public int ShortCount => manager.Items.Count(i => i.Side == Side.Sell) + manager.ClosedItems.Count(i => i.Side == Side.Sell);

        [Metric("Base", "Exposed")]
        public bool Exposed => manager.Items.Any();

        [Metric("Base", "Exposed Amount")]
        public double ExposedAmount => manager.Items.Sum(i => i.Quantity - i.ClosedQuantity);

        [Metric("Base", "Trade Count")]
        public int TradeCount => manager.TradeCount;

        [Metric("Performance", "Average Profit/Trade", "$")]
        public double AverageProfitPerTrade => manager.ClosedItems.Any() ? manager.ClosedItems.Average(i => i.NetProfit) : 0;

        [Metric("Performance", "Average Gross Profit", "$")]
        public double AverageGrossProfit => manager.ClosedItems.Any() ? manager.ClosedItems.Average(i => i.GrossProfit) : 0;

        [Metric("Performance", "Win Rate", "%")]
        public double WinRate => PositiveOperations + NegativeOperations == 0 ? 0 : (double)PositiveOperations / (PositiveOperations + NegativeOperations);

        [Metric("Performance", "Profit Factor")]
        public double ProfitFactor =>
            manager.ClosedItems.Where(i => i.NetProfit < 0).Sum(i => Math.Abs(i.NetProfit)) is double losses && losses > 0
                ? manager.ClosedItems.Where(i => i.NetProfit > 0).Sum(i => i.NetProfit) / losses
                : double.NaN;

        [Metric("Performance", "Expectancy")]
        public double Expectancy
        {
            get
            {
                var total = PositiveOperations + NegativeOperations;
                Core.Instance.Loggers.Log("NetProfit called", LoggingLevel.Error);
                if (total == 0) return 0;
                double avgWin = manager.ClosedItems.Where(i => i.NetProfit > 0).DefaultIfEmpty().Average(i => i?.NetProfit ?? 0);
                double avgLoss = manager.ClosedItems.Where(i => i.NetProfit <= 0).DefaultIfEmpty().Average(i => i?.NetProfit ?? 0);
                return (PositiveOperations / (double)total) * avgWin + (NegativeOperations / (double)total) * avgLoss;
            }
        }

        [Metric("Performance", "Max Drawdown", "$")]
        public double MaxDrawdown
        {
            get
            {
                if (!EnableHeavyMetrics) return double.NaN;
                double peak = 0, trough = 0, maxDD = 0, cumulative = 0;
                foreach (var item in manager.ClosedItems)
                {
                    cumulative += item.NetProfit;
                    if (cumulative > peak) { peak = cumulative; trough = cumulative; }
                    if (cumulative < trough)
                    {
                        trough = cumulative;
                        maxDD = Math.Min(maxDD, trough - peak);
                    }
                }
                return Math.Abs(maxDD);
            }
        }

        [Metric("Performance", "Max Consecutive Wins")]
        public int MaxConsecutiveWins => EnableHeavyMetrics ? GetMaxConsecutive(i => i.NetProfit > 0) : -1;

        [Metric("Performance", "Max Consecutive Losses")]
        public int MaxConsecutiveLosses => EnableHeavyMetrics ? GetMaxConsecutive(i => i.NetProfit <= 0) : -1;

        [Metric("Performance", "Recovery Factor")]
        public double RecoveryFactor => EnableHeavyMetrics ? (MaxDrawdown == 0 ? double.NaN : NetProfit / MaxDrawdown) : double.NaN;

        [Metric("Performance", "Profit StdDev")]
        public double ProfitStdDev =>
            EnableHeavyMetrics && manager.ClosedItems.Count >= 2
                ? Math.Sqrt(manager.ClosedItems.Sum(i => Math.Pow(i.NetProfit - AverageProfitPerTrade, 2)) / (manager.ClosedItems.Count - 1))
                : double.NaN;

        [Metric("Performance", "Sharpe Ratio")]
        public double SharpeRatio => EnableHeavyMetrics && ProfitStdDev != 0 ? AverageProfitPerTrade / ProfitStdDev : double.NaN;

        [Metric("Exposure", "Max Exposure", "units")]
        public double MaxExposureAmount => manager.Items.Any() ? manager.Items.Max(i => i.Quantity - i.ClosedQuantity) : 0;

        [Metric("Exposure", "Avg Exposure", "units")]
        public double AvgExposurePerTrade => manager.Items.Any() ? manager.Items.Average(i => i.Quantity - i.ClosedQuantity) : 0;
        #endregion

        #region Utility
        private int GetMaxConsecutive(Func<SlTpItems, bool> condition)
        {
            int max = 0, current = 0;
            foreach (var item in manager.ClosedItems)
            {
                if (condition(item))
                    current++;
                else
                {
                    max = Math.Max(max, current);
                    current = 0;
                }
            }
            return Math.Max(max, current);
        }

        public void ExportToMeter(Meter meter, string prefix = "metric_")
        {
            var properties = this.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetCustomAttribute<MetricAttribute>() != null);

            foreach (var prop in properties)
            {
                var attr = prop.GetCustomAttribute<MetricAttribute>();
                string name = attr.DisplayName ?? prop.Name;
                string category = attr.Category?.ToLower().Replace(" ", "_") ?? "general";
                string unit = string.IsNullOrWhiteSpace(attr.Unit) ? "" : $" ({attr.Unit})";

                // Es: metric_performance_avgprofit
                string metricName = $"{prefix}{category}_{prop.Name.ToLower()}";
                string description = $"{category.ToUpper()}: {name}{unit}";

                if (prop.PropertyType == typeof(double))
                {
                    meter.CreateObservableGauge(metricName, () => (double)prop.GetValue(this), description);
                }
                else if (prop.PropertyType == typeof(int))
                {
                    meter.CreateObservableGauge(metricName, () => (int)prop.GetValue(this), description);
                }
                else if (prop.PropertyType == typeof(bool))
                {
                    meter.CreateObservableGauge(metricName, () => ((bool?)prop.GetValue(this)) == true ? 1 : 0, description);
                }
            }
        }

        public void SetAccount(Account account)
        {
            this.Account = account;
        }

        public void SetStrategyTag(string strategyTag)
        {
            this.StrategyTag = strategyTag;
        }
        #endregion
    }
}
