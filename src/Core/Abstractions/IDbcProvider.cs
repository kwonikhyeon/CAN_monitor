using CanMonitor.Core.Models;

namespace CanMonitor.Core.Abstractions;

public interface IDbcProvider
{
    DbcDatabase Current { get; }
    event EventHandler<DbcDatabase>? DatabaseReplaced;
    Task LoadAsync(string path, CancellationToken ct = default);
    Task SaveAsync(string path, CancellationToken ct = default);
}
