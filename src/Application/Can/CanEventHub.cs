using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class CanEventHub : IDisposable
{
    private readonly Subject<CanFrame> _frames = new();
    private readonly Subject<SignalValue> _signals = new();
    private readonly Subject<AlarmState> _alarms = new();
    private readonly Subject<BusStatusChange> _bus = new();
    private readonly SerialDisposable _currentBinding = new();

    public IObservable<CanFrame> Frames => _frames.AsObservable();
    public IObservable<SignalValue> Signals => _signals.AsObservable();
    public IObservable<AlarmState> Alarms => _alarms.AsObservable();
    public IObservable<BusStatusChange> Bus => _bus.AsObservable();

    public IDisposable Attach(
        ICanBus bus,
        IObservable<SignalValue> signals,
        IObservable<AlarmState> alarms,
        IObservable<BusStatusChange> busStatus)
        => AttachRawStreams(bus.Frames, signals, alarms, busStatus);

    public IDisposable AttachRawStreams(
        IObservable<CanFrame> frames,
        IObservable<SignalValue> signals,
        IObservable<AlarmState> alarms,
        IObservable<BusStatusChange> busStatus)
    {
        var binding = new CompositeDisposable(
            frames.Subscribe(_frames.OnNext, _ => { }, () => { }),
            signals.Subscribe(_signals.OnNext, _ => { }, () => { }),
            alarms.Subscribe(_alarms.OnNext, _ => { }, () => { }),
            busStatus.Subscribe(_bus.OnNext, _ => { }, () => { }));

        _currentBinding.Disposable = binding;
        return binding;
    }

    public void Dispose()
    {
        _currentBinding.Dispose();
        _frames.Dispose();
        _signals.Dispose();
        _alarms.Dispose();
        _bus.Dispose();
    }
}
