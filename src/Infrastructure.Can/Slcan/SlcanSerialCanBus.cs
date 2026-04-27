using System.Globalization;
using System.IO.Ports;
using System.Text;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Infrastructure.Can.Slcan;

public sealed class SlcanSerialCanBus : CanBusBase
{
    private readonly object _serialGate = new();
    private SerialPort? _serial;
    private CancellationTokenSource? _rxCts;
    private Task? _rxTask;

    public override string Name => "SLCAN Serial";

    protected override Task OpenDriverAsync(CanBusOptions options, CancellationToken ct)
    {
        var portName = NormalizePortName(options.ChannelId);

        var bitrateCode = GetBitrateCode(options.Bitrate);
        var serial = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
        {
            NewLine = "\r",
            ReadTimeout = 100,
            WriteTimeout = 1000,
            DtrEnable = true,
            RtsEnable = true
        };

        serial.Open();
        _serial = serial;

        WriteCommand("C");
        WriteCommand($"S{bitrateCode}");
        WriteCommand("O");

        _rxCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _rxTask = Task.Run(() => ReceiveLoopAsync(_rxCts.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    protected override async Task StopDriverAsync()
    {
        _rxCts?.Cancel();
        if (_rxTask is not null)
        {
            try { await _rxTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        lock (_serialGate)
        {
            if (_serial is not null)
            {
                if (_serial.IsOpen)
                {
                    TryWriteCommand("C");
                    _serial.Close();
                }
                _serial.Dispose();
                _serial = null;
            }
        }

        _rxCts?.Dispose();
        _rxCts = null;
        _rxTask = null;
    }

    protected override Task SendDriverAsync(CanFrame frame, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (frame.Data.Length > 8)
            throw new ArgumentOutOfRangeException(nameof(frame), "Classic CAN frames can contain at most 8 data bytes.");

        var command = FormatFrame(frame);
        WriteCommand(command);
        return Task.CompletedTask;
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new StringBuilder();

        while (!ct.IsCancellationRequested)
        {
            int next;
            try
            {
                var serial = _serial;
                if (serial is null || !serial.IsOpen)
                    return;

                next = serial.ReadChar();
            }
            catch (TimeoutException)
            {
                await Task.Yield();
                continue;
            }
            catch (InvalidOperationException)
            {
                return;
            }
            catch (IOException)
            {
                return;
            }

            if (next == '\r')
            {
                ProcessLine(buffer.ToString());
                buffer.Clear();
                continue;
            }

            if (next != '\n')
                buffer.Append((char)next);
        }
    }

    private void ProcessLine(string line)
    {
        if (TryParseFrame(line, out var frame))
            PublishRx(frame);
    }

    private void WriteCommand(string command)
    {
        lock (_serialGate)
        {
            if (_serial is null || !_serial.IsOpen)
                throw new InvalidOperationException("SLCAN serial port is not open.");

            _serial.Write(command + "\r");
        }
    }

    private void TryWriteCommand(string command)
    {
        try { WriteCommand(command); }
        catch { }
    }

    private static string NormalizePortName(string? channelId)
    {
        var value = channelId?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SLCAN channel must be a COM port number or COM port name, for example 7 or COM7.", nameof(channelId));

        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var portNumber)
            ? $"COM{portNumber}"
            : value.ToUpperInvariant();
    }

    private static string FormatFrame(CanFrame frame)
    {
        var data = frame.Data.ToArray();
        var builder = new StringBuilder();
        builder.Append(frame.IsExtended ? 'T' : 't');
        builder.Append(frame.Id.ToString(frame.IsExtended ? "X8" : "X3", CultureInfo.InvariantCulture));
        builder.Append(data.Length.ToString("X1", CultureInfo.InvariantCulture));

        foreach (var value in data)
            builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));

        return builder.ToString();
    }

    private static bool TryParseFrame(string line, out CanFrame frame)
    {
        frame = default;
        if (line.Length < 5)
            return false;

        var isExtended = line[0] == 'T';
        if (line[0] != 't' && !isExtended)
            return false;

        var idLength = isExtended ? 8 : 3;
        if (line.Length < 1 + idLength + 1)
            return false;

        if (!uint.TryParse(line.Substring(1, idLength), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id))
            return false;

        if (!int.TryParse(line.Substring(1 + idLength, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var dlc))
            return false;

        if (dlc < 0 || dlc > 8 || line.Length < 1 + idLength + 1 + dlc * 2)
            return false;

        var data = new byte[dlc];
        var dataStart = 1 + idLength + 1;
        for (var i = 0; i < dlc; i++)
        {
            if (!byte.TryParse(line.Substring(dataStart + i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out data[i]))
                return false;
        }

        frame = new CanFrame(id, isExtended, data, DateTimeOffset.UtcNow, CanDirection.Rx);
        return true;
    }

    private static int GetBitrateCode(int bitrate) => bitrate switch
    {
        10_000 => 0,
        20_000 => 1,
        50_000 => 2,
        100_000 => 3,
        125_000 => 4,
        250_000 => 5,
        500_000 => 6,
        800_000 => 7,
        1_000_000 => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(bitrate), bitrate, "SLCAN supports 10000, 20000, 50000, 100000, 125000, 250000, 500000, 800000, or 1000000 bps.")
    };
}
