using System.Reactive.Concurrency;
using System.Reactive.Linq;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class CanReceivePipeline
{
    private readonly IObservable<SignalValue> _decoded;

    public CanReceivePipeline(
        IObservable<CanFrame> source,
        ISignalDecoder decoder,
        IAlarmEngine alarmEngine,
        IScheduler? scheduler = null)
    {
        var runOn = scheduler ?? TaskPoolScheduler.Default;
        _decoded = source
            .ObserveOn(runOn)
            .SelectMany(frame => decoder.Decode(frame))
            .Do(alarmEngine.Submit)
            .Publish()
            .RefCount();
    }

    public IObservable<SignalValue> DecodedSignals => _decoded;
}
