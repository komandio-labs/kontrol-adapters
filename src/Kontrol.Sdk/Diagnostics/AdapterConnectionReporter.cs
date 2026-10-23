using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
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
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private readonly Lock _sync = new();
    private readonly MmfChannel<TelemetryData> _channel = new($"Local\\Kontrol_AdapterStatus_{adapterId}");
    private readonly AdapterLogReporter _diagnosticReporter = new(adapterId);
    private long _sequence;
    private bool _initialized;
    private (string Title, string Message, string? Recommendation)? _activeError;
    private bool _disposed;

    public void ReportLoaded()
    {
        lock (_sync)
        {
            if (!_disposed) Send(AdapterRuntimeState.Loaded);
        }
    }

    public void ReportActive()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _activeError = null;
            Send(AdapterRuntimeState.Active);
        }
    }

    public void Pulse()
    {
        lock (_sync)
        {
            if (!_disposed) Send(_activeError is not null ? AdapterRuntimeState.Error : AdapterRuntimeState.Active);
        }
    }

    public void ReportError(string title, string message, string? recommendation = null)
    {
        lock (_sync)
        {
            if (_disposed) return;
            _activeError = (title, message, recommendation);
            Send(AdapterRuntimeState.Error);
        }
    }

    public void ClearError()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _activeError = null;
            Send(AdapterRuntimeState.Active);
        }
    }

    private void Send(AdapterRuntimeState state)
    {
        try
        {
            if (!_initialized)
            {
                _channel.CreateOrOpen();
                try
                {
                    _channel.Read(out var existingFrame);
                    string existingJson = existingFrame.GetJson();
                    if (!string.IsNullOrWhiteSpace(existingJson))
                    {
                        AdapterRuntimeStatus? existingStatus = JsonSerializer.Deserialize<AdapterRuntimeStatus>(existingJson, SerializerOptions);
                        if (existingStatus is not null && existingStatus.Sequence > _sequence)
                        {
                            _sequence = existingStatus.Sequence;
                        }
                    }
                }
                catch
                {
                    // A malformed prior frame must not prevent a fresh status report.
                }
                _initialized = true;
            }

            var frame = new TelemetryData();
            var payload = new AdapterRuntimeStatus(
                ++_sequence,
                state,
                Process.GetCurrentProcess().Id,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                _activeError?.Title,
                _activeError?.Message,
                _activeError?.Recommendation);

            frame.SetJson(JsonSerializer.Serialize(payload, SerializerOptions));
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
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _channel.Dispose();
            _diagnosticReporter.Dispose();
        }
    }

}

public sealed record AdapterRuntimeStatus(
    [property: JsonPropertyName("sequence")] long Sequence,
    [property: JsonPropertyName("state")] AdapterRuntimeState State,
    [property: JsonPropertyName("processId")] int ProcessId,
    [property: JsonPropertyName("timestampUnixMilliseconds")] long TimestampUnixMilliseconds,
    [property: JsonPropertyName("errorTitle")] string? ErrorTitle = null,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage = null,
    [property: JsonPropertyName("recommendation")] string? Recommendation = null
);
