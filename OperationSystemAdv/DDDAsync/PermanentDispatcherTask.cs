using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TradingPlatform.BusinessLayer;

namespace DivergentStrV0_1.OperationSystemAdv.DDDAsync
{
    internal class PermanentDispatcherTask : IDispatcherTask
    {
        public string Name { get; }
        private readonly List<Func<CancellationToken, Task>> _steps;

        public event Action<IDispatcherTask, bool, Exception?> OnCompleted;
        private bool _run;
        public bool Running { get; private set; }
        private Thread worker;

        public PermanentDispatcherTask(string name)
        {
            Name = name;
            _steps = new List<Func<CancellationToken, Task>>();
            this._run = false;
            this.Running = false;
        }

        public async void Start()
        {
            if (!_run)
                _run = true;

            //if (!Running)
            //    this.ExecuteAsync();
        }

        public void AddStep(Func<CancellationToken, Task> step)
        {
            _steps.Add(step);
        }

        public async Task ExecuteAsync(CancellationToken token)
        {
            try
            {
                this.Running = true;
                worker = new Thread(() =>
                {
                    while (this.Running)
                    {
                        try
                        {
                            foreach (var step in _steps)
                            {
                                token.ThrowIfCancellationRequested();
                                step(token);
                            }
                            OnCompleted?.Invoke(this, true, null);

                        }
                        catch (Exception ex)
                        {
                            OnCompleted?.Invoke(this, true, ex);
                        }
                    }
                });

                worker.IsBackground = true;
                worker.Name = "VolumeProfileWatcher";
                worker.Start();
            }
            catch (Exception ex)
            {
                OnCompleted?.Invoke(this, false, ex);
            }
        }
    }
}
