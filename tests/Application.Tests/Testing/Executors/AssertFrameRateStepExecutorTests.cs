using CanMonitor.Application.Testing;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using System.Reactive.Subjects;
using Xunit;

namespace CanMonitor.Application.Tests.Testing.Executors;

public sealed class AssertFrameRateStepExecutorTests
{
    private sealed class NoopDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
    }

    [Fact]
    public async Task Passed_when_rate_within_tolerance()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var ctx = new TestRunnerContext(bus, new Subject<SignalValue>(), new Subject<AlarmState>(), new NoopDecoder());
        var exec = new AssertFrameRateStepExecutor();

        var cts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                bus.Inject(new CanFrame(0x200, false, new byte[] { 1 }, DateTimeOffset.UtcNow, CanDirection.Rx));
                await Task.Delay(10);
            }
        });

        var outcome = await exec.ExecuteAsync(
            new AssertFrameRateStep(0x200, 100, TolerancePct: 25),
            ctx, CancellationToken.None);
        cts.Cancel();

        outcome.Should().Be(StepOutcome.Passed);
    }

    [Fact]
    public async Task Failed_when_rate_below_tolerance()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var ctx = new TestRunnerContext(bus, new Subject<SignalValue>(), new Subject<AlarmState>(), new NoopDecoder());
        var exec = new AssertFrameRateStepExecutor();

        var outcome = await exec.ExecuteAsync(
            new AssertFrameRateStep(0x999, 100, TolerancePct: 10),
            ctx, CancellationToken.None);

        outcome.Should().Be(StepOutcome.Failed);
    }
}
