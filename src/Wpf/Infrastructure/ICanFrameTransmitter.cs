using CanMonitor.Core.Models;

namespace CanMonitor.Wpf.Infrastructure;

public interface ICanFrameTransmitter
{
    ConnectionState State { get; }
    IObservable<ConnectionState> StateChanges { get; }
    IObservable<CanFrame> Frames { get; }
    Task SendFrameAsync(CanFrame frame, CancellationToken ct = default);
}
