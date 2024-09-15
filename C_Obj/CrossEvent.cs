using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DivergentStrV0_1.C_Obj
{
    public enum EventCrosArg
    {
        Gold_fast,
        Dead_fast,
        Gold_midt,
        Dead_mid,
        Gold_slow,
        Dead_slow,
        Unkown
    }
    public class CrossEvent : EventArgs
    {
        public EventCrosArg Args { get; set; }
        public double Price { get; set; }
        public int BarIndex { get; set; }
        public Cloud CurrentCloud { get; private set; }

        public CrossEvent(EventCrosArg arg, double price, int barIndex, Cloud currentCloud)
        {
            Price = price;
            Args = arg;
            BarIndex = barIndex;
            CurrentCloud = currentCloud;
        }
    }
}
