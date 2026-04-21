using System.Reactive.Linq;
using CanMonitor.Application.Can;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class ManualBusStatusPublisherTests
{
    [Fact]
    public void Default_status_is_Disconnected()
    {
        var pub = new ManualBusStatusPublisher();
        pub.Current.Status.Should().Be(BusStatus.Disconnected);
    }

    [Fact]
    public void Publish_updates_Current_and_emits_on_Changes()
    {
        var pub = new ManualBusStatusPublisher();
        var received = new List<BusStatusChange>();
        using var _ = pub.Changes.Skip(1).Subscribe(received.Add);

        var change = new BusStatusChange(BusStatus.Connected, "ok", null, 0, DateTimeOffset.UtcNow);
        pub.Publish(change);

        pub.Current.Should().Be(change);
        received.Should().ContainSingle().Which.Should().Be(change);
    }
}
