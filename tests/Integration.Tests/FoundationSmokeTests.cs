using System.Reactive.Linq;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using CanMonitor.Dbc;
using CanMonitor.Infrastructure.Can;
using CanMonitor.Infrastructure.Can.Virtual;
using FluentAssertions;
using Xunit;

namespace CanMonitor.Integration.Tests;

public sealed class FoundationSmokeTests
{
    private static string Confirmed(string name) =>
        Path.Combine(AppContext.BaseDirectory, "confirmed", name);

    [Fact(Timeout = 5000)]
    public async Task End_to_end_injected_frame_decodes_to_signal_values()
    {
        // 준비
        var dbc = new DbcParserLibProvider();
        await dbc.LoadAsync(Confirmed("120HP_NoPto.dbc"));
        var decoder = new SignalDecoder(dbc);

        var factory = new CanBusFactory();
        await using var bus = (VirtualCanBus)factory.Create("Virtual");
        await bus.OpenAsync(new CanBusOptions());

        var decoded = new List<SignalValue>();
        using var subscription = bus.Frames
            .Where(f => f.Direction == CanDirection.Rx)
            .Subscribe(frame =>
            {
                foreach (var v in decoder.Decode(frame))
                    decoded.Add(v);
            });

        // 실행 — Gear_Lever_N_Status=1 인 0x0C000E00 프레임을 주입.
        // Gear_Lever_N_Status 는 Motorola @0+, start bit 7(byte 0 의 MSB),
        // length 1. byte 0 의 MSB 를 세트 → 0x80.
        var payload = new byte[] { 0x80, 0, 0, 0, 0, 0, 0, 0 };
        bus.Inject(new CanFrame(0x0C000E00, IsExtended: true, Data: payload,
            Timestamp: DateTimeOffset.UtcNow, Direction: CanDirection.Rx));

        // 동기화 Subject 가 배달할 시간을 잠시 부여.
        await Task.Delay(50);

        // 검증
        decoded.Should().NotBeEmpty();
        var neutralBit = decoded.Single(s => s.SignalName == "Gear_Lever_N_Status");
        neutralBit.RawValue.Should().Be(1);
        neutralBit.PhysicalValue.Should().Be(1);
        neutralBit.MessageName.Should().Be("Status_0x0C000E00");
    }
}
