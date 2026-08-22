using System.Text.Json;
using System.Text.Json.Serialization;
using Kontrol.Sdk.IPC;

namespace Kontrol.Sdk.Diagnostics;

/// <summary>Represents the in-process operational state of a Kontrol adapter.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AdapterRuntimeState
{
    /// <summary>Adapter assembly is loaded into the target process and is initializing SDK/hooks.</summary>
    Loaded,
    /// <summary>Adapter is fully hooked, operational, and streaming telemetry/input.</summary>
    Active,
    /// <summary>Adapter encountered an in-process initialization or runtime failure.</summary>
    Error
}

/// <summary>Reports that an adapter was loaded in a target process, its operational state, and diagnostic health.</summary>
public sealed class AdapterConnectionReporter(string adapterId) : IDisposable
{
    private readonly MmfChannel<TelemetryData> _channel = new($"Local\\Kontrol_AdapterStatus_{adapterId}");
    private readonly AdapterLogReporter _diagnosticReporter = new(adapterId);
    private long _sequence;
    private bool _initialized;
    private (string Title, string Message, string? Recommendation)? _activeError;

    public void ReportLoaded() => Send(AdapterRuntimeState.Loaded);

    public void ReportActive()
    {
        _activeError = null;
        Send(AdapterRuntimeState.Active);
    }

    public void Pulse() => Send(_activeError is not null ? AdapterRuntimeState.Error : AdapterRuntimeState.Active);

    public void ReportError(string title, string message, string? recommendation = null)
    {
        _activeError = (title, message, recommendation);
        Send(AdapterRuntimeState.Error);
    }

    public void ClearError()
    {
        _activeError = null;
        Send(AdapterRuntimeState.Active);
    }

    private void Send(AdapterRuntimeState state)
    {
        try
        {
            if (!_initialized)
            {
                _channel.CreateOrOpen();
                _initialized = true;
            }

            var frame = new TelemetryData();
            var payload = new AdapterRuntimeStatus(
                ++_sequence,
                state,
                Environment.ProcessId,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                _activeError?.Title,
                _activeError?.Message,
                _activeError?.Recommendation);

            frame.SetJson(JsonSerializer.Serialize(payload));
            _channel.Write(ref frame);
        }
        catch (Exception ex)
        {
            // In normal operation this remains IPC-only. With explicit Debug Mode
            // enabled, AdapterLogReporter also records the failure to the fallback
            // file so an unavailable status channel is diagnosable.
            _diagnosticReporter.WriteDebug($"Adapter heartbeat IPC report failed: {ex}");
        }
    }

    public void Dispose()
    {
        _channel.Dispose();
        _diagnosticReporter.Dispose();
    }
}

public sealed record AdapterRuntimeStatus(
    long Sequence,
    AdapterRuntimeState State,
    int ProcessId,
    long TimestampUnixMilliseconds,
    string? ErrorTitle = null,
    string? ErrorMessage = null,
    string? Recommendation = null
);
