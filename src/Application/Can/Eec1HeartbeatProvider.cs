using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class Eec1HeartbeatProvider : IBusHeartbeatProvider, IDisposable
{
    private const uint Eec1ExtendedId = 0x18F00417u;
    private readonly BehaviorSubject<bool> _enabled = new(true);
    private int _lowCode;
    private int _highCode;

    public string Name => "EEC1";
    public TimeSpan Period => TimeSpan.FromMilliseconds(100);

    public bool Enabled => _enabled.Value;
    public IObservable<bool> EnabledChanges => _enabled.AsObservable();

    public void SetEnabled(bool enabled)
    {
        if (_enabled.Value != enabled)
            _enabled.OnNext(enabled);
    }

    public void SetLow(int code) => Interlocked.Exchange(ref _lowCode, Math.Clamp(code, 0, 15));
    public void SetHigh(int code) => Interlocked.Exchange(ref _highCode, Math.Clamp(code, 0, 15));

    public CanFrame BuildFrame()
    {
        var buf = new byte[8];
        var low = Volatile.Read(ref _lowCode);
        var high = Volatile.Read(ref _highCode);
        buf[0] = (byte)(((high & 0x0F) << 4) | (low & 0x0F));
        return new CanFrame(Eec1ExtendedId, IsExtended: true, buf, DateTimeOffset.UtcNow, CanDirection.Tx);
    }

    public void Dispose()
    {
        _enabled.OnCompleted();
        _enabled.Dispose();
    }
}
