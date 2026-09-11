using System.Globalization;
using Kontrol.Sdk.IPC;

namespace Kontrol.Sdk.Diagnostics;

/// <summary>Legacy-CLR implementation of Kontrol's adapter-log IPC contract.</summary>
public sealed class AdapterLogReporter(string adapterId) : IDisposable
{
    private readonly MmfChannel<TelemetryData> _channel = new($"Local\\Kontrol_Logs_{adapterId}");
    private long _sequence;
    private bool _initialized;

    public void Write(string message) => WriteCore("Information", message);

    public void WriteDebug(string message) => WriteCore("Debug", message);

    public void WriteWarning(string message) => WriteCore("Information", "[Warning] " + message);

    public void WriteError(string message) => WriteCore("Error", message);

    public void Dispose() => _channel.Dispose();

    private void WriteCore(string level, string message)
    {
        try
        {
            if (!_initialized)
            {
                _channel.CreateOrOpen();
                _initialized = true;
            }

            string boundedMessage = message ?? string.Empty;
            if (boundedMessage.Length > 320)
            {
                boundedMessage = boundedMessage.Substring(0, 320);
            }

            string json = "{\"sequence\":" + (++_sequence).ToString(CultureInfo.InvariantCulture) +
                ",\"message\":\"" + Escape(boundedMessage) + "\",\"level\":\"" + Escape(level) + "\"}";
            var frame = new TelemetryData();
            frame.SetJson(json);
            _channel.Write(ref frame);
        }
        catch
        {
            // Logging must never affect the legacy game process.
        }
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
}
