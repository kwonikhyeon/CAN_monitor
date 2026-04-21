using System.Collections.Immutable;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Dbc;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Dbc.Tests;

public sealed class SignalDecoderTests
{
    private sealed class FakeDbcProvider(DbcDatabase db) : IDbcProvider
    {
        public DbcDatabase Current { get; private set; } = db;
        public event EventHandler<DbcDatabase>? DatabaseReplaced;
        public Task LoadAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(string path, CancellationToken ct = default) => Task.CompletedTask;
        public void Replace(DbcDatabase next) { Current = next; DatabaseReplaced?.Invoke(this, next); }
    }

    private static CanFrame FrameWith(params byte[] payload) =>
        new(Id: 0x100, IsExtended: false, Data: payload,
            Timestamp: DateTimeOffset.UnixEpoch, Direction: CanDirection.Rx);

    private static DbcMessage Msg(params DbcSignal[] signals) =>
        new(Id: 0x100, IsExtended: false, Name: "M", Dlc: 8,
            Signals: signals.ToImmutableArray(), CycleTime: null);

    // ---- Unknown-id handling ----

    [Fact]
    public void Decode_unknown_message_returns_empty()
    {
        var db   = new DbcDatabase(new[] { Msg() });
        var sut  = new SignalDecoder(new FakeDbcProvider(db));

        var result = sut.Decode(new CanFrame(0xFFF, false, Array.Empty<byte>(), default, CanDirection.Rx));
        result.Should().BeEmpty();
    }

    // ---- Intel (little-endian) ----

    [Fact]
    public void Intel_8bit_signal_at_byte0()
    {
        var sig = new DbcSignal("A", StartBit: 0, Length: 8, LittleEndian: true, IsSigned: false,
            Factor: 1, Offset: 0, Minimum: 0, Maximum: 255, Unit: null, ValueTable: null);
        var sut = new SignalDecoder(new FakeDbcProvider(new DbcDatabase(new[] { Msg(sig) })));

        var r = sut.Decode(FrameWith(0xAB, 0, 0, 0, 0, 0, 0, 0));
        r.Should().ContainSingle();
        r[0].RawValue.Should().Be(0xAB);
    }

    [Fact]
    public void Intel_16bit_signal_crossing_byte_boundary()
    {
        // Start bit 0, length 16, little-endian => bytes[0] low, bytes[1] high
        var sig = new DbcSignal("A", 0, 16, true, false, 1, 0, 0, 65535, null, null);
        var sut = new SignalDecoder(new FakeDbcProvider(new DbcDatabase(new[] { Msg(sig) })));

        var r = sut.Decode(FrameWith(0x34, 0x12, 0, 0, 0, 0, 0, 0));
        r[0].RawValue.Should().Be(0x1234);
    }

    // ---- Motorola (big-endian, @0+) ----

    [Fact]
    public void Motorola_8bit_signal_at_msb_of_byte0()
    {
        var sig = new DbcSignal("A", StartBit: 7, Length: 8, LittleEndian: false, IsSigned: false,
            Factor: 1, Offset: 0, Minimum: 0, Maximum: 255, Unit: null, ValueTable: null);
        var sut = new SignalDecoder(new FakeDbcProvider(new DbcDatabase(new[] { Msg(sig) })));

        var r = sut.Decode(FrameWith(0xAB, 0, 0, 0, 0, 0, 0, 0));
        r[0].RawValue.Should().Be(0xAB);
    }

    [Fact]
    public void Motorola_16bit_signal_crossing_byte_boundary()
    {
        // High byte in byte[0] MSB-aligned, low byte in byte[1].
        // DBC start bit for Motorola @0+ is the MSB position; 7=MSB of byte 0.
        var sig = new DbcSignal("A", StartBit: 7, Length: 16, LittleEndian: false, IsSigned: false,
            Factor: 1, Offset: 0, Minimum: 0, Maximum: 65535, Unit: null, ValueTable: null);
        var sut = new SignalDecoder(new FakeDbcProvider(new DbcDatabase(new[] { Msg(sig) })));

        var r = sut.Decode(FrameWith(0x12, 0x34, 0, 0, 0, 0, 0, 0));
        r[0].RawValue.Should().Be(0x1234);
    }

    // ---- Signed ----

    [Fact]
    public void Signed_negative_extracted_with_signextension()
    {
        var sig = new DbcSignal("A", 0, 8, true, IsSigned: true, 1, 0, -128, 127, null, null);
        var sut = new SignalDecoder(new FakeDbcProvider(new DbcDatabase(new[] { Msg(sig) })));

        var r = sut.Decode(FrameWith(0xFF, 0, 0, 0, 0, 0, 0, 0));
        r[0].RawValue.Should().Be(-1);
    }

    // ---- Factor/offset ----

    [Fact]
    public void Applies_factor_and_offset_to_physical_value()
    {
        var sig = new DbcSignal("Temp", 0, 8, true, false,
            Factor: 0.5, Offset: -40.0, Minimum: -40, Maximum: 87.5, Unit: "degC", ValueTable: null);
        var sut = new SignalDecoder(new FakeDbcProvider(new DbcDatabase(new[] { Msg(sig) })));

        var r = sut.Decode(FrameWith(100, 0, 0, 0, 0, 0, 0, 0));
        r[0].RawValue.Should().Be(100);
        r[0].PhysicalValue.Should().Be(10.0);                // 100 * 0.5 + -40
        r[0].Unit.Should().Be("degC");
    }

    // ---- Snapshot capture semantics ----

    [Fact]
    public void Captures_snapshot_at_decode_start()
    {
        var sigA = new DbcSignal("A", 0, 8, true, false, 1, 0, 0, 255, null, null);
        var sigB = new DbcSignal("B", 0, 8, true, false, 1, 0, 0, 255, null, null);
        var msgA = new DbcMessage(0x100, false, "M", 8, ImmutableArray.Create(sigA), null);
        var msgB = new DbcMessage(0x100, false, "M", 8, ImmutableArray.Create(sigB), null);

        var provider = new FakeDbcProvider(new DbcDatabase(new[] { msgA }));
        var sut      = new SignalDecoder(provider);

        var firstBatch  = sut.Decode(FrameWith(5, 0, 0, 0, 0, 0, 0, 0));
        provider.Replace(new DbcDatabase(new[] { msgB }));
        var secondBatch = sut.Decode(FrameWith(5, 0, 0, 0, 0, 0, 0, 0));

        firstBatch.Single().SignalName.Should().Be("A");
        secondBatch.Single().SignalName.Should().Be("B");
    }
}
