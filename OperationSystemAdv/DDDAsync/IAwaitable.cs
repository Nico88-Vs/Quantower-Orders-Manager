using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DivergentStrV0_1.OperationSystemAdv.DDDAsync
{
    internal interface IAwaitable
    {
        CancellationToken Canc_Token { get; }
        bool Proced { get; }
        TimeSpan TimeOut { get; set; }
        bool IsWaitinTurnedOn { get; }
    }
}
