namespace CanMonitor.Core.Abstractions;

public sealed record CanBusOptions(
    int Bitrate = 250_000,
    string? ChannelId = null,
    bool ListenOnly = false);
