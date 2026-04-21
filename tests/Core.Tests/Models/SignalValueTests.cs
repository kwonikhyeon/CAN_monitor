using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Core.Tests.Models;

public sealed class SignalValueTests
{
    [Fact]
    public void Equal_when_all_fields_match()
    {
        var ts = DateTimeOffset.UtcNow;
        var a = new SignalValue("Msg", "Sig", 123.0, 1.23, "A", ts);
        var b = new SignalValue("Msg", "Sig", 123.0, 1.23, "A", ts);
        a.Should().Be(b);
    }

    [Fact]
    public void Unit_may_be_null()
    {
        var v = new SignalValue("Msg", "Sig", 0, 0, null, DateTimeOffset.UtcNow);
        v.Unit.Should().BeNull();
    }
}
