using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Core.Tests.Models;

public sealed class BusStatusChangeTests
{
    [Fact]
    public void BusStatus_has_six_states()
    {
        Enum.GetValues<BusStatus>().Should().BeEquivalentTo(new[]
        {
            BusStatus.Disconnected, BusStatus.Connecting, BusStatus.Connected,
            BusStatus.Recovering,  BusStatus.Faulted,    BusStatus.Disconnecting
        });
    }

    [Fact]
    public void BusStatusChange_captures_retry_and_error()
    {
        var ex = new InvalidOperationException("driver lost");
        var change = new BusStatusChange(
            BusStatus.Recovering, "retrying", ex, RetryAttempt: 2, At: DateTimeOffset.UtcNow);

        change.Status.Should().Be(BusStatus.Recovering);
        change.Error.Should().Be(ex);
        change.RetryAttempt.Should().Be(2);
    }
}
