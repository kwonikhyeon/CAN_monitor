namespace CanMonitor.Core.Models;

public enum BusStatus
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Recovering = 3,
    Faulted = 4,
    Disconnecting = 5
}
