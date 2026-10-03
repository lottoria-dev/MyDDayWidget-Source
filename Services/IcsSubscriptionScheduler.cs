using System;
using System.Threading;
using System.Windows.Threading;

namespace DDay3.Services
{
    internal sealed class IcsSubscriptionScheduler : IDisposable
    {
        private readonly IcsCalendarService service;
        private readonly DispatcherTimer timer;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool busy, disposed;
        internal IcsSubscriptionScheduler(IcsCalendarService service, Dispatcher dispatcher)
        {
            this.service = service;
            timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMinutes(1) };
            timer.Tick += Tick; timer.Start();
            dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => Tick(this, EventArgs.Empty)));
        }
        private async void Tick(object sender, EventArgs e)
        {
            if (busy || disposed) return;
            busy = true;
            try { await service.RefreshDue(lifetime.Token); }
            catch (OperationCanceledException) { }
            catch (Exception) { /* No exception text: a feed address may contain a secret. Retry on a later tick. */ }
            finally { busy = false; }
        }
        public void Dispose()
        { if (disposed) return; disposed = true; timer.Stop(); timer.Tick -= Tick; lifetime.Cancel(); }
    }
}
