using System.Collections.Immutable;

namespace CanMonitor.Core.Models;

public sealed record DbcSignal(
    string Name,
    int StartBit,
    int Length,
    bool LittleEndian,
    bool IsSigned,
    double Factor,
    double Offset,
    double Minimum,
    double Maximum,
    string? Unit,
    ImmutableDictionary<long, string>? ValueTable);
