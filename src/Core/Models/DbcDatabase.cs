using System.Collections.Immutable;

namespace CanMonitor.Core.Models;

public sealed class DbcDatabase
{
    public static DbcDatabase Empty { get; } = new DbcDatabase(Array.Empty<DbcMessage>());

    public ImmutableArray<DbcMessage> Messages { get; }
    public ImmutableDictionary<uint, DbcMessage> MessagesById { get; }

    public DbcDatabase(IEnumerable<DbcMessage> messages)
    {
        var list = messages.ToImmutableArray();
        Messages = list;

        var byId = ImmutableDictionary.CreateBuilder<uint, DbcMessage>();
        foreach (var m in list)
        {
            if (byId.ContainsKey(m.Id))
                throw new ArgumentException(
                    $"duplicate message id 0x{m.Id:X} ({m.Name})", nameof(messages));
            byId.Add(m.Id, m);
        }
        MessagesById = byId.ToImmutable();
    }
}
