using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.C_Obj
{
    public class GapEventArgs : EventArgs
    {
        public TF.TimeFrame Timeframe { get; set; }
        public Side Side { get; set; }
        public GapEventArgs(TF.TimeFrame tf, Side s)
        {
            this.Timeframe = tf;
            this.Side = s;
        }
    }
}
