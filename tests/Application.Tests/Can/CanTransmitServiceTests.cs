using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class CanTransmitServiceTests
{
    private static CanFrame Fr(uint id) =>
        new(id, false, new byte[] { 0 }, DateTimeOffset.UtcNow, CanDirection.Tx);

    [Fact]
    public async Task SendAsync_forwards_to_ICanBus_immediately()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        await using var sched = new TxScheduler(bus);
        var svc = new CanTransmitService(bus, sched);

        var sent = new List<uint>();
        using var _ = bus.Frames.Subscribe(f => { if (f.Direction == CanDirection.Tx) sent.Add(f.Id); });

        await svc.SendAsync(Fr(1));
        await svc.SendAsync(Fr(2));

        sent.Should().Equal(new uint[] { 1, 2 });
    }

    [Fact]
    public async Task Burst_delegates_to_scheduler()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        await using var sched = new TxScheduler(bus);
        var svc = new CanTransmitService(bus, sched);

        var sent = new List<uint>();
        using var _ = bus.Frames.Subscribe(f => { if (f.Direction == CanDirection.Tx) sent.Add(f.Id); });

        using var __ = svc.Burst(new[] { Fr(10), Fr(11), Fr(12) });
        await sched.DrainForTestsAsync();

        sent.Should().Equal(new uint[] { 10, 11, 12 });
    }
}
