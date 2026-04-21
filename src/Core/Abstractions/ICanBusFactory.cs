namespace CanMonitor.Core.Abstractions;

public interface ICanBusFactory
{
    IReadOnlyList<string> AvailableAdapters { get; }
    ICanBus Create(string adapterName);
}
