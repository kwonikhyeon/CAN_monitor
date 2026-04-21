using System.Collections.Immutable;

namespace CanMonitor.Core.Models;

public sealed record DbcMessage(
    uint Id,
    bool IsExtended,
    string Name,
    int Dlc,
    ImmutableArray<DbcSignal> Signals,
    TimeSpan? CycleTime);
