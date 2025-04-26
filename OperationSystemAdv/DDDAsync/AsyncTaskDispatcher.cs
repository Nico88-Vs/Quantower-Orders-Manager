using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DivergentStrV0_1.OperationSystemAdv.DDDAsync
{
    public class AsyncTaskDispatcher
    {
        private readonly List<IDispatcherTask> activeTasks = new();
        private readonly object lockObj = new();

        public void Register(IDispatcherTask task, TimeSpan? timeout = null)
        {
            var cts = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : new CancellationTokenSource();
            lock (lockObj)
            {
                activeTasks.Add(task);
            }

            task.OnCompleted += (t, success, ex) =>
            {
                lock (lockObj)
                {
                    activeTasks.Remove(t);
                }

                //TODO: Handle resoult

                if (success)
                    Console.WriteLine($"✅ Task {t.Name} completed successfully.");
                else
                    Console.WriteLine($"❌ Task {t.Name} failed: {ex?.Message}");
            };

            _ = Task.Run(async () =>
            {
                try
                {
                    await task.ExecuteAsync(cts.Token);

                    #region 🐞 BUG [Bug noto da risolvere]
                    //task.OnCompleted?.Invoke(task, true, null);
                    //Spostare l'invocazione di OnCompleted qui per evitare che venga chiamata più volte
                    #endregion


                }
                catch (Exception ex)
                {
                    //task.OnCompleted.Invoke(task, false, ex);
                }
            });
        }

        public bool HasRunningTasks()
        {
            lock (lockObj)
                return activeTasks.Any();
        }

        public void CancelAll()
        {
            lock (lockObj)
            {
                activeTasks.Clear();
            }
        }
    }
}
