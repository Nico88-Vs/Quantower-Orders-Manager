using System;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Reflection;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv.DDDCore
{
    public enum ExpositionSide
    {
        Long,
        Short,
        Both,
        Unexposed
    }

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
        private IManagerFacade manager;

        public bool EnableHeavyMetrics { get; set; }
        public Account Account { get; private set; }

        public PerformanceMetrics()
        {
            this.EnableHeavyMetrics = false;
        }

        public void SetPerformanceMetrics(bool enableHeavy, string strategyTag, Account account)
        {
            this.EnableHeavyMetrics = enableHeavy;
            this.StrategyTag = strategyTag;
            this.Account = account;
        }

        public void SetAccount(Account account) => this.Account = account;
        public void SetStrategyTag(string strategyTag) => this.StrategyTag = strategyTag;
        public void SetManager(IManagerFacade managerFacade) => this.manager = managerFacade;

        public string StrategyTag { get; private set; }

        [Metric("System", "Enable Heavy Metrics")] public bool EnableHeavyMetricsFlag => EnableHeavyMetrics;
        [Metric("Meta", "Strategy Tag")] public string StrategyTagDisplay => StrategyTag;
        [Metric("Base", "AccountBalance", "$")] public double AccountBalance => this.Account != null ? this.Account.Balance : 0;

        [Metric("Base", "Net Profit", "$")]
        public double NetProfit => manager == null ? 0 : manager.Items.Sum(i => i.NetProfit) + manager.ClosedItems.Sum(i => i.NetProfit);

        [Metric("Base", "Gross Profit", "$")]
        public double GrossProfit => manager == null ? 0 : manager.Items.Sum(i => i.GrossProfit) + manager.ClosedItems.Sum(i => i.GrossProfit);

        [Metric("Base", "Fees Paid", "$")]
        public double PaiedFees => manager == null ? 0 : manager.Items.Sum(i => i.Fees) + manager.ClosedItems.Sum(i => i.Fees);

        [Metric("Base", "Positive Operations")]
        public int PositiveOperations => manager == null ? 0 : manager.Items.Count(i => i.GrossProfit > 0) + manager.ClosedItems.Count(i => i.GrossProfit > 0);

        [Metric("Base", "Negative Operations")]
        public int NegativeOperations => manager == null ? 0 : manager.Items.Count(i => i.GrossProfit <= 0) + manager.ClosedItems.Count(i => i.GrossProfit <= 0);

        [Metric("Base", "Long Count")] public int LongCount => manager == null ? 0 : manager.Items.Count(i => i.Side == Side.Buy) + manager.ClosedItems.Count(i => i.Side == Side.Buy);
        [Metric("Base", "Short Count")] public int ShortCount => manager == null ? 0 : manager.Items.Count(i => i.Side == Side.Sell) + manager.ClosedItems.Count(i => i.Side == Side.Sell);
        [Metric("Base", "Exposed")] public bool Exposed => manager != null && manager.Items.Any();

        [Metric("Base", "Exposed Side")]
        public ExpositionSide ExposedSide => manager == null ? ExpositionSide.Unexposed :
            manager.Items.Any(x => x.Side == Side.Buy) && manager.Items.Any(x => x.Side == Side.Sell) ? ExpositionSide.Both :
            manager.Items.Any(x => x.Side == Side.Buy) && !manager.Items.Any(x => x.Side == Side.Sell) ? ExpositionSide.Long :
            manager.Items.Any(x => x.Side == Side.Sell) && !manager.Items.Any(x => x.Side == Side.Buy) ? ExpositionSide.Short :
            ExpositionSide.Unexposed;

        [Metric("Base", "Exposed Count")] public double ExposedCount => manager == null ? 0 : manager.Items.Count();
        [Metric("Base", "Exposed Amount")] public double ExposedAmount => manager == null ? 0 : manager.ExposedAmmount;
        [Metric("Base", "Trade Count")] public int TradeCount => manager == null ? 0 : manager.TradeCount;

        [Metric("Performance", "Average Profit/Trade", "$")]
        public double AverageProfitPerTrade => manager != null && manager.ClosedItems.Any() ? manager.ClosedItems.Average(i => i.NetProfit) : 0;

        [Metric("Performance", "Average Gross Profit", "$")]
        public double AverageGrossProfit => manager != null && manager.ClosedItems.Any() ? manager.ClosedItems.Average(i => i.GrossProfit) : 0;

        [Metric("Performance", "Win Rate", "%")]
        public double WinRate => PositiveOperations + NegativeOperations == 0 ? 0 : (double)PositiveOperations / (PositiveOperations + NegativeOperations);

        [Metric("Performance", "Profit Factor")]
        public double ProfitFactor
        {
            get
            {
                if (manager == null) return 0;
                double losses = manager.ClosedItems.Where(i => i.NetProfit < 0).Sum(i => Math.Abs(i.NetProfit));
                double gains = manager.ClosedItems.Where(i => i.NetProfit > 0).Sum(i => i.NetProfit);
                return losses > 0 ? gains / losses : 0;
            }
        }

        [Metric("Performance", "Expectancy", "$")]
        public double Expectancy
        {
            get
            {
                if (manager == null) return 0;
                var total = PositiveOperations + NegativeOperations;
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
                if (!EnableHeavyMetrics || manager == null) return double.NaN;
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

        [Metric("Performance", "Max Consecutive Wins")] public int MaxConsecutiveWins => EnableHeavyMetrics ? GetMaxConsecutive(i => i.NetProfit > 0) : -1;
        [Metric("Performance", "Max Consecutive Losses")] public int MaxConsecutiveLosses => EnableHeavyMetrics ? GetMaxConsecutive(i => i.NetProfit <= 0) : -1;
        [Metric("Performance", "Recovery Factor")] public double RecoveryFactor => EnableHeavyMetrics ? (MaxDrawdown == 0 ? double.NaN : NetProfit / MaxDrawdown) : double.NaN;
        [Metric("Performance", "Profit StdDev")] public double ProfitStdDev => EnableHeavyMetrics && manager != null && manager.ClosedItems.Count >= 2 ? Math.Sqrt(manager.ClosedItems.Sum(i => Math.Pow(i.NetProfit - AverageProfitPerTrade, 2)) / (manager.ClosedItems.Count - 1)) : double.NaN;
        [Metric("Performance", "Sharpe Ratio")] public double SharpeRatio => EnableHeavyMetrics && ProfitStdDev != 0 ? AverageProfitPerTrade / ProfitStdDev : double.NaN;

        [Metric("Exposure", "Max Exposure", "units")] public double MaxExposureAmount => manager != null && manager.Items.Any() ? manager.Items.Max(i => i.Quantity - i.ClosedQuantity) : 0;
        [Metric("Exposure", "Avg Exposure", "units")] public double AvgExposurePerTrade => manager != null && manager.Items.Any() ? manager.Items.Average(i => i.Quantity - i.ClosedQuantity) : 0;

        private int GetMaxConsecutive(Func<ITpSlItems, bool> condition)
        {
            if (manager == null) return 0;
            int max = 0, current = 0;
            foreach (var item in manager.ClosedItems)
            {
                if (condition(item)) current++;
                else { max = Math.Max(max, current); current = 0; }
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

                string metricName = $"{prefix}{category}_{prop.Name.ToLower()}";
                string description = $"{category.ToUpper()}: {name}{unit}";

                if (prop.PropertyType == typeof(double))
                    meter.CreateObservableGauge(metricName, () => (double)prop.GetValue(this), description);
                else if (prop.PropertyType == typeof(int))
                    meter.CreateObservableGauge(metricName, () => (int)prop.GetValue(this), description);
                else if (prop.PropertyType == typeof(bool))
                    meter.CreateObservableGauge(metricName, () => ((bool?)prop.GetValue(this)) == true ? 1 : 0, description);
            }
        }
    }
}
