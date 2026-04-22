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

    [Fact]
    public async Task SetVirtualInput_applies_supplied_fields_only()
    {
        using var svc = new VirtualInputService();
        svc.Update(new VirtualInputState(ClutchPedalPercent: 30));
        var exec = new SetVirtualInputStepExecutor(svc);

        var outcome = await exec.ExecuteAsync(
            new SetVirtualInputStep(GearLever: "Neutral", PtoSwitch: true),
            context: null!, CancellationToken.None);

        outcome.Should().Be(StepOutcome.Passed);
        svc.Current.GearLever.Should().Be(GearLever.Neutral);
        svc.Current.PtoSwitch.Should().BeTrue();
        svc.Current.ClutchPedalPercent.Should().Be(30); // unchanged
    }

    [Theory]
    [InlineData("None",    GearLever.None)]
    [InlineData("Neutral", GearLever.Neutral)]
    [InlineData("Forward", GearLever.Forward)]
    [InlineData("Reverse", GearLever.Reverse)]
    [InlineData("N",       GearLever.Neutral)] // short alias
    [InlineData("F",       GearLever.Forward)]
    [InlineData("R",       GearLever.Reverse)]
    public async Task SetVirtualInput_parses_gear_lever_strings(string input, GearLever expected)
    {
        using var svc = new VirtualInputService();
        var exec = new SetVirtualInputStepExecutor(svc);

        var outcome = await exec.ExecuteAsync(
            new SetVirtualInputStep(GearLever: input), context: null!, CancellationToken.None);

        outcome.Should().Be(StepOutcome.Passed);
        svc.Current.GearLever.Should().Be(expected);
    }

    [Fact]
    public async Task SetVirtualInput_fails_on_unknown_gear_lever()
    {
        using var svc = new VirtualInputService();
        var exec = new SetVirtualInputStepExecutor(svc);

        var outcome = await exec.ExecuteAsync(
            new SetVirtualInputStep(GearLever: "Banana"), context: null!, CancellationToken.None);

        outcome.Should().Be(StepOutcome.Failed);
    }

    [Fact]
    public async Task EnterSimulationMode_activates_flag_and_enables_heartbeat()
    {
        using var svc = new VirtualInputService();
        using var vi = new VirtualInputHeartbeat(svc);
        var exec = new EnterSimulationModeStepExecutor(svc, new IBusHeartbeatProvider[] { vi });

        var outcome = await exec.ExecuteAsync(
            new EnterSimulationModeStep(), context: null!, CancellationToken.None);

        outcome.Should().Be(StepOutcome.Passed);
        svc.IsSimulationModeActive.Should().BeTrue();
        vi.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task ExitSimulationMode_deactivates_flag_and_disables_heartbeat()
    {
        using var svc = new VirtualInputService();
        using var vi = new VirtualInputHeartbeat(svc);
        await svc.EnterSimulationModeAsync();
        vi.SetEnabled(true);

        var exec = new ExitSimulationModeStepExecutor(svc, new IBusHeartbeatProvider[] { vi });
        var outcome = await exec.ExecuteAsync(
            new ExitSimulationModeStep(), context: null!, CancellationToken.None);

        outcome.Should().Be(StepOutcome.Passed);
        svc.IsSimulationModeActive.Should().BeFalse();
        vi.Enabled.Should().BeFalse();
    }
}
