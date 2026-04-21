using CanMonitor.Core.Testing;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Core.Tests.Testing;

public sealed class TestStepIdentityTests
{
    [Fact]
    public void Discriminators_match_spec()
    {
        new WaitStep(TimeSpan.FromMilliseconds(1)).Type.Should().Be("Wait");
        new ObserveSignalStep("M", "S", 1, TimeSpan.FromMilliseconds(1), 0.1).Type.Should().Be("ObserveSignal");
        new ObserveBitStep("M", "S", true, TimeSpan.FromMilliseconds(1)).Type.Should().Be("ObserveBit");
        new SendCanFrameStep(0x100, false, new byte[] { 1 }).Type.Should().Be("SendCanFrame");
        new AssertFrameRateStep(0x100, 100, 5).Type.Should().Be("AssertFrameRate");
        new ManualConfirmStep("instruct").Type.Should().Be("ManualConfirm");
        new SetVirtualInputStep().Type.Should().Be("SetVirtualInput");
        new SetHeartbeatStep("EEC1", true).Type.Should().Be("SetHeartbeat");
        new EnterSimulationModeStep().Type.Should().Be("EnterSimulationMode");
        new ExitSimulationModeStep().Type.Should().Be("ExitSimulationMode");
    }
}
