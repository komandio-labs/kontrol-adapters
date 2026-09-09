using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace Kontrol.Adapters.SpaceEngineers1.Plugin
{
    internal sealed class LegacyMmfChannel<T> : IDisposable where T : struct
    {
        private readonly string _mapName;
        private readonly int _size = Marshal.SizeOf(typeof(T));
        private MemoryMappedFile _map;
        private MemoryMappedViewAccessor _view;

        public LegacyMmfChannel(string mapName) => _mapName = mapName;

        public void CreateOrOpen()
        {
            Dispose();
            _map = MemoryMappedFile.CreateOrOpen(_mapName, _size, MemoryMappedFileAccess.ReadWrite);
            _view = _map.CreateViewAccessor(0, _size, MemoryMappedFileAccess.ReadWrite);
        }

        public void Read(out T data)
        {
            if (_view is null)
            {
                data = default(T);
                return;
            }
            _view.Read(0, out data);
        }

        public void Write(ref T data) => _view?.Write(0, ref data);

        public void Dispose()
        {
            _view?.Dispose();
            _view = null;
            _map?.Dispose();
            _map = null;
        }
    }

    internal sealed class PulsarStatusReporter : IDisposable
    {
        private const string StatusMapName = @"Local\Kontrol_AdapterStatus_space-engineers-1";
        private readonly LegacyMmfChannel<TelemetryData> _channel = new LegacyMmfChannel<TelemetryData>(StatusMapName);
        private long _sequence;
        private long _lastPulseUtcTicks;

        public void ReportLoaded()
        {
            _channel.CreateOrOpen();
            Send("Loaded", null, null, null);
        }

        public void Pulse()
        {
            long now = DateTime.UtcNow.Ticks;
            if (now - _lastPulseUtcTicks < TimeSpan.TicksPerSecond)
                return;
            Send("Active", null, null, null);
        }

        public void ReportError(string title, string message, string recommendation)
        {
            Send("Error", title, message, recommendation);
        }

        public void Dispose() => _channel.Dispose();

        private void Send(string state, string errorTitle, string errorMessage, string recommendation)
        {
            _lastPulseUtcTicks = DateTime.UtcNow.Ticks;
            var frame = new TelemetryData();
            frame.SetJson(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{\"sequence\":{0},\"state\":\"{1}\",\"processId\":{2},\"timestampUnixMilliseconds\":{3}{4}{5}{6}}}",
                ++_sequence,
                state,
                Process.GetCurrentProcess().Id,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                JsonProperty("errorTitle", errorTitle),
                JsonProperty("errorMessage", errorMessage),
                JsonProperty("recommendation", recommendation)));
            _channel.Write(ref frame);
        }

        private static string JsonProperty(string name, string value) => value is null
            ? string.Empty
            : ",\"" + name + "\":\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal unsafe struct TelemetryData
    {
        private const int Capacity = 512;
        private fixed byte _bytes[Capacity];

        public void SetJson(string json)
        {
            byte[] source = Encoding.UTF8.GetBytes(json ?? string.Empty);
            fixed (byte* destination = _bytes)
            {
                for (int index = 0; index < Capacity; index++)
                    destination[index] = index < source.Length && index < Capacity - 1 ? source[index] : (byte)0;
            }
        }
    }
}
