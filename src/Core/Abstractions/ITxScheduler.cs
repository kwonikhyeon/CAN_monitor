using CanMonitor.Core.Models;

namespace CanMonitor.Core.Abstractions;

public interface ITxScheduler
{
    IDisposable Schedule(string name, Func<CanFrame> factory, TimeSpan period);
    IDisposable SendBurst(IReadOnlyList<CanFrame> frames, TimeSpan? interval = null);
}
