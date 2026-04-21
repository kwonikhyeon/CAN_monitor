namespace CanMonitor.Core.Models;

/// <summary>
/// Immutable CAN frame. <c>Data</c> MUST point to an independently owned buffer —
/// never wrap a driver's reusable/pooled native buffer. Frame producers (ICanBus implementations)
/// are responsible for copying payload at ingress. See spec §5/§8.
/// </summary>
public readonly record struct CanFrame(
    uint Id,
    bool IsExtended,
    ReadOnlyMemory<byte> Data,
    DateTimeOffset Timestamp,
    CanDirection Direction);
