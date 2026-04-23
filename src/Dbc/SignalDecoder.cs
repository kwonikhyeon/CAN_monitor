using System.Reactive.Linq;
using System.Reactive.Subjects;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Dbc;

public sealed class SignalDecoder : ISignalDecoder, IDisposable
{
    private readonly IDbcProvider _dbc;
    private readonly Subject<CanFrame> _unknown = new();

    public SignalDecoder(IDbcProvider dbc) => _dbc = dbc;

    public IObservable<CanFrame> UnknownFrames => _unknown.AsObservable();

    public IReadOnlyList<SignalValue> Decode(CanFrame frame)
    {
        var db = _dbc.Current;                                // 시작 시점의 스냅샷을 캡처
        if (!db.MessagesById.TryGetValue(frame.Id, out var msg))
        {
            _unknown.OnNext(frame);
            return Array.Empty<SignalValue>();
        }

        var payload = frame.Data.Span;
        var results = new SignalValue[msg.Signals.Length];

        for (int i = 0; i < msg.Signals.Length; i++)
        {
            var sig = msg.Signals[i];
            long raw = sig.LittleEndian
                ? ExtractIntel(payload, sig.StartBit, sig.Length, sig.IsSigned)
                : ExtractMotorola(payload, sig.StartBit, sig.Length, sig.IsSigned);

            double phys = raw * sig.Factor + sig.Offset;
            results[i] = new SignalValue(msg.Name, sig.Name, raw, phys, sig.Unit, frame.Timestamp);
        }
        return results;
    }

    public void Dispose()
    {
        _unknown.OnCompleted();
        _unknown.Dispose();
    }

    private static long ExtractIntel(ReadOnlySpan<byte> data, int startBit, int length, bool isSigned)
    {
        ulong raw = 0;
        for (int i = 0; i < length; i++)
        {
            int absBit    = startBit + i;
            int byteIndex = absBit >> 3;
            int bitInByte = absBit & 7;
            if (byteIndex >= data.Length) break;
            if (((data[byteIndex] >> bitInByte) & 1) != 0)
                raw |= 1UL << i;
        }
        return SignExtend(raw, length, isSigned);
    }

    private static long ExtractMotorola(ReadOnlySpan<byte> data, int startBit, int length, bool isSigned)
    {
        ulong raw   = 0;
        int byteIdx = startBit >> 3;
        int bitIdx  = startBit & 7;

        for (int i = 0; i < length; i++)
        {
            if (byteIdx >= data.Length) break;
            raw <<= 1;
            uint bit = (uint)((data[byteIdx] >> bitIdx) & 1);
            raw |= bit;

            if (bitIdx == 0)
            {
                bitIdx = 7;
                byteIdx++;
            }
            else
            {
                bitIdx--;
            }
        }
        return SignExtend(raw, length, isSigned);
    }

    private static long SignExtend(ulong raw, int length, bool isSigned)
    {
        if (!isSigned || length == 64) return unchecked((long)raw);
        ulong signBit = 1UL << (length - 1);
        if ((raw & signBit) == 0) return (long)raw;
        ulong mask = ~((1UL << length) - 1);
        return unchecked((long)(raw | mask));
    }
}
