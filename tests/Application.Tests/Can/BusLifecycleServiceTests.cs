using System.Reactive.Linq;
using CanMonitor.Application.Can;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class BusLifecycleServiceTests
{
    [Fact]
    public async Task Starts_schedule_for_providers_that_begin_enabled()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        await using var scheduler = new TxScheduler(bus);
        using var eec1 = new Eec1HeartbeatProvider();

        var seen = new List<CanFrame>();
        using var _ = bus.Frames.Where(f => f.Id == 0x18F00417u).Subscribe(seen.Add);

        await using var sut = new BusLifecycleService(new IBusHeartbeatProvider[] { eec1 }, scheduler);
        sut.Start();

        await Task.Delay(350);
        await scheduler.DrainForTestsAsync();

        seen.Should().HaveCountGreaterOrEqualTo(2); // ~3 frames in 300ms at 100ms period
    }

    [Fact]
    public async Task Disabling_provider_cancels_schedule()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        await using var scheduler = new TxScheduler(bus);
        using var eec1 = new Eec1HeartbeatProvider();

        await using var sut = new BusLifecycleService(new IBusHeartbeatProvider[] { eec1 }, scheduler);
        sut.Start();

        await Task.Delay(150);
        eec1.SetEnabled(false);
        await scheduler.DrainForTestsAsync();

        var snapshot = new List<CanFrame>();
        using var _ = bus.Frames.Where(f => f.Id == 0x18F00417u).Subscribe(snapshot.Add);

        await Task.Delay(250);
        snapshot.Should().BeEmpty();
    }

    [Fact]
    public async Task Enabling_provider_after_start_begins_schedule()
    {
        await using var bus = new VirtualCanBus();
        await bus.OpenAsync(new CanBusOptions());
        await using var scheduler = new TxScheduler(bus);
        using var eec1 = new Eec1HeartbeatProvider();
        eec1.SetEnabled(false); // start disabled

        await using var sut = new BusLifecycleService(new IBusHeartbeatProvider[] { eec1 }, scheduler);
        sut.Start();

        await Task.Delay(50);
        var before = new List<CanFrame>();
        using (bus.Frames.Where(f => f.Id == 0x18F00417u).Subscribe(before.Add))
            await Task.Delay(150);
        before.Should().BeEmpty();

        eec1.SetEnabled(true);

        var after = new List<CanFrame>();
        using (bus.Frames.Where(f => f.Id == 0x18F00417u).Subscribe(after.Add))
        {
            await Task.Delay(250);
            await scheduler.DrainForTestsAsync();
        }
        after.Should().NotBeEmpty();
    }
}
