using System.Collections.Immutable;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;
using DbcParserLib;
using DbcParserLib.Model;
using LibDbc     = DbcParserLib.Dbc;
using LibMessage = DbcParserLib.Model.Message;
using LibSignal  = DbcParserLib.Model.Signal;

namespace CanMonitor.Dbc;

public sealed class DbcParserLibProvider : IDbcProvider
{
    private DbcDatabase _current = DbcDatabase.Empty;

    public DbcDatabase Current => _current;
    public event EventHandler<DbcDatabase>? DatabaseReplaced;

    public Task LoadAsync(string path, CancellationToken ct = default)
    {
        var parsed = Parser.ParseFromPath(path);
        var next   = ConvertDbc(parsed);
        _current   = next;
        DatabaseReplaced?.Invoke(this, next);
        return Task.CompletedTask;
    }

    public Task SaveAsync(string path, CancellationToken ct = default)
    {
        // Save is part of DBC editor (feature H) — deferred.
        throw new NotSupportedException("DBC save is part of feature H (Phase 3+).");
    }

    private static DbcDatabase ConvertDbc(LibDbc parsed)
    {
        var messages = parsed.Messages.Select(ConvertMessage).ToArray();
        return new DbcDatabase(messages);
    }

    private static DbcMessage ConvertMessage(LibMessage m)
    {
        var isExtended = (m.ID & 0x80000000u) != 0 || m.ID > 0x7FF;
        var cleanId    = m.ID & 0x1FFFFFFFu;

        var signals = m.Signals.Select(ConvertSignal).ToImmutableArray();

        TimeSpan? cycle = null;
        // Try the CycleTime method if it exists
        if (m.CycleTime(out var cycleMs) && cycleMs > 0)
            cycle = TimeSpan.FromMilliseconds(cycleMs);

        return new DbcMessage(
            Id: cleanId,
            IsExtended: isExtended,
            Name: m.Name,
            Dlc: (int)m.DLC,
            Signals: signals,
            CycleTime: cycle);
    }

    private static DbcSignal ConvertSignal(LibSignal s)
    {
        ImmutableDictionary<long, string>? table = null;
        if (s.ValueTableMap is { Count: > 0 })
        {
            var b = ImmutableDictionary.CreateBuilder<long, string>();
            foreach (var kv in s.ValueTableMap)
                b[kv.Key] = kv.Value;
            table = b.ToImmutable();
        }

        return new DbcSignal(
            Name:         s.Name,
            StartBit:     (int)s.StartBit,
            Length:       (int)s.Length,
            LittleEndian: s.ByteOrder == 1,          // DbcParserLib: 1 = Intel, 0 = Motorola
            IsSigned:     s.ValueType == DbcValueType.Signed,
            Factor:       s.Factor,
            Offset:       s.Offset,
            Minimum:      s.Minimum,
            Maximum:      s.Maximum,
            Unit:         string.IsNullOrEmpty(s.Unit) ? null : s.Unit,
            ValueTable:   table);
    }
}
