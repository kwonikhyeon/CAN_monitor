using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Wpf.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CanMonitor.Wpf.Shell;

public sealed partial class StatusBarViewModel : ObservableObject, IDisposable
{
    private readonly CompositeDisposable _subscriptions = new();

    [ObservableProperty] private int _rxPerSecond;
    [ObservableProperty] private int _txPerSecond;
    [ObservableProperty] private long _droppedFrames;
    [ObservableProperty] private long _decodeFailures;
    [ObservableProperty] private int _activeAlarms;
    [ObservableProperty] private string _dbcFileLabel = string.Empty;
    [ObservableProperty] private ConnectionState _session;

    public StatusBarViewModel(
        CanEventHub hub,
        RawFrameStore store,
        IAlarmEngine alarmEngine,
        ISessionState sessionState,
        IScheduler uiScheduler)
    {
        var window = TimeSpan.FromSeconds(1);

        _subscriptions.Add(hub.Frames
            .Where(f => f.Direction == CanDirection.Rx)
            .Buffer(window, uiScheduler)
            .Subscribe(batch => RxPerSecond = batch.Count));

        _subscriptions.Add(hub.Frames
            .Where(f => f.Direction == CanDirection.Tx)
            .Buffer(window, uiScheduler)
            .Subscribe(batch => TxPerSecond = batch.Count));

        _subscriptions.Add(Observable
            .Interval(window, uiScheduler)
            .Select(_ => store.DroppedCount)
            .Subscribe(v => DroppedFrames = v));

        // DecodeFailures: SignalValue에 Status 속성이 없어 Phase 3a에서는 0으로 유지.
        // Phase 3b에서 SignalValue에 Status 필드 추가 후 해당 Where+Scan 블록 복구.

        var initialActive = alarmEngine.CurrentAlarms.Count(a => a.Active);
        ActiveAlarms = initialActive;
        _subscriptions.Add(alarmEngine.AlarmChanges
            .Scan(initialActive, (count, alarm) => alarm.Active ? count + 1 : Math.Max(count - 1, 0))
            .ObserveOn(uiScheduler)
            .Subscribe(v => ActiveAlarms = v));

        _subscriptions.Add(sessionState.StateChanges
            .Subscribe(s => Session = s));

        _subscriptions.Add(sessionState.DbcChanges
            .Select(d => d?.DisplayName ?? string.Empty)
            .Subscribe(label => DbcFileLabel = label));
    }

    public void Dispose() => _subscriptions.Dispose();
}
