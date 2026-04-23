using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Xunit;

namespace CanMonitor.Application.Tests.Testing.Executors;

public sealed class SendCanFrameStepExecutorTests
{
    private sealed class NoopDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
        public IObservable<CanFrame> UnknownFrames => Observable.Never<CanFrame>();
    }

    [Fact]
    public async Task Sends_frame_through_context_bus()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var ctx = new TestRunnerContext(bus, new Subject<SignalValue>(), new Subject<AlarmState>(), new NoopDecoder());
        var exec = new SendCanFrameStepExecutor();

        var sent = new List<uint>();
        using var _ = bus.Frames.Subscribe(f => { if (f.Direction == CanDirection.Tx) sent.Add(f.Id); });

        var outcome = await exec.ExecuteAsync(
            new SendCanFrameStep(0x123, false, new byte[] { 1, 2, 3 }),
            ctx, CancellationToken.None);

        outcome.Should().Be(StepOutcome.Passed);
        sent.Should().ContainSingle().Which.Should().Be(0x123u);
    }
}
