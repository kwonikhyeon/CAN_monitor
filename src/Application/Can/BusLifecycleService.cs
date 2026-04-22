using System.Reactive.Disposables;
using CanMonitor.Core.Abstractions;

namespace CanMonitor.Application.Can;

public sealed class BusLifecycleService : IAsyncDisposable
{
    private readonly IBusHeartbeatProvider[] _providers;
    private readonly ITxScheduler _scheduler;
    private readonly object _gate = new();
    private readonly Dictionary<IBusHeartbeatProvider, IDisposable> _active = new();
    private readonly CompositeDisposable _subscriptions = new();
    private bool _started;

    public BusLifecycleService(IEnumerable<IBusHeartbeatProvider> providers, ITxScheduler scheduler)
    {
        _providers = providers.ToArray();
        _scheduler = scheduler;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_started) return;
            _started = true;

            foreach (var provider in _providers)
            {
                var p = provider;
                _subscriptions.Add(p.EnabledChanges.Subscribe(enabled => Reconcile(p, enabled)));
            }
        }
    }

    private void Reconcile(IBusHeartbeatProvider provider, bool enabled)
    {
        lock (_gate)
        {
            if (enabled)
            {
                if (_active.ContainsKey(provider)) return;
                _active[provider] = _scheduler.Schedule(provider.Name, provider.BuildFrame, provider.Period);
            }
            else
            {
                if (_active.Remove(provider, out var sub))
                    sub.Dispose();
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _subscriptions.Dispose();
            foreach (var sub in _active.Values) sub.Dispose();
            _active.Clear();
        }
        return ValueTask.CompletedTask;
    }
}
