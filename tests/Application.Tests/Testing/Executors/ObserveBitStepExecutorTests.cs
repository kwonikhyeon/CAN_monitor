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

public sealed class ObserveBitStepExecutorTests
{
    private sealed class NoopDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
    }

    private static async Task<(ITestRunnerContext ctx, Subject<SignalValue> signals)> MkAsync()
    {
        var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        var signals = new Subject<SignalValue>();
        return (new TestRunnerContext(bus, signals, new Subject<AlarmState>(), new NoopDecoder()), signals);
    }

    [Fact]
    public async Task Passes_when_bit_becomes_expected()
    {
        var (ctx, signals) = await MkAsync();
        var exec = new ObserveBitStepExecutor();
        var step = new ObserveBitStep("M", "S", true, TimeSpan.FromMilliseconds(200));

        _ = Task.Run(async () =>
        {
            await Task.Delay(20);
            signals.OnNext(new SignalValue("M", "S", 1, 1, null, DateTimeOffset.UtcNow));
        });

        (await exec.ExecuteAsync(step, ctx, CancellationToken.None)).Should().Be(StepOutcome.Passed);
    }

    [Fact]
    public async Task Fails_when_bit_never_becomes_expected()
    {
        var (ctx, signals) = await MkAsync();
        var exec = new ObserveBitStepExecutor();
        var step = new ObserveBitStep("M", "S", true, TimeSpan.FromMilliseconds(80));

        _ = Task.Run(async () =>
        {
            await Task.Delay(10);
            signals.OnNext(new SignalValue("M", "S", 0, 0, null, DateTimeOffset.UtcNow));
        });

        (await exec.ExecuteAsync(step, ctx, CancellationToken.None)).Should().Be(StepOutcome.Failed);
    }
}
