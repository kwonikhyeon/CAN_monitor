using System.Runtime.InteropServices;
using CanMonitor.Core.Abstractions;
using CanMonitor.Core.Models;

namespace CanMonitor.Infrastructure.Can.CandleLight;

public sealed class CandleLightCanBus : CanBusBase
{
    private const uint CanExtendedFrameFlag = 0x80000000;
    private const uint CanRtrFrameFlag = 0x40000000;
    private const uint CanErrorFrameFlag = 0x20000000;
    private const uint CanIdMask = 0x1FFFFFFF;
    private const byte DefaultChannelIndex = 0;

    private readonly object _driverGate = new();
    private IntPtr _deviceHandle;
    private byte _channelIndex = DefaultChannelIndex;
    private CancellationTokenSource? _rxCts;
    private Task? _rxTask;

    public override string Name => "CandleLight USB";

    protected override Task OpenDriverAsync(CanBusOptions options, CancellationToken ct)
    {
        try
        {
            var deviceIndex = ParseDeviceIndex(options.ChannelId);
            _channelIndex = DefaultChannelIndex;

            _deviceHandle = OpenDevice(deviceIndex);
            Ensure(CandleNative.candle_channel_count(_deviceHandle, out var channelCount), "read channel count");
            if (channelCount == 0)
                throw new InvalidOperationException("CandleLight device has no CAN channels.");

            Ensure(CandleNative.candle_channel_set_bitrate(_deviceHandle, _channelIndex, (uint)options.Bitrate),
                $"set bitrate {options.Bitrate}");
            Ensure(CandleNative.candle_channel_start(_deviceHandle, _channelIndex, CandleMode.Normal),
                "start CAN channel");

            _rxCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _rxTask = Task.Run(() => ReceiveLoopAsync(_rxCts.Token), CancellationToken.None);
            return Task.CompletedTask;
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException("candle.dll was not found. Copy the Candle native DLL next to CanMonitor.Wpf.exe.", ex);
        }
        catch (EntryPointNotFoundException ex)
        {
            throw new InvalidOperationException("candle.dll is present but does not expose the expected Candle API.", ex);
        }
    }

    protected override async Task StopDriverAsync()
    {
        _rxCts?.Cancel();
        if (_rxTask is not null)
        {
            try { await _rxTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        lock (_driverGate)
        {
            if (_deviceHandle == IntPtr.Zero)
                return;

            _ = CandleNative.candle_channel_stop(_deviceHandle, _channelIndex);
            _ = CandleNative.candle_dev_close(_deviceHandle);
            _ = CandleNative.candle_dev_free(_deviceHandle);
            _deviceHandle = IntPtr.Zero;
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

        var payload = frame.Data.ToArray();
        var nativeFrame = new CandleFrame
        {
            can_id = frame.Id,
            can_dlc = (byte)payload.Length,
            channel = _channelIndex,
            flags = 0,
            reserved = 0,
            data = new byte[8],
            timestamp_us = 0
        };

        if (frame.IsExtended)
            nativeFrame.can_id |= CanExtendedFrameFlag;
        Buffer.BlockCopy(payload, 0, nativeFrame.data, 0, payload.Length);

        lock (_driverGate)
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("CandleLight device is not open.");

            Ensure(CandleNative.candle_frame_send(_deviceHandle, _channelIndex, ref nativeFrame), "send CAN frame");
        }

        return Task.CompletedTask;
    }

    private static int ParseDeviceIndex(string? channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId))
            return 0;
        if (!int.TryParse(channelId, out var result) || result < 0)
            throw new ArgumentException("Channel must be a non-negative CandleLight device index.", nameof(channelId));
        return result;
    }

    private static IntPtr OpenDevice(int deviceIndex)
    {
        Ensure(CandleNative.candle_list_scan(out var list), "scan CandleLight devices");
        try
        {
            Ensure(CandleNative.candle_list_length(list, out var count), "read device count");
            if (count == 0)
                throw new InvalidOperationException("No CandleLight/CANable USB devices were found.");
            if (deviceIndex >= count)
                throw new InvalidOperationException($"CandleLight device index {deviceIndex} is out of range. Found {count} device(s).");

            Ensure(CandleNative.candle_dev_get(list, (byte)deviceIndex, out var device), "select CandleLight device");
            Ensure(CandleNative.candle_dev_open(device), "open CandleLight device");
            return device;
        }
        finally
        {
            _ = CandleNative.candle_list_free(list);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            CandleFrame nativeFrame;
            bool hasFrame;

            lock (_driverGate)
            {
                if (_deviceHandle == IntPtr.Zero)
                    return;
                hasFrame = CandleNative.candle_frame_read(_deviceHandle, out nativeFrame, 20);
            }

            if (!hasFrame)
            {
                await Task.Yield();
                continue;
            }

            var rawId = nativeFrame.can_id;
            if ((rawId & CanErrorFrameFlag) != 0 || (rawId & CanRtrFrameFlag) != 0)
                continue;

            var length = Math.Min(nativeFrame.can_dlc, (byte)8);
            var data = new byte[length];
            if (nativeFrame.data is not null)
                Buffer.BlockCopy(nativeFrame.data, 0, data, 0, length);

            PublishRx(new CanFrame(
                rawId & CanIdMask,
                (rawId & CanExtendedFrameFlag) != 0,
                data,
                DateTimeOffset.UtcNow,
                CanDirection.Rx));
        }
    }

    private static void Ensure(bool ok, string operation)
    {
        if (!ok)
            throw new InvalidOperationException($"CandleLight API failed to {operation}.");
    }

    private enum CandleMode : uint
    {
        Normal = 0
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CandleFrame
    {
        public uint echo_id;
        public uint can_id;
        public byte can_dlc;
        public byte channel;
        public byte flags;
        public byte reserved;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] data;

        public uint timestamp_us;
    }

    private static class CandleNative
    {
        private const string DllName = "candle.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool candle_list_scan(out IntPtr list);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool candle_list_free(IntPtr list);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool candle_list_length(IntPtr list, out byte length);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool candle_dev_get(IntPtr list, byte devNum, out IntPtr device);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool candle_dev_open(IntPtr device);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool candle_dev_close(IntPtr device);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool candle_dev_free(IntPtr device);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool candle_channel_count(IntPtr device, out byte numChannels);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool candle_channel_set_bitrate(IntPtr device, byte channel, uint bitrate);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool candle_channel_start(IntPtr device, byte channel, CandleMode flags);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool candle_channel_stop(IntPtr device, byte channel);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool candle_frame_send(IntPtr device, byte channel, ref CandleFrame frame);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool candle_frame_read(IntPtr device, out CandleFrame frame, uint timeoutMs);
    }
}
