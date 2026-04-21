using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Core.Tests.Models;

public sealed class AlarmStateTests
{
    [Fact]
    public void Severity_enum_has_four_levels()
    {
        Enum.GetValues<AlarmSeverity>().Should().BeEquivalentTo(new[]
        {
            AlarmSeverity.Info, AlarmSeverity.Warning,
            AlarmSeverity.Error, AlarmSeverity.LimpHome
        });
    }

    [Fact]
    public void AlarmState_equality_by_value()
    {
        var since = DateTimeOffset.UtcNow;
        var a = new AlarmState("FFLT-FPSV", AlarmSeverity.Error, "Fwd PSV fault", true, since);
        var b = new AlarmState("FFLT-FPSV", AlarmSeverity.Error, "Fwd PSV fault", true, since);
        a.Should().Be(b);
    }
}
