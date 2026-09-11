using System.Diagnostics;
using System.Globalization;
using Kontrol.Sdk.IPC;

namespace Kontrol.Sdk.Diagnostics;

public enum AdapterRuntimeState
{
    Loaded,
    Active,
    Error
}

/// <summary>Legacy-CLR implementation of Kontrol's adapter-status IPC contract.</summary>
public sealed class AdapterConnectionReporter(string adapterId) : IDisposable
{
    private readonly MmfChannel<TelemetryData> _channel = new($"Local\\Kontrol_AdapterStatus_{adapterId}");
    private long _sequence;
    private bool _initialized;
    private string? _errorTitle;
    private string? _errorMessage;
    private string? _recommendation;
    private bool _hasError;

    public void ReportLoaded() => Send(AdapterRuntimeState.Loaded);

    public void ReportActive()
    {
        _hasError = false;
        Send(AdapterRuntimeState.Active);
    }

    public void Pulse() => Send(_hasError ? AdapterRuntimeState.Error : AdapterRuntimeState.Active);

    public void ReportError(string title, string message, string? recommendation = null)
    {
        _errorTitle = title;
        _errorMessage = message;
        _recommendation = recommendation;
        _hasError = true;
        Send(AdapterRuntimeState.Error);
    }

    public void ClearError()
    {
        _hasError = false;
        Send(AdapterRuntimeState.Active);
    }

    public void Dispose() => _channel.Dispose();

    private void Send(AdapterRuntimeState state)
    {
        try
        {
            if (!_initialized)
            {
                _channel.CreateOrOpen();
                _initialized = true;
            }

            string json = "{\"sequence\":" + (++_sequence).ToString(CultureInfo.InvariantCulture) +
                ",\"state\":\"" + state + "\",\"processId\":" + Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture) +
                ",\"timestampUnixMilliseconds\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture) +
                JsonProperty("errorTitle", _hasError ? _errorTitle : null) +
                JsonProperty("errorMessage", _hasError ? _errorMessage : null) +
                JsonProperty("recommendation", _hasError ? _recommendation : null) + "}";
            var frame = new TelemetryData();
            frame.SetJson(json);
            _channel.Write(ref frame);
        }
        catch
        {
            // Adapter status must never affect the legacy game process.
        }
    }

    private static string JsonProperty(string name, string? value) => value == null
        ? string.Empty
        : ",\"" + name + "\":\"" + Escape(value) + "\"";

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
}
