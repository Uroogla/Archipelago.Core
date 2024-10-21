using Archipelago.Core.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archipelago.Core.Traps
{
    public class LagTrap : IDisposable
    {
        private readonly LagSimulator lagSimulator;
        private readonly TimeSpan duration;
        private Task simulationTask;
        public LagTrap(TimeSpan? duration = null)
        {
            this.duration = duration ?? TimeSpan.FromSeconds(10);
            lagSimulator = new LagSimulator();
        }
        public void Start()
        {
            lagSimulator.Start(minLagMs: 50, maxLagMs: 300, frequency: 20);

            simulationTask = Task.Delay(duration).ContinueWith(_ =>
            {
                lagSimulator.Stop();
            }, TaskScheduler.Default);
        }
        public async Task WaitForCompletionAsync()
        {
            if (simulationTask != null)
            {
                await simulationTask;
            }
        }
        public void Dispose()
        {
            lagSimulator.Stop();
        }
    }
}
