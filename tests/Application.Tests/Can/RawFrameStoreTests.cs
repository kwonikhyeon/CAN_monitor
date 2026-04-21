using CanMonitor.Application.Can;
using CanMonitor.Core.Models;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Application.Tests.Can;

public sealed class RawFrameStoreTests
{
    private static CanFrame Fr(uint id) =>
        new(id, false, new byte[] { 0 }, DateTimeOffset.UtcNow, CanDirection.Rx);

    [Fact]
    public void Preserves_frames_under_capacity()
    {
        var store = new RawFrameStore(capacity: 4);
        store.Record(Fr(1));
        store.Record(Fr(2));
        store.Record(Fr(3));

        store.Snapshot().Select(f => f.Id).Should().Equal(new uint[] { 1, 2, 3 });
        store.DroppedCount.Should().Be(0);
    }

    [Fact]
    public void Drops_oldest_when_over_capacity()
    {
        var store = new RawFrameStore(capacity: 3);
        store.Record(Fr(1));
        store.Record(Fr(2));
        store.Record(Fr(3));
        store.Record(Fr(4));
        store.Record(Fr(5));

        store.Snapshot().Select(f => f.Id).Should().Equal(new uint[] { 3, 4, 5 });
        store.DroppedCount.Should().Be(2);
    }

    [Fact]
#pragma warning disable xUnit1031
    public void Concurrent_writes_do_not_throw_and_respect_capacity()
    {
        var store = new RawFrameStore(capacity: 1_000);
        var tasks = Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (int i = 0; i < 2_000; i++)
                store.Record(Fr((uint)(worker * 10_000 + i)));
        })).ToArray();
        Task.WaitAll(tasks);

        store.Snapshot().Count.Should().BeLessThanOrEqualTo(1_000);
    }
#pragma warning restore xUnit1031
}
