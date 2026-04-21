using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Microsoft.Reactive.Testing;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class TxSchedulerTests
{
    private static CanFrame Fr(uint id) =>
        new(id, false, new byte[] { 0 }, DateTimeOffset.UtcNow, CanDirection.Tx);

    [Fact]
    public async Task Scheduled_factory_fires_at_configured_period()
    {
        var scheduler = new TestScheduler();
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());

        var sent = new List<uint>();
        using var _ = bus.Frames.Subscribe(f => { if (f.Direction == CanDirection.Tx) sent.Add(f.Id); });

        await using var tx = new TxScheduler(bus, scheduler);
        uint counter = 0;
        using var sub = tx.Schedule("T", () => Fr(++counter), TimeSpan.FromMilliseconds(100));

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(350).Ticks);
        await tx.DrainForTestsAsync();

        sent.Should().Equal(new uint[] { 1, 2, 3 });
    }

    [Fact]
    public async Task Dispose_of_Schedule_handle_stops_further_sends()
    {
        var scheduler = new TestScheduler();
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());

        var sent = new List<uint>();
        using var _ = bus.Frames.Subscribe(f => { if (f.Direction == CanDirection.Tx) sent.Add(f.Id); });

        await using var tx = new TxScheduler(bus, scheduler);
        uint counter = 0;
        var sub = tx.Schedule("T", () => Fr(++counter), TimeSpan.FromMilliseconds(100));

        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(150).Ticks);
        sub.Dispose();
        scheduler.AdvanceBy(TimeSpan.FromMilliseconds(500).Ticks);
        await tx.DrainForTestsAsync();

        sent.Should().Equal(new uint[] { 1 });
    }

    [Fact]
    public async Task SendBurst_flushes_frames_in_order()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());

        var sent = new List<uint>();
        using var _ = bus.Frames.Subscribe(f => { if (f.Direction == CanDirection.Tx) sent.Add(f.Id); });

        await using var tx = new TxScheduler(bus);
        using var __ = tx.SendBurst(new[] { Fr(10), Fr(11), Fr(12) });

        await tx.DrainForTestsAsync();

        sent.Should().Equal(new uint[] { 10, 11, 12 });
    }
}
