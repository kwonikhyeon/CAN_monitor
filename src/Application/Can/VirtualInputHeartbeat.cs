using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class VirtualInputHeartbeat : IBusHeartbeatProvider, IDisposable
{
    private const uint VirtualInputExtendedId = 0x18FF5080u;
    private readonly IVirtualInputService _service;
    private readonly BehaviorSubject<bool> _enabled = new(false);
    private bool _disposed;

    public VirtualInputHeartbeat(IVirtualInputService service)
    {
        _service = service;
    }

    public string Name => "VirtualInput";
    public TimeSpan Period => TimeSpan.FromMilliseconds(50);

    public bool Enabled => _enabled.Value;
    public IObservable<bool> EnabledChanges => _enabled.AsObservable();

    public void SetEnabled(bool enabled)
    {
        if (_enabled.Value != enabled)
            _enabled.OnNext(enabled);
    }

    public CanFrame BuildFrame()
    {
        var s = _service.Current;
        var buf = new byte[8];

        byte byte0 = 0;
        byte0 |= (byte)((int)s.GearLever & 0x03);                // 비트 1..0
        byte0 |= (byte)(((int)s.RangeShift & 0x03) << 2);        // 비트 3..2
        if (s.TemperatureSwitch) byte0 |= 0b0001_0000;           // 비트 4
        buf[0] = byte0;

        byte byte1 = 0;
        if (s.PtoSwitch)     byte1 |= 0b0000_0001;
        if (s.FourWdSwitch)  byte1 |= 0b0000_0010;
        if (s.InchingSwitch) byte1 |= 0b0000_0100;
        if (s.ParkingSwitch) byte1 |= 0b0000_1000;
        buf[1] = byte1;

        buf[2] = (byte)Math.Clamp((int)Math.Round(s.ClutchPedalPercent), 0, 100);
        // buf[3] 은 예약 영역

        int s1 = Math.Clamp((int)Math.Round(s.SpeedSensor1Rpm), 0, ushort.MaxValue);
        buf[4] = (byte)((s1 >> 8) & 0xFF);
        buf[5] = (byte)(s1 & 0xFF);

        int s2 = Math.Clamp((int)Math.Round(s.SpeedSensor2Rpm), 0, ushort.MaxValue);
        buf[6] = (byte)((s2 >> 8) & 0xFF);
        buf[7] = (byte)(s2 & 0xFF);

        return new CanFrame(VirtualInputExtendedId, IsExtended: true, buf, DateTimeOffset.UtcNow, CanDirection.Tx);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _enabled.OnCompleted();
        }
        catch (ObjectDisposedException)
        {
            // Subject 가 이미 dispose 된 경우
        }
        _enabled.Dispose();
    }
}
