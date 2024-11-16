using DivergentStrV0_1.C_Obj;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.Utils
{

    public class IchiManager
    {
        public CloudSeries CloudSeries { get; set; }
        public List<TF> TFs { get; set; }
        private Indicator _ichi;
        private bool running = false;

        public event EventHandler<GapEventArgs> GapDetected;

        public IchimokuCloudScenario Scenario
        {
            get
            {
                return CloudSeries.Scenario;
            }
        }

        private HistoricalData _hd;
        private int tenkanperiod;

        public IchiManager(Indicator Ichimoku, HistoricalData hd)
        {
            _hd = hd;
            TFs = new List<TF>();

            int multiplaierMid = (int)Ichimoku.Settings.FirstOrDefault(x => x.Name == "Multiplaier").Value;
            int multiplaierSlow = (int)Ichimoku.Settings.FirstOrDefault(x => x.Name == "MultiplaierSecondo").Value;
            tenkanperiod = (int)Ichimoku.Settings.FirstOrDefault(x => x.Name == "Tenkan Sen").Value;

            TF fast = new TF(TF.TimeFrame.Fast, 1, Ichimoku, Convert.ToInt32(IchiLineIndex.Senkou_SpanA0), Convert.ToInt32(IchiLineIndex.Senkou_SpanB0));
            TFs.Add(fast);
            TF mid = new TF(TF.TimeFrame.Mid, multiplaierMid, Ichimoku, Convert.ToInt32(IchiLineIndex.Senkou_SpanA), Convert.ToInt32(IchiLineIndex.Senkou_SpanB));
            TFs.Add(mid);
            TF slow = new TF(TF.TimeFrame.Slow, multiplaierSlow, Ichimoku, Convert.ToInt32(IchiLineIndex.Senkou_SpanA2), Convert.ToInt32(IchiLineIndex.Senkou_SpanB2));
            TFs.Add(slow);

            _ichi = Ichimoku;

            CloudSeries = new CloudSeries(_hd, fast, mid, slow);

            CloudSeries.GenerateCloud(TFs);
        }

        public void Stop()
        {
        }
        public void Update()
        {
            running = true;
            foreach (TF tf in TFs)
                CloudSeries.Update(tf);


            if (_ichi.LinesSeries[Convert.ToInt32(IchiLineIndex.LonGap)].GetValue() > 0)
            {
                GapEventArgs args = new GapEventArgs(TF.TimeFrame.Mid, Side.Buy);
                OnGap(args);
            }
            if (_ichi.LinesSeries[Convert.ToInt32(IchiLineIndex.ShortGap)].GetValue() > 0)
            {
                GapEventArgs args = new GapEventArgs(TF.TimeFrame.Mid, Side.Sell);
                OnGap(args);
            }
            if (_ichi.LinesSeries[Convert.ToInt32(IchiLineIndex.LonGap_Bigger)].GetValue() < 100000)
            {
                GapEventArgs args = new GapEventArgs(TF.TimeFrame.Slow, Side.Buy);
                OnGap(args);
            }
            if (_ichi.LinesSeries[Convert.ToInt32(IchiLineIndex.ShortGap_Bigger)].GetValue() < 100000)
            {
                GapEventArgs args = new GapEventArgs(TF.TimeFrame.Slow, Side.Sell);
                OnGap(args);
            }
        }

        public virtual void OnGap(GapEventArgs e)
        {
            GapDetected?.Invoke(this, e);
        }

    }
}
