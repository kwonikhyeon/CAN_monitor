using CanMonitor.Core.Models;

namespace CanMonitor.Core.Abstractions;

public interface ICanBus : IAsyncDisposable
{
    string Name { get; }
    bool IsOpen { get; }
    IObservable<CanFrame> Frames { get; }

    Task OpenAsync(CanBusOptions options, CancellationToken ct = default);
    Task CloseAsync();
    Task SendAsync(CanFrame frame, CancellationToken ct = default);
}
