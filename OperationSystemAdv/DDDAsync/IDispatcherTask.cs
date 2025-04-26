using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DivergentStrV0_1.OperationSystemAdv.DDDAsync
{
    public interface IDispatcherTask
    {
        string Name { get; }
        Task ExecuteAsync(CancellationToken token);
        event Action<IDispatcherTask, bool, Exception?> OnCompleted;
    }
}
