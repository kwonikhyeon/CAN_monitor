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

public sealed class ObserveSignalStepExecutorTests
{
    private sealed class NoopDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
        public IObservable<CanFrame> UnknownFrames => Observable.Never<CanFrame>();
    }

    private static async Task<(ITestRunnerContext ctx, Subject<SignalValue> signals)> MkAsync()
    {
        var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var signals = new Subject<SignalValue>();
        return (new TestRunnerContext(bus, signals, new Subject<AlarmState>(), new NoopDecoder()), signals);
    }

    [Fact]
    public async Task Passed_when_matching_value_within_tolerance_before_timeout()
    {
        var (ctx, signals) = await MkAsync();
        var exec = new ObserveSignalStepExecutor();
        var step = new ObserveSignalStep("M", "S", 10, TimeSpan.FromMilliseconds(300), 0.5);

        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            signals.OnNext(new SignalValue("M", "S", 10.2, 10.2, null, DateTimeOffset.UtcNow));
        });

        var outcome = await exec.ExecuteAsync(step, ctx, CancellationToken.None);
        outcome.Should().Be(StepOutcome.Passed);
    }

    [Fact]
    public async Task Failed_when_value_is_outside_tolerance()
    {
        var (ctx, signals) = await MkAsync();
        var exec = new ObserveSignalStepExecutor();
        var step = new ObserveSignalStep("M", "S", 10, TimeSpan.FromMilliseconds(200), 0.1);

        _ = Task.Run(async () =>
        {
            await Task.Delay(30);
            signals.OnNext(new SignalValue("M", "S", 11.0, 11.0, null, DateTimeOffset.UtcNow));
        });

        var outcome = await exec.ExecuteAsync(step, ctx, CancellationToken.None);
        outcome.Should().Be(StepOutcome.Failed);
    }

    [Fact]
    public async Task Failed_when_nothing_arrives_within_window()
    {
        var (ctx, _) = await MkAsync();
        var exec = new ObserveSignalStepExecutor();
        var step = new ObserveSignalStep("M", "S", 10, TimeSpan.FromMilliseconds(80), 0.1);

        var outcome = await exec.ExecuteAsync(step, ctx, CancellationToken.None);
        outcome.Should().Be(StepOutcome.Failed);
    }
}
