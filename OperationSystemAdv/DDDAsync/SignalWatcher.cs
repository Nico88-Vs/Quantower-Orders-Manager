using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DivergentStrV0_1.OperationSystemAdv.DDDAsync
{

    public class SignalWatcher
    {
        private readonly Thread _worker;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<string, AsyncSignal> _signals = new();
        private readonly ConcurrentDictionary<string, Func<bool>> _conditions = new();

        public SignalWatcher()
        {
            _worker = new Thread(() =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    foreach (var kvp in _conditions)
                    {
                        var key = kvp.Key;
                        var condition = kvp.Value;

                        if (condition() && _signals.TryGetValue(key, out var signal))
                        {
                            signal.Signal();
                        }
                    }

                    Thread.Sleep(50); // controllo a intervalli regolari
                }
            });

            _worker.IsBackground = true;
            _worker.Name = "SignalWatcher";
            _worker.Start();
        }

        public void RegisterCondition(string key, Func<bool> condition)
        {
            _conditions[key] = condition;
            _signals.TryAdd(key, new AsyncSignal());
        }

        public Task WaitAsync(string key, CancellationToken token = default)
        {
            if (_signals.TryGetValue(key, out var signal))
            {
                return signal.WaitAsync(token);
            }

            throw new InvalidOperationException($"Signal with key '{key}' is not registered.");
        }

        public void Stop()
        {
            _cts.Cancel();
        }
    }
}
