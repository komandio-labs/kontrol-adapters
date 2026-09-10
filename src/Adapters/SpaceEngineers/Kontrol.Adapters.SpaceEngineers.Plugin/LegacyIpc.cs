using System;
using System.IO;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace Kontrol.Adapters.SpaceEngineers.Plugin
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
        private const string StatusMapName = @"Local\Kontrol_AdapterStatus_space-engineers";
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
                "{{\"sequence\":{0},\"state\":\"{1}\",\"processId\":{2},\"timestampUnixMilliseconds\":{3}{4}{5}{6}}}",
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

    /// <summary>
    /// Legacy Pulsar payload equivalent of the SDK adapter log reporter. The
    /// payload cannot reference Kontrol.Sdk, so it writes the same 512-byte
    /// telemetry frame shape consumed by Kontrol's RuntimeWorker.
    /// </summary>
    internal sealed class LegacyAdapterLogReporter : IDisposable
    {
        private const string LogMapName = @"Local\Kontrol_Logs_space-engineers";
        private readonly LegacyMmfChannel<TelemetryData> _channel = new LegacyMmfChannel<TelemetryData>(LogMapName);
        private readonly object _sync = new object();
        private readonly bool _debugFallback = string.Equals(
            Environment.GetEnvironmentVariable("KONTROL_ADAPTER_DEBUG"), "1", StringComparison.OrdinalIgnoreCase);
        private long _sequence;
        private bool _initialized;

        public void Write(string message) => WriteCore("Information", message);

        public void WriteWarning(string message) => WriteCore("Information", "[Warning] " + message);

        public void WriteDebug(string message) => WriteCore("Debug", message);

        public void WriteError(string message) => WriteCore("Error", message);

        public void Dispose() => _channel.Dispose();

        private void WriteCore(string level, string message)
        {
            string boundedMessage = message ?? string.Empty;
            if (boundedMessage.Length > 320)
                boundedMessage = boundedMessage.Substring(0, 320);

            lock (_sync)
            {
                string json = string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"sequence\":{0},\"message\":\"{1}\",\"level\":\"{2}\"}}",
                    ++_sequence,
                    EscapeJson(boundedMessage),
                    EscapeJson(level));

                try
                {
                    if (!_initialized)
                    {
                        _channel.CreateOrOpen();
                        _initialized = true;
                    }

                    var frame = new TelemetryData();
                    frame.SetJson(json);
                    _channel.Write(ref frame);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    if (_debugFallback)
                        AppendFallback("log IPC failed: " + exception.Message);
                }

                if (_debugFallback)
                    AppendFallback(json);
            }
        }

        private static string EscapeJson(string value) => value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");

        private static void AppendFallback(string line)
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Kontrol", "adapters", "space-engineers", "logs");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "adapter-debug.log"),
                    DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture) + " " + line + Environment.NewLine);
            }
            catch
            {
                // Diagnostics must never interrupt game input handling.
            }
        }
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
