using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Testing;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Testing.Executors;

public sealed class ManualConfirmStepExecutorTests
{
    [Fact]
    public async Task Returns_ManualRequired()
    {
        var exec = new ManualConfirmStepExecutor();
        var outcome = await exec.ExecuteAsync(
            new ManualConfirmStep("전원 공급기 전압을 12V로 맞추세요"),
            context: null!, CancellationToken.None);

        outcome.Should().Be(StepOutcome.ManualRequired);
    }
}
