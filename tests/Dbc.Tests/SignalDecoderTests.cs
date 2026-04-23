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

    // ---- 알 수 없는 ID 처리 ----

    [Fact]
    public void Decode_unknown_message_returns_empty()
    {
        var db   = new DbcDatabase(new[] { Msg() });
        var sut  = new SignalDecoder(new FakeDbcProvider(db));

        var result = sut.Decode(new CanFrame(0xFFF, false, Array.Empty<byte>(), default, CanDirection.Rx));
        result.Should().BeEmpty();
    }

    // ---- Intel (리틀 엔디언) ----

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
        // start bit 0, length 16, 리틀 엔디언 → bytes[0] 저위, bytes[1] 고위
        var sig = new DbcSignal("A", 0, 16, true, false, 1, 0, 0, 65535, null, null);
        var sut = new SignalDecoder(new FakeDbcProvider(new DbcDatabase(new[] { Msg(sig) })));

        var r = sut.Decode(FrameWith(0x34, 0x12, 0, 0, 0, 0, 0, 0));
        r[0].RawValue.Should().Be(0x1234);
    }

    // ---- Motorola (빅 엔디언, @0+) ----

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
        // 고위 바이트는 byte[0] 의 MSB 정렬, 저위 바이트는 byte[1].
        // DBC 의 Motorola @0+ start bit 는 MSB 위치 — 7 = byte 0 의 MSB.
        var sig = new DbcSignal("A", StartBit: 7, Length: 16, LittleEndian: false, IsSigned: false,
            Factor: 1, Offset: 0, Minimum: 0, Maximum: 65535, Unit: null, ValueTable: null);
        var sut = new SignalDecoder(new FakeDbcProvider(new DbcDatabase(new[] { Msg(sig) })));

        var r = sut.Decode(FrameWith(0x12, 0x34, 0, 0, 0, 0, 0, 0));
        r[0].RawValue.Should().Be(0x1234);
    }

    // ---- 부호 있음 ----

    [Fact]
    public void Signed_negative_extracted_with_signextension()
    {
        var sig = new DbcSignal("A", 0, 8, true, IsSigned: true, 1, 0, -128, 127, null, null);
        var sut = new SignalDecoder(new FakeDbcProvider(new DbcDatabase(new[] { Msg(sig) })));

        var r = sut.Decode(FrameWith(0xFF, 0, 0, 0, 0, 0, 0, 0));
        r[0].RawValue.Should().Be(-1);
    }

    // ---- Factor/Offset ----

    [Fact]
    public void Applies_factor_and_offset_to_physical_value()
    {
        var sig = new DbcSignal("Temp", 0, 8, true, false,
            Factor: 0.5, Offset: -40.0, Minimum: -40, Maximum: 87.5, Unit: "degC", ValueTable: null);
        var sut = new SignalDecoder(new FakeDbcProvider(new DbcDatabase(new[] { Msg(sig) })));

        var r = sut.Decode(FrameWith(100, 0, 0, 0, 0, 0, 0, 0));
        r[0].RawValue.Should().Be(100);
        r[0].PhysicalValue.Should().Be(10.0);                // 100 * 0.5 + (-40)
        r[0].Unit.Should().Be("degC");
    }

    // ---- 스냅샷 캡처 시맨틱 ----

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

    [Fact]
    public void Unknown_id_publishes_to_UnknownFrames_and_returns_empty()
    {
        var provider = new FakeDbcProvider(DbcDatabase.Empty);
        var sut = new SignalDecoder(provider);

        var seen = new List<CanFrame>();
        using var sub = sut.UnknownFrames.Subscribe(seen.Add);

        var frame = new CanFrame(0xDEAD, true, new byte[] { 0 }, DateTimeOffset.UtcNow, CanDirection.Rx);
        var result = sut.Decode(frame);

        result.Should().BeEmpty();
        seen.Should().ContainSingle().Which.Id.Should().Be(0xDEADu);
    }
}
