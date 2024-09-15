using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DivergentStrV0_1.C_Obj
{
    public enum NewTradeSenderType
    {
        Volume,
        IchimokuGap
    }

    public class NewTradEventArg : EventArgs
    {
        public double Level { get; set; }
        public NewTradeSenderType SenderType { get; set; }
        public object Args { get; set; }

        public NewTradEventArg(NewTradeSenderType senderType, object agr = null)
        {
            SenderType = senderType;
            Args = agr;
        }
    }
}
