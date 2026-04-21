using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Testing;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Testing.Executors;

public sealed class WaitStepExecutorTests
{
    [Fact]
    public async Task Waits_for_requested_duration()
    {
        var exec = new WaitStepExecutor();
        var start = DateTimeOffset.UtcNow;
        var outcome = await exec.ExecuteAsync(
            new WaitStep(TimeSpan.FromMilliseconds(100)), context: null!, CancellationToken.None);

        (DateTimeOffset.UtcNow - start).Should().BeGreaterThan(TimeSpan.FromMilliseconds(80));
        outcome.Should().Be(StepOutcome.Passed);
    }

    [Fact]
    public async Task Respects_cancellation()
    {
        var exec = new WaitStepExecutor();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(20);

        var act = () => exec.ExecuteAsync(new WaitStep(TimeSpan.FromSeconds(5)), null!, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
