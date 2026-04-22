using CanMonitor.Wpf.Infrastructure;

namespace CanMonitor.Wpf.Shell;

public interface ISessionState
{
    IObservable<ConnectionState> StateChanges { get; }
    IObservable<DbcFileOption?> DbcChanges { get; }
}
