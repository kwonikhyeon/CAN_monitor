using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Core.Models;

namespace CanMonitor.Application.Can;

public sealed class ManualBusStatusPublisher : IDisposable
{
    private readonly BehaviorSubject<BusStatusChange> _subject;

    public ManualBusStatusPublisher()
    {
        _subject = new BehaviorSubject<BusStatusChange>(
            new BusStatusChange(BusStatus.Disconnected, null, null, 0, DateTimeOffset.UtcNow));
    }

    public BusStatusChange Current => _subject.Value;
    public IObservable<BusStatusChange> Changes => _subject.AsObservable();

    public void Publish(BusStatusChange change) => _subject.OnNext(change);

    public void Dispose() => _subject.Dispose();
}
