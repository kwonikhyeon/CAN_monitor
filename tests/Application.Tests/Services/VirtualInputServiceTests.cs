using CanMonitor.Application.Services;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Services;

public sealed class VirtualInputServiceTests
{
    [Fact]
    public void Starts_with_default_state_and_simulation_off()
    {
        using var sut = new VirtualInputService();
        sut.IsSimulationModeActive.Should().BeFalse();
        sut.Current.Should().Be(new VirtualInputState());
    }

    [Fact]
    public async Task EnterSimulationMode_flips_flag()
    {
        using var sut = new VirtualInputService();
        await sut.EnterSimulationModeAsync();
        sut.IsSimulationModeActive.Should().BeTrue();

        await sut.ExitSimulationModeAsync();
        sut.IsSimulationModeActive.Should().BeFalse();
    }

    [Fact]
    public void Update_replaces_snapshot_and_publishes()
    {
        using var sut = new VirtualInputService();
        var seen = new List<VirtualInputState>();
        using var _ = sut.Changes.Subscribe(seen.Add);

        var next = sut.Current with { GearLever = GearLever.Neutral, ClutchPedalPercent = 50.0 };
        sut.Update(next);

        sut.Current.Should().Be(next);
        seen.Should().HaveCountGreaterOrEqualTo(2); // BehaviorSubject 가 초기값 + 다음값 재방출
        seen.Last().Should().Be(next);
    }
}
