using DivergentStrV0_1.C_Obj;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1
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
            this.tenkanperiod = (int)Ichimoku.Settings.FirstOrDefault(x => x.Name == "Tenkan Sen").Value;

            TF fast = new TF(TF.TimeFrame.Fast, 1, Ichimoku, Convert.ToInt32(IchiLineIndex.Senkou_SpanA0), Convert.ToInt32(IchiLineIndex.Senkou_SpanB0));
            TFs.Add(fast);
            TF mid = new TF(TF.TimeFrame.Mid, multiplaierMid, Ichimoku, Convert.ToInt32(IchiLineIndex.Senkou_SpanA), Convert.ToInt32(IchiLineIndex.Senkou_SpanB));
            TFs.Add(mid);
            TF slow = new TF(TF.TimeFrame.Slow, multiplaierSlow, Ichimoku, Convert.ToInt32(IchiLineIndex.Senkou_SpanA2), Convert.ToInt32(IchiLineIndex.Senkou_SpanB2));
            TFs.Add(slow);

            this._ichi = Ichimoku;

            this.CloudSeries = new CloudSeries(this._hd, fast, mid, slow);

            this.CloudSeries.GenerateCloud(TFs);

            //this.CloudSeries.Cross += this.CloudSeries_Cross;
            //this.CloudSeries.TrendCross += this.CloudSeries_TrendCross;
        }

        //private void CloudSeries_TrendCross(object sender, TrendEvent e) => throw new NotImplementedException();
        //private void CloudSeries_Cross(object sender, CrossEvent e)
        //{
        //    if (this.running)
        //    {
        //        if (this.Scenario == IchimokuCloudScenario.STRONG_BULLISH & e.Args == EventCrosArg.Dead_fast)
        //        {
        //            try
        //            {
        //                double slprice = e.CurrentCloud.MaximaFast.Last().Value;
        //                //TODO:attenzione che le basi sono mescolate
        //                double tPrice = this.CloudSeries.MidCloudDictionary.FirstOrDefault(x => x.Value.Contains(e.CurrentCloud)).Key.BasesList.Last().Value;
        //                var sl = SlTpHolder.CreateSL(slprice, isTrailing: false);
        //                var tp = SlTpHolder.CreateTP(tPrice);

        //                PositionManager.CreateRequest(Side.Sell, e.Price, sl, tp);
        //            }
        //            catch (Exception ex)
        //            {
        //                Core.Instance.Loggers.Log($"Error Placing order with ichi message : {ex.Message}");
        //            }
        //        }

        //        if (this.Scenario == IchimokuCloudScenario.STRONG_BEARISH & e.Args == EventCrosArg.Gold_fast)
        //        {
        //            try
        //            {
        //                double slprice = e.CurrentCloud.MinimaFast.Last().Value;
        //                //TODO:attenzione che le basi sono mescolate
        //                double tPrice = this.CloudSeries.MidCloudDictionary.FirstOrDefault(x => x.Value.Contains(e.CurrentCloud)).Key.BasesList.Last().Value;
        //                var sl = SlTpHolder.CreateSL(slprice, isTrailing: false);
        //                var tp = SlTpHolder.CreateTP(tPrice);

        //                PositionManager.CreateRequest(Side.Buy, e.Price, sl, tp);
        //            }
        //            catch (Exception ex)
        //            {
        //                Core.Instance.Loggers.Log($"Error Placing order with ichi message : {ex.Message}");
        //            }
        //        }
        //    }
        //}
        public void Stop()
        {
            //this.CloudSeries.Cross -= this.CloudSeries_Cross;
            //this.CloudSeries.TrendCross -= this.CloudSeries_TrendCross;
        }
        public void Update()
        {
            this.running = true;
            foreach (TF tf in TFs)
                this.CloudSeries.Update(tf);


            if (_ichi.LinesSeries[Convert.ToInt32(IchiLineIndex.LonGap)].GetValue() > 0)
            {
                GapEventArgs args = new GapEventArgs(TF.TimeFrame.Fast, Side.Buy);
                this.OnGap(args);
            }
            if (_ichi.LinesSeries[Convert.ToInt32(IchiLineIndex.ShortGap)].GetValue() > 0)
            {
                GapEventArgs args = new GapEventArgs(TF.TimeFrame.Fast, Side.Sell);
                this.OnGap(args);
            }
            if (_ichi.LinesSeries[Convert.ToInt32(IchiLineIndex.LonGap_Bigger)].GetValue() < 100000)
            {
                GapEventArgs args = new GapEventArgs(TF.TimeFrame.Mid, Side.Buy);
                this.OnGap(args);
            }
            if (_ichi.LinesSeries[Convert.ToInt32(IchiLineIndex.ShortGap_Bigger)].GetValue() < 100000)
            {
                GapEventArgs args = new GapEventArgs(TF.TimeFrame.Mid, Side.Sell);
                this.OnGap(args);
            }
        }
                   
        public virtual void OnGap(GapEventArgs e)
        {
            GapDetected?.Invoke(this, e);
        }

    }
}
