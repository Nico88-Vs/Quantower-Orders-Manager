using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.C_Obj
{

    public enum cloudLineReference
    {
        fast,
        slow
    }
    public class TF
    {
        public int Delay { get; set; }
        public TimeFrame Timeframe { get; set; }
        public Indicator Indicatore { get; private set; }
        public int FastSeries { get; private set; }
        public int SlowSeries { get; private set; }

        public TF()
        {

        }

        public TF(TimeFrame timeframe, int delay, Indicator indi, int fast, int slow)
        {
            Delay = delay;
            Timeframe = timeframe;
            Indicatore = indi;
            FastSeries = fast;
            SlowSeries = slow;
        }
        public enum TimeFrame
        {
            Fast,
            Mid,
            Slow,
        }

        public double ReturnLast(cloudLineReference lineRef)
        {
            var idx = lineRef == cloudLineReference.fast ? this.FastSeries : this.SlowSeries;
            return this.Indicatore.GetValue(lineIndex : idx);
        }
        
        public double ReturnCurrent(cloudLineReference lineRef, int tenkanperiod)
        {
            var idx = lineRef == cloudLineReference.fast ? this.FastSeries : this.SlowSeries;
            var ofset = this.GetCorrectBuffer(tenkanperiod);
            return this.Indicatore.GetValue(lineIndex : idx, offset: ofset);
        }

        public int GetCorrectBuffer(int tenkanperiod, int buffer)
        {
            return buffer + tenkanperiod * Delay;
        }

        public int GetCorrectBuffer(int tenkanperiod)
        {
            return tenkanperiod * Delay;
        }
    }
}
