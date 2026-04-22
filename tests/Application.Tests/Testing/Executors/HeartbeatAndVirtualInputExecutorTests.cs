using CanMonitor.Application.Can;
using CanMonitor.Application.Services;
using CanMonitor.Application.Testing.Executors;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Core.Testing;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Testing.Executors;

public sealed class HeartbeatAndVirtualInputExecutorTests
{
    [Fact]
    public async Task SetHeartbeat_toggles_matching_provider()
    {
        using var eec1 = new Eec1HeartbeatProvider();   // starts Enabled=true
        var exec = new SetHeartbeatStepExecutor(new IBusHeartbeatProvider[] { eec1 });

        var outcome = await exec.ExecuteAsync(
            new SetHeartbeatStep("EEC1", false), context: null!, CancellationToken.None);

        outcome.Should().Be(StepOutcome.Passed);
        eec1.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task SetHeartbeat_fails_when_name_not_registered()
    {
        var exec = new SetHeartbeatStepExecutor(Array.Empty<IBusHeartbeatProvider>());
        var outcome = await exec.ExecuteAsync(
            new SetHeartbeatStep("Unknown", true), context: null!, CancellationToken.None);
        outcome.Should().Be(StepOutcome.Failed);
    }
}
