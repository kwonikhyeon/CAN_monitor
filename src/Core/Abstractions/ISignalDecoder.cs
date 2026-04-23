using CanMonitor.Core.Models;

namespace CanMonitor.Core.Abstractions;

public interface ISignalDecoder
{
    IReadOnlyList<SignalValue> Decode(CanFrame frame);
    IObservable<CanFrame> UnknownFrames { get; }
}
