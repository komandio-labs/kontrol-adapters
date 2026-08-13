using System.Text.Json;
using Kontrol.Sdk.IPC;

namespace Kontrol.Sdk.Diagnostics;

/// <summary>Reports that an adapter was loaded in a target process and remains alive.</summary>
public sealed class AdapterConnectionReporter(string adapterId) : IDisposable
{
    private readonly MmfChannel<TelemetryData> _channel = new($"Local\\Kontrol_AdapterStatus_{adapterId}");
    private readonly AdapterLogReporter _diagnosticReporter = new(adapterId);
    private long _sequence;
    private bool _initialized;

    public void ReportLoaded() => Send("Loaded");
    public void Pulse() => Send("Heartbeat");

    private void Send(string state)
    {
        try
        {
            if (!_initialized)
            {
                _channel.CreateOrOpen();
                _initialized = true;
            }

            var frame = new TelemetryData();
            frame.SetJson(JsonSerializer.Serialize(new AdapterRuntimeStatus(
                ++_sequence, state, Environment.ProcessId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())));
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

public sealed record AdapterRuntimeStatus(long Sequence, string State, int ProcessId, long TimestampUnixMilliseconds);
