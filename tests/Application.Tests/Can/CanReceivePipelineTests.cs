using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class CanReceivePipelineTests
{
    private sealed class FakeDecoder : ISignalDecoder
    {
        private readonly IReadOnlyList<SignalValue> _out;
        public FakeDecoder(IReadOnlyList<SignalValue> output) { _out = output; }
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => _out;
        public IObservable<CanFrame> UnknownFrames => Observable.Never<CanFrame>();
    }

    private sealed class RecordingAlarm : IAlarmEngine
    {
        public List<SignalValue> Submitted { get; } = new();
        public IObservable<AlarmState> AlarmChanges => Observable.Never<AlarmState>();
        public IReadOnlyCollection<AlarmState> CurrentAlarms => Array.Empty<AlarmState>();
        public void Submit(SignalValue value) => Submitted.Add(value);
    }

    private static CanFrame Fr() =>
        new(0x100, false, new byte[] { 1 }, DateTimeOffset.UtcNow, CanDirection.Rx);

    [Fact]
    public void Decoded_signals_are_forwarded_in_order()
    {
        var source = new Subject<CanFrame>();
        var values = new[]
        {
            new SignalValue("M", "A", 1, 1, null, DateTimeOffset.UtcNow),
            new SignalValue("M", "B", 2, 2, null, DateTimeOffset.UtcNow),
        };
        var decoder = new FakeDecoder(values);
        var alarm = new RecordingAlarm();
        var pipeline = new CanReceivePipeline(source, decoder, alarm, ImmediateScheduler.Instance);

        var received = new List<string>();
        using var _ = pipeline.DecodedSignals.Subscribe(v => received.Add(v.SignalName));

        source.OnNext(Fr());

        received.Should().Equal(new[] { "A", "B" });
    }

    [Fact]
    public void Each_decoded_value_is_submitted_to_alarm_engine()
    {
        var source = new Subject<CanFrame>();
        var values = new[]
        {
            new SignalValue("M", "A", 1, 1, null, DateTimeOffset.UtcNow),
            new SignalValue("M", "B", 2, 2, null, DateTimeOffset.UtcNow),
        };
        var decoder = new FakeDecoder(values);
        var alarm = new RecordingAlarm();
        var pipeline = new CanReceivePipeline(source, decoder, alarm, ImmediateScheduler.Instance);

        using var _ = pipeline.DecodedSignals.Subscribe(_ => { });
        source.OnNext(Fr());
        source.OnNext(Fr());

        alarm.Submitted.Should().HaveCount(4);
    }
}
