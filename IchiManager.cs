using DivergentStrV0_1.C_Obj;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1
{
    public class IchiManager
    {
        public CloudSeries CloudSeries { get; set; }
        public List<TF> TFs { get; set; }

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


            this.CloudSeries = new CloudSeries(this._hd, fast, mid, slow);

            this.CloudSeries.GenerateCloud(TFs);

            this.CloudSeries.Cross += this.CloudSeries_Cross;
            this.CloudSeries.TrendCross += this.CloudSeries_TrendCross;
        }

        private void CloudSeries_TrendCross(object sender, TrendEvent e) => throw new NotImplementedException();
        private void CloudSeries_Cross(object sender, CrossEvent e) => throw new NotImplementedException();

        public void Stop()
        {
            this.CloudSeries.Cross -= this.CloudSeries_Cross;
            this.CloudSeries.TrendCross -= this.CloudSeries_TrendCross;
        }

        public void Update()
        {
            foreach (TF tf in TFs)
                this.CloudSeries.Update(tf);
        }
    }
}
