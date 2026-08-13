using System.Text.Json;
using Kontrol.Sdk.IPC;

namespace Kontrol.Sdk.Diagnostics;

/// <summary>Reports adapter diagnostics to Kontrol through IPC, with an opt-in disk fallback for diagnostics.</summary>
public sealed class AdapterLogReporter(string adapterId) : IDisposable
{
    private readonly MmfChannel<TelemetryData> _channel = new($"Local\\Kontrol_Logs_{adapterId}");
    private long _sequence;
    private bool _initialized;

    public void Write(string message) => Write(message, AdapterLogLevel.Information);

    public void WriteDebug(string message) => Write(message, AdapterLogLevel.Debug);

    public void WriteError(string message) => Write(message, AdapterLogLevel.Error);

    private void Write(string message, AdapterLogLevel level)
    {
        WriteDebugFallback(level, message);
        try
        {
            if (!_initialized)
            {
                _channel.CreateOrOpen();
                _initialized = true;
            }

            // The shared IPC payload holds 512 UTF-8 bytes. Keep the JSON valid
            // instead of letting the fixed transport truncate it mid-message.
            string boundedMessage = message.Length <= 320 ? message : $"{message[..317]}...";
            var payload = new TelemetryData();
            payload.SetJson(JsonSerializer.Serialize(new AdapterLogEvent(++_sequence, boundedMessage, level)));
            _channel.Write(ref payload);
        }
        catch
        {
            // Logging must never affect the target process.
        }
    }

    public void Dispose() => _channel.Dispose();

    private void WriteDebugFallback(AdapterLogLevel level, string message)
    {
        // Target-process file I/O is deliberately opt-in. In normal operation the
        // host owns persistent logs; this fallback exists only when IPC is broken.
        if (!string.Equals(Environment.GetEnvironmentVariable("KONTROL_ADAPTER_DEBUG"), "1", StringComparison.Ordinal)) return;

        try
        {
            string? folder = Environment.GetEnvironmentVariable("KONTROL_ADAPTER_LOG_FOLDER");
            if (string.IsNullOrWhiteSpace(folder)) folder = adapterId;
            string path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Kontrol", "adapters", folder, "logs", "adapter-debug.log");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            const long maxLogBytes = 10 * 1024 * 1024;
            if (File.Exists(path) && new FileInfo(path).Length >= maxLogBytes)
            {
                string archive = Path.Combine(Path.GetDirectoryName(path)!, $"adapter-debug-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
                File.Move(path, archive);
            }

            foreach (string oldLog in Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "adapter-debug-*.log")
                         .Where(file => File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-7)))
            {
                File.Delete(oldLog);
            }
            File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff} {level}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostic logging must never affect the target process.
        }
    }
}

public enum AdapterLogLevel { Debug, Information, Error }

public sealed record AdapterLogEvent(long Sequence, string Message, AdapterLogLevel Level = AdapterLogLevel.Information);
