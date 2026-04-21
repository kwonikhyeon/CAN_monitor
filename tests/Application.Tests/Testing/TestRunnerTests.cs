using CanMonitor.Application.Testing;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Xunit;

namespace CanMonitor.Application.Tests.Testing;

public sealed class TestRunnerTests
{
    private sealed class AlwaysPass<TStep> : IStepExecutor<TStep> where TStep : TestStep
    {
        public Type StepType => typeof(TStep);
        public int CallCount { get; private set; }
        public Task<StepOutcome> ExecuteAsync(TStep step, ITestRunnerContext ctx, CancellationToken ct)
        { CallCount++; return Task.FromResult(StepOutcome.Passed); }
        public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext ctx, CancellationToken ct)
            => ExecuteAsync((TStep)step, ctx, ct);
    }

    private sealed class AlwaysFail<TStep> : IStepExecutor<TStep> where TStep : TestStep
    {
        public Type StepType => typeof(TStep);
        public Task<StepOutcome> ExecuteAsync(TStep step, ITestRunnerContext ctx, CancellationToken ct)
            => Task.FromResult(StepOutcome.Failed);
        public Task<StepOutcome> ExecuteAsync(TestStep step, ITestRunnerContext ctx, CancellationToken ct)
            => ExecuteAsync((TStep)step, ctx, ct);
    }

    private static async Task<ITestRunnerContext> MakeContextAsync()
    {
        var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        return new TestRunnerContext(
            bus,
            new Subject<SignalValue>(),
            new Subject<AlarmState>(),
            new NoopDecoder());
    }

    private sealed class NoopDecoder : ISignalDecoder
    {
        public IReadOnlyList<SignalValue> Decode(CanFrame frame) => Array.Empty<SignalValue>();
    }

    private static TestCase Case(params TestStep[] steps) =>
        new("TC-X", "Unit", "x", Array.Empty<TestStep>(), steps);

    [Fact]
    public async Task All_steps_pass_returns_Passed()
    {
        var ctx = await MakeContextAsync();
        var runner = new TestRunner(new IStepExecutor[] { new AlwaysPass<WaitStep>() }, ctx);

        var result = await runner.RunAsync(Case(new WaitStep(TimeSpan.Zero), new WaitStep(TimeSpan.Zero)));

        result.Outcome.Should().Be(TestOutcome.Passed);
        result.StepLog.Should().HaveCount(2).And.OnlyContain(p => p.Outcome == StepOutcome.Passed);
    }

    [Fact]
    public async Task First_failing_step_stops_run_and_returns_Failed()
    {
        var ctx = await MakeContextAsync();
        var runner = new TestRunner(new IStepExecutor[] { new AlwaysFail<WaitStep>() }, ctx);

        var result = await runner.RunAsync(Case(new WaitStep(TimeSpan.Zero), new WaitStep(TimeSpan.Zero)));

        result.Outcome.Should().Be(TestOutcome.Failed);
        result.StepLog.Should().HaveCount(1).And.OnlyContain(p => p.Outcome == StepOutcome.Failed);
    }

    [Fact]
    public async Task Unknown_step_type_throws_NotSupportedException()
    {
        var ctx = await MakeContextAsync();
        var runner = new TestRunner(Array.Empty<IStepExecutor>(), ctx);

        var act = () => runner.RunAsync(Case(new WaitStep(TimeSpan.Zero)));

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*WaitStep*");
    }
}
