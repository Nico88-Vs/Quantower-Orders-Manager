using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;
using static DivergentStrV0_1.C_Obj.TF;

namespace DivergentStrV0_1.C_Obj
{
    public class CloudSeries
    {
        #region Variabili
        public string StatusLog { get; private set; } = "Initilaisin";
        public string DebugLog { get; set; } = "";
        public List<Cloud> Clouds { get; set; }
        public List<Cloud> CloudsMid { get; set; }
        public List<Cloud> CloudSlow { get; set; }
        public List<IchimokuCloudScenario> ScenarioList { get; set; }
        public Cloud CurrentCloud { get; set; }
        public Cloud CurrentMidCloud { get; set; }
        public Cloud CurrentSlowCloud { get; set; }
        public HistoricalData Hd { get; set; }
        public Dictionary<Cloud, List<Cloud>> MidCloudDictionary { get; set; }
        public Dictionary<Cloud, List<Cloud>> SlowCloudDictionary { get; set; }
        public Dictionary<Cloud, List<Cloud>> MidInSlowDictionary { get; set; }

        public IchimokuCloudScenario Scenario { get; private set; } = IchimokuCloudScenario.UNDEFINED;

        public TF MidTF { get; private set; }
        public TF FastTF { get; private set; }
        public TF SlowTF { get; private set; }

        public int FastSeriesStart { get; private set; } = 0;
        public int MidSeriesStart { get; private set; } = 0;
        public int SlowSeriesStart { get; private set; } = 0;
        public int TenkanPeriod { get; private set; } = 26;

        // Esporto from time / to time as string for Db
        public string FromTime { get; set; }
        public string ToTime { get; set; }


        //Sync LineSeries (Sync on FasT.Frame)
        private double FastS_Fast;
        private double SlowS_Fast;
        private double FastS_Mid;
        private double SlowS_Mid;
        private double FastS_Slow;
        private double SlowS_Slow;
        private bool CloudSlow_IsRunning = false;

        private Indicator Indi;

        public event EventHandler<CrossEvent> Cross;
        public event EventHandler<TrendEvent> TrendCross;
        #endregion

        public CloudSeries(HistoricalData hd, TF Fast, TF Mid, TF Slow)
        {
            this.MidTF = Mid;
            this.FastTF = Fast;
            this.SlowTF = Slow;

            this.Hd = hd;

            if (Fast.Indicatore == Mid.Indicatore && Mid.Indicatore == Slow.Indicatore)
            {
                this.Indi = Fast.Indicatore;
            }

            this.Clouds = new List<Cloud>();
            this.CloudsMid = new List<Cloud>();
            this.CloudSlow = new List<Cloud>();
            this.ScenarioList = new List<IchimokuCloudScenario>();
            this.MidCloudDictionary = new Dictionary<Cloud, List<Cloud>>();
            this.SlowCloudDictionary = new Dictionary<Cloud, List<Cloud>>();
            this.MidInSlowDictionary = new Dictionary<Cloud, List<Cloud>>();

            this.FromTime = hd.FromTime.ToString();
            this.ToTime = hd.ToTime.ToString();
        }

        public void GenerateCloud(List<TF> timeFrame)
        {
            //inizio il ciclo sulla storia
            for (int i = this.Hd.Count; i >= 0; i--)
            {
                int step = this.Hd.Count - i;
                CloudSlow_IsRunning = step > this.SlowTF.Delay * this.TenkanPeriod ? true : false;

                if (CloudSlow_IsRunning)
                {
                    //Calculating sync LineSeries (Sync on FasT.Frame)
                    FastS_Fast = this.Indi.GetValue(lineIndex: FastTF.FastSeries, offset: i);
                    SlowS_Fast = this.Indi.GetValue(lineIndex: FastTF.SlowSeries, offset: i);
                    FastS_Mid = this.Indi.GetValue(lineIndex: MidTF.FastSeries, offset: i + MidTF.GetCorrectBuffer(this.TenkanPeriod) - FastTF.GetCorrectBuffer(this.TenkanPeriod));
                    SlowS_Mid = this.Indi.GetValue(lineIndex: MidTF.SlowSeries, offset: i + MidTF.GetCorrectBuffer(this.TenkanPeriod) - FastTF.GetCorrectBuffer(this.TenkanPeriod));
                    SlowS_Slow = this.Indi.GetValue(lineIndex: SlowTF.SlowSeries, offset: i + SlowTF.GetCorrectBuffer(this.TenkanPeriod) - FastTF.GetCorrectBuffer(this.TenkanPeriod));
                    FastS_Slow = this.Indi.GetValue(lineIndex: SlowTF.FastSeries, offset: i + SlowTF.GetCorrectBuffer(this.TenkanPeriod) - FastTF.GetCorrectBuffer(this.TenkanPeriod));

                    GetScenario();
                }

                foreach (TF item in timeFrame)
                {
                    double fastValue = this.Indi.GetValue(lineIndex: item.FastSeries, offset: i);
                    double slowvalue = this.Indi.GetValue(lineIndex: item.SlowSeries, offset: i);
                    double fastValueShift = this.Indi.GetValue(lineIndex: item.FastSeries, offset: i + 1);
                    double slowvalueShift = this.Indi.GetValue(lineIndex: item.SlowSeries, offset: i + 1);
                    double originPrice = (fastValue + slowvalue) / 2;

                    //verifico la presenza delle linee
                    if (fastValueShift > 0 && !this.GetCorrectList(item).Any())
                    {
                        SetCloud(item, i, originPrice);
                    }

                    else if (fastValueShift > 0 && slowvalueShift > 0)
                    {
                        this.GetCorrectCloud(item).UpdateCloud(i);

                        bool goldenCross = (slowvalueShift >= fastValueShift) && (fastValue > slowvalue) ? true : false;
                        bool deadCross = (slowvalueShift <= fastValueShift) && (fastValue < slowvalue) ? true : false;

                        if (goldenCross || deadCross)
                        {
                            double endPrice = (fastValue + slowvalue + fastValueShift + slowvalueShift) / 4;
                            CrossEvent ar = new CrossEvent(CalculateArg(goldenCross, this.GetCorrectCloud(item).Time_F), endPrice, this.Hd.Count - i, GetCorrectCloud(item));
                            OnCross(ar);
                        }
                    }

                    else
                    {
                        SeriesStart(item);
                    }

                }
            }
        }

        public void Update(TF timeFrame)
        {
            // Update The Todate prop
            this.ToTime = this.Hd.ToTime.ToString();

            double fastValue = this.Indi.GetValue(lineIndex: timeFrame.FastSeries, offset: 0);
            double slowvalue = this.Indi.GetValue(lineIndex: timeFrame.SlowSeries, offset: 0);
            double fastValueShift = this.Indi.GetValue(lineIndex: timeFrame.FastSeries, offset: 1);
            double slowvalueShift = this.Indi.GetValue(lineIndex: timeFrame.SlowSeries, offset: 1);

            CloudSlow_IsRunning = this.CloudSlow.Sum(x => x.Length) > this.SlowTF.Delay * this.TenkanPeriod ? true : false;

            if (CloudSlow_IsRunning && timeFrame.Timeframe == TimeFrame.Fast)
            {
                //Calculating sync LineSeries (Sync on FasT.Frame)
                FastS_Fast = this.Indi.GetValue(lineIndex: FastTF.FastSeries, offset: 0);
                SlowS_Fast = this.Indi.GetValue(lineIndex: FastTF.SlowSeries, offset: 0);
                FastS_Mid = this.Indi.GetValue(lineIndex: MidTF.FastSeries, offset: MidTF.GetCorrectBuffer(this.TenkanPeriod) - FastTF.GetCorrectBuffer(this.TenkanPeriod));
                SlowS_Mid = this.Indi.GetValue(lineIndex: MidTF.SlowSeries, offset: MidTF.GetCorrectBuffer(this.TenkanPeriod) - FastTF.GetCorrectBuffer(this.TenkanPeriod));
                SlowS_Slow = this.Indi.GetValue(lineIndex: SlowTF.SlowSeries, offset: SlowTF.GetCorrectBuffer(this.TenkanPeriod) - FastTF.GetCorrectBuffer(this.TenkanPeriod));
                FastS_Slow = this.Indi.GetValue(lineIndex: SlowTF.FastSeries, offset: SlowTF.GetCorrectBuffer(this.TenkanPeriod) - FastTF.GetCorrectBuffer(this.TenkanPeriod));

                GetScenario();
            }

            bool goldenCross = (slowvalueShift >= fastValueShift) && (fastValue > slowvalue) ? true : false;
            bool deadCross = (slowvalueShift <= fastValueShift) && (fastValue < slowvalue) ? true : false;

            switch (timeFrame.Timeframe)
            {
                case TimeFrame.Fast:

                    if (fastValueShift > 0 && slowvalueShift > 0)
                    {
                        this.CurrentCloud.UpdateCloud(0);

                        if (goldenCross || deadCross)
                        {
                            double endPrice = (fastValue + slowvalue + fastValueShift + slowvalueShift) / 4;
                            CrossEvent ar = new CrossEvent(CalculateArg(goldenCross, CurrentCloud.Time_F), endPrice, this.Hd.Count, CurrentCloud);
                            OnCross(ar);
                        }

                    }
                    break;

                case TimeFrame.Mid:

                    if (fastValueShift > 0 && slowvalueShift > 0)
                    {
                        this.CurrentMidCloud.UpdateCloud(0);

                        if (goldenCross || deadCross)
                        {
                            double endPrice = (fastValue + slowvalue + fastValueShift + slowvalueShift) / 4;
                            CrossEvent ar = new CrossEvent(CalculateArg(goldenCross, CurrentMidCloud.Time_F), endPrice, this.Hd.Count, CurrentMidCloud);
                            OnCross(ar);
                        }
                    }
                    break;

                case TimeFrame.Slow:

                    if (fastValueShift > 0 && slowvalueShift > 0)
                    {
                        this.CurrentSlowCloud.UpdateCloud(0);

                        if (goldenCross || deadCross)
                        {
                            double endPrice = (fastValue + slowvalue + fastValueShift + slowvalueShift) / 4;
                            CrossEvent ar = new CrossEvent(CalculateArg(goldenCross, CurrentSlowCloud.Time_F), endPrice, this.Hd.Count, this.CurrentSlowCloud);
                            OnCross(ar);
                        }
                    }
                    break;
            }
        }

        public virtual void OnTrendCross(TrendEvent e)
        {
            TrendCross?.Invoke(this, e);
        }

        public virtual void OnCross(CrossEvent e)
        {
            CloseCloud(e);
            SwitCloud(e);
            Cross?.Invoke(this, e);
        }

        #region Services
        private EventCrosArg CalculateArg(bool isGolden, TF tf)
        {
            EventCrosArg x = EventCrosArg.Unkown;

            switch (tf.Timeframe)
            {
                case TF.TimeFrame.Fast:
                    x = isGolden == true ? EventCrosArg.Gold_fast : EventCrosArg.Dead_fast;
                    break;

                case TF.TimeFrame.Mid:
                    x = isGolden == true ? EventCrosArg.Gold_midt : EventCrosArg.Dead_mid;
                    break;

                case TF.TimeFrame.Slow:
                    x = isGolden == true ? EventCrosArg.Gold_slow : EventCrosArg.Dead_slow;
                    break;
            }
            return x;
        }

        private void DictionaryManager(Cloud cl)
        {
            int correctBuffer = cl.Buffer + cl.Time_F.GetCorrectBuffer(TenkanPeriod);

            switch (cl.Time_F.Timeframe)
            {
                case TimeFrame.Fast:
                    if (MidCloudDictionary.Any())
                    {
                        try
                        {
                            int delay = this.CloudsMid[0].Time_F.GetCorrectBuffer(TenkanPeriod);

                            Cloud c = MidCloudDictionary.Keys.Last(x => x.Buffer + delay < correctBuffer);
                            MidCloudDictionary[c].Add(cl);
                        }
                        catch (Exception)
                        {
                            this.StatusLog = $"Errore Nel Dizionario  ... TF fast mid";
                        }
                    }

                    if (SlowCloudDictionary.Any())
                    {
                        try
                        {
                            int delay = this.CloudSlow[0].Time_F.GetCorrectBuffer(TenkanPeriod);

                            Cloud cs = SlowCloudDictionary.Keys.Last(x => x.Buffer + delay < correctBuffer);
                            SlowCloudDictionary[cs].Add(cl);
                        }
                        catch (Exception)
                        {
                            this.StatusLog = $"Errore Nel Dizionario  ... TF fast slow";
                        }
                    }

                    break;
                case TimeFrame.Mid:
                    if (SlowCloudDictionary.Any())
                    {
                        try
                        {
                            int delay = this.CloudSlow[0].Time_F.GetCorrectBuffer(TenkanPeriod);

                            Cloud cs = SlowCloudDictionary.Keys.Last(x => x.Buffer + delay < correctBuffer);
                            SlowCloudDictionary[cs].Add(cl);
                        }
                        catch (Exception)
                        {
                            this.StatusLog = $"Errore Nel Dizionario  ... TF mid slow";
                        }
                    }
                    return;

            }
        }

        private void SwitCloud(CrossEvent e)
        {
            List<Cloud> list = new List<Cloud>();

            switch (e.CurrentCloud.Time_F.Timeframe)
            {
                case TimeFrame.Fast:
                    Cloud x = new Cloud(this.CurrentCloud.Id + 1, e.BarIndex, e.CurrentCloud.Time_F, e.Price);
                    this.CurrentCloud = x;
                    this.Clouds.Add(x);
                    DictionaryManager(x);
                    break;
                case TimeFrame.Mid:
                    Cloud y = new Cloud(this.CurrentMidCloud.Id + 1, e.BarIndex, e.CurrentCloud.Time_F, e.Price);
                    this.CurrentMidCloud = y;
                    this.CloudsMid.Add(y);
                    this.MidCloudDictionary.Add(y, list);
                    DictionaryManager(y);
                    break;
                case TimeFrame.Slow:
                    Cloud z = new Cloud(this.CurrentSlowCloud.Id + 1, e.BarIndex, e.CurrentCloud.Time_F, e.Price);
                    this.CurrentSlowCloud = z;
                    this.CloudSlow.Add(z);
                    this.SlowCloudDictionary.Add(z, list);
                    this.MidInSlowDictionary.Add(z, list);
                    break;
            }
        }

        private void CloseCloud(CrossEvent e)
        {
            switch (e.CurrentCloud.Time_F.Timeframe)
            {
                case TimeFrame.Fast:
                    this.CurrentCloud.CloudIsClosed(e.Price, e.BarIndex);
                    break;
                case TimeFrame.Mid:
                    this.CurrentMidCloud.CloudIsClosed(e.Price, e.BarIndex);
                    break;
                case TimeFrame.Slow:
                    this.CurrentSlowCloud.CloudIsClosed(e.Price, e.BarIndex);
                    break;
            }
        }

        private void SetCloud(TF tF, int buffer, double originPrice)
        {
            List<Cloud> list = new List<Cloud>();

            switch (tF.Timeframe)
            {
                case TimeFrame.Fast:
                    if (this.CurrentCloud == null)
                        this.CurrentCloud = new Cloud(0, buffer, tF, originPrice);
                    if (!this.Clouds.Any())
                        this.Clouds.Add(this.CurrentCloud);
                    this.StatusLog = "CurrentCloud Created";
                    break;

                case TimeFrame.Mid:
                    if (this.CurrentMidCloud == null)
                    {
                        this.CurrentMidCloud = new Cloud(0, buffer, tF, originPrice);
                        MidCloudDictionary.Add(this.CurrentMidCloud, list);
                    }
                    if (!this.CloudsMid.Any())
                        this.CloudsMid.Add(CurrentMidCloud);
                    this.StatusLog = "CurrentCloud Created";
                    break;

                case TimeFrame.Slow:
                    if (this.CurrentSlowCloud == null)
                    {
                        this.CurrentSlowCloud = new Cloud(0, buffer, tF, originPrice);
                        SlowCloudDictionary.Add(this.CurrentSlowCloud, list);
                        MidInSlowDictionary.Add(this.CurrentSlowCloud, list);
                    }
                    if (!this.CloudSlow.Any())
                        this.CloudSlow.Add(CurrentSlowCloud);
                    this.StatusLog = "CurrentCloud Created";
                    break;
            }
        }

        private void SeriesStart(TF timeframe)
        {
            switch (timeframe.Timeframe)
            {
                case TimeFrame.Fast:
                    this.FastSeriesStart++;
                    break;
                case TimeFrame.Mid:
                    this.MidSeriesStart++;
                    break;
                case TimeFrame.Slow:
                    this.SlowSeriesStart++;
                    break;
            }
        }

        private Cloud GetCorrectCloud(TF tF)
        {
            switch (tF.Timeframe)
            {
                case TimeFrame.Fast:
                    return this.CurrentCloud;
                case TimeFrame.Mid:
                    return this.CurrentMidCloud;
                case TimeFrame.Slow:
                    return this.CurrentSlowCloud;

                default:
                    return null;
            }
        }

        private List<Cloud> GetCorrectList(TF tF)
        {
            switch (tF.Timeframe)
            {
                case TimeFrame.Fast:
                    return this.Clouds;
                case TimeFrame.Mid:
                    return this.CloudsMid;
                case TimeFrame.Slow:
                    return this.CloudSlow;
                default: return null;
            }
        }
        private List<int> GetBuffer()
        {
            List<int> list = new List<int>();

            for (int i = Hd.Count; i > 0; i--)
            {
                list.Add(i - 1);
            }
            return list;
        }
        private void GetBuffer(LineSeries line)
        {
            List<int> list = GetBuffer();

            for (int i = 0; i < list.Count; i++)
            {
                line.SetValue(value: list[i], offset: list[i]);
            }

        }
        private void GetScenario()
        {
            if (!CloudSlow_IsRunning)
                return;

            double Fast_midpoint = (FastS_Fast + SlowS_Fast) / 2;
            double Mid_midpoint = (FastS_Mid + SlowS_Mid) / 2;
            double Slow_midpoint = (FastS_Slow + SlowS_Slow) / 2;

            IchimokuCloudScenario newScenario = IchimokuCloudScenario.UNDEFINED;

            if (Fast_midpoint >= Mid_midpoint && Mid_midpoint >= Slow_midpoint)
            {
                newScenario = IchimokuCloudScenario.STRONG_BULLISH; // tutto sopra
            }
            else if (Fast_midpoint <= Mid_midpoint && Mid_midpoint >= Slow_midpoint)
            {
                newScenario = IchimokuCloudScenario.CONSOLIDATION_BULLISH; // fast al centro rialzista
            }
            else if (Mid_midpoint <= Fast_midpoint && Fast_midpoint <= Slow_midpoint)
            {
                newScenario = IchimokuCloudScenario.CONSOLIDATION_BEARISH; // fast al centro ribassista
            }
            else if (Fast_midpoint >= Slow_midpoint && Slow_midpoint >= Mid_midpoint)
            {
                newScenario = IchimokuCloudScenario.MODERATELY_BULLISH; // slow al centro rialzista
            }
            else if (Fast_midpoint <= Slow_midpoint && Mid_midpoint >= Slow_midpoint)
            {
                newScenario = IchimokuCloudScenario.MODERATELY_BEARISH; // slow al centro ribassista
            }
            else if (Slow_midpoint >= Mid_midpoint && Mid_midpoint >= Fast_midpoint)
            {
                newScenario = IchimokuCloudScenario.STRONG_BEARISH; // tutto sotto
            }
            else
            {
                newScenario = IchimokuCloudScenario.UNDEFINED;
            }

            if (newScenario != IchimokuCloudScenario.UNDEFINED && this.Scenario != IchimokuCloudScenario.UNDEFINED)
            {
                if (newScenario != this.Scenario)
                {
                    TrendCros tr = GeTrendCross_Arg(newScenario);
                    TrendEvent args = new TrendEvent(this.Hd.Count, tr);
                    this.Scenario = newScenario;
                    this.OnTrendCross(args);
                }
            }

            this.Scenario = newScenario;
            ScenarioList.Add(this.Scenario);
        }
        private TrendCros GeTrendCross_Arg(IchimokuCloudScenario scenario)
        {
            TrendCros output = TrendCros.UNDEFINED;

            switch (this.Scenario)
            {
                case IchimokuCloudScenario.STRONG_BULLISH:
                    switch (scenario)
                    {
                        case IchimokuCloudScenario.MODERATELY_BULLISH:
                            output = TrendCros.STRONG_BULLISH_TO_MODERATELY_BULLISH;
                            break;
                        case IchimokuCloudScenario.STRONG_BEARISH:
                            output = TrendCros.STRONG_BULLISH_TO_STRONG_BEARISH;
                            break;
                        case IchimokuCloudScenario.CONSOLIDATION_BULLISH:
                            output = TrendCros.STRONG_BULLISH_TO_CONSOLIDATION_BULLISH;
                            break;
                    }
                    break;
                case IchimokuCloudScenario.STRONG_BEARISH:
                    switch (scenario)
                    {
                        case IchimokuCloudScenario.MODERATELY_BULLISH:
                            output = TrendCros.STRONG_BULLISH_TO_MODERATELY_BULLISH;
                            break;
                        case IchimokuCloudScenario.MODERATELY_BEARISH:
                            output = TrendCros.STRONG_BEARISH_TO_MODERATELY_BEARISH;
                            break;
                        case IchimokuCloudScenario.CONSOLIDATION_BULLISH:
                            output = TrendCros.STRONG_BULLISH_TO_CONSOLIDATION_BULLISH;
                            break;
                        case IchimokuCloudScenario.CONSOLIDATION_BEARISH:
                            output = TrendCros.STRONG_BEARISH_TO_MODERATELY_BEARISH;
                            break;
                    }
                    break;
                case IchimokuCloudScenario.MODERATELY_BULLISH:
                    switch (scenario)
                    {
                        case IchimokuCloudScenario.MODERATELY_BEARISH:
                            output = TrendCros.MODERATELY_BULLISH_TO_MODERATELY_BEARISH;
                            break;
                        case IchimokuCloudScenario.STRONG_BEARISH:
                            output = TrendCros.MODERATELY_BULLISH_TO_STRONG_BEARISH;
                            break;
                        case IchimokuCloudScenario.CONSOLIDATION_BULLISH:
                            output = TrendCros.MODERATELY_BULLISH_TO_CONSOLIDATION_BULLISH;
                            break;
                    }
                    break;
                case IchimokuCloudScenario.MODERATELY_BEARISH:
                    switch (scenario)
                    {
                        case IchimokuCloudScenario.CONSOLIDATION_BEARISH:
                            output = TrendCros.MODERATELY_BEARISH_TO_CONSOLIDATION_BEARISH;
                            break;
                        case IchimokuCloudScenario.STRONG_BEARISH:
                            output = TrendCros.MODERATELY_BEARISH_TO_STRONG_BEARISH;
                            break;
                        case IchimokuCloudScenario.MODERATELY_BULLISH:
                            output = TrendCros.MODERATELY_BEARISH_TO_MODERATELY_BULLISH;
                            break;
                    }
                    break;
                case IchimokuCloudScenario.CONSOLIDATION_BULLISH:
                    switch (scenario)
                    {
                        case IchimokuCloudScenario.MODERATELY_BULLISH:
                            output = TrendCros.CONSOLIDATION_BULLISH_TO_MODERATELY_BULLISH;
                            break;
                        case IchimokuCloudScenario.MODERATELY_BEARISH:
                            output = TrendCros.CONSOLIDATION_BULLISH_TO_MODERATELY_BEARISH;
                            break;
                    }
                    break;
                case IchimokuCloudScenario.CONSOLIDATION_BEARISH:
                    switch (scenario)
                    {
                        case IchimokuCloudScenario.MODERATELY_BEARISH:
                            output = TrendCros.CONSOLIDATION_BEARISH_TO_MODERATELY_BEARISH;
                            break;
                        case IchimokuCloudScenario.MODERATELY_BULLISH:
                            output = TrendCros.CONSOLIDATION_BEARISH_TO_MODERATELY_BULLISH;
                            break;
                        case IchimokuCloudScenario.STRONG_BEARISH:
                            output = TrendCros.CONSOLIDATION_BEARISH_TO_STRONG_BEARISH;
                            break;
                    }
                    break;
                default:
                    output = TrendCros.UNDEFINED;
                    break;
            }

            return output;
        }
        #endregion

    }
}
