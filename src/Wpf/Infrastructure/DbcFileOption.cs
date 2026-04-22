namespace CanMonitor.Wpf.Infrastructure;

public enum DbcSource
{
    Confirmed,
    Experimental,
    External
}

public sealed record DbcFileOption(string Path, string DisplayName, DbcSource Source);
