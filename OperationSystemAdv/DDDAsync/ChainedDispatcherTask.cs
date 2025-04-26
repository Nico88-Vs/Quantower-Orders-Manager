using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DivergentStrV0_1.OperationSystemAdv.DDDAsync
{
    public class ChainedDispatcherTask : IDispatcherTask
    {
        public string Name { get; }
        private readonly List<Func<CancellationToken, Task>> _steps;

        public event Action<IDispatcherTask, bool, Exception?> OnCompleted;

        public ChainedDispatcherTask(string name)
        {
            Name = name;
            _steps = new List<Func<CancellationToken, Task>>();
        }

        public void AddStep(Func<CancellationToken, Task> step)
        {
            _steps.Add(step);
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            try
            {
                foreach (var step in _steps)
                {
                    token.ThrowIfCancellationRequested();
                    await step(token);
                }
                OnCompleted?.Invoke(this, true, null);
            }
            catch (Exception ex)
            {
                OnCompleted?.Invoke(this, false, ex);
            }
        }
    }
}
