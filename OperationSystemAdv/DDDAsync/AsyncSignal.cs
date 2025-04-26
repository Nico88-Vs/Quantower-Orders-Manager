using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DivergentStrV0_1.OperationSystemAdv.DDDAsync
{
    internal class AsyncSignal
    {
        private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Canc_Token { get; set; }

        public Task WaitAsync(CancellationToken token = default)
        {
            Canc_Token = token;
            return _tcs.Task.WaitAsync(Canc_Token);
        }

        public void Signal()
        {
            if (!_tcs.Task.IsCompleted)
                _tcs.SetResult(true);
        }
    }
}
