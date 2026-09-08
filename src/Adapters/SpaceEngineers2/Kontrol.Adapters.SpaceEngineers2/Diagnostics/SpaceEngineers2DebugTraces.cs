#if DEBUG
using System.Text.Json;
using Kontrol.Sdk.Diagnostics;

namespace Kontrol.Adapters.SpaceEngineers2;

/// <summary>Stable identifiers for traces emitted by this adapter only.</summary>
internal static class SpaceEngineers2DebugTraceKeys
{
    internal const string VelocityHold = "velocity-hold";
    internal const string FlightMode = "flight-mode";
    internal const string CruiseState = "cruise-state";
    internal const string Performance = "performance";
}

/// <summary>
/// Debug-build trace metadata and the current selection received in adapter settings.
/// This type is excluded from Release assemblies so trace checks have no Release cost.
/// </summary>
internal static class SpaceEngineers2DebugTraces
{
    internal const string SettingsKey = "debugTraceIds";

    internal static IReadOnlyList<AdapterTraceDescriptor> Supported { get; } =
    [
        new(
            SpaceEngineers2DebugTraceKeys.VelocityHold,
            "Velocity hold",
            "Periodic velocity-hold axes, targets, commands, and cruise state."),
        new(
            SpaceEngineers2DebugTraceKeys.FlightMode,
            "Flight mode",
            "Flight-control mode and cockpit gyro-mode transitions."),
        new(
            SpaceEngineers2DebugTraceKeys.CruiseState,
            "Cruise control state",
            "Cruise-control set, reset, cancellation, and target-adjustment events."),
        new(
            SpaceEngineers2DebugTraceKeys.Performance,
            "Adapter performance",
            "Periodic timing summaries and slow-operation reports for adapter work.")
    ];

    private static readonly HashSet<string> SupportedIds =
        Supported.Select(trace => trace.Id).ToHashSet(StringComparer.Ordinal);
    private static IReadOnlySet<string> _enabled = new HashSet<string>(StringComparer.Ordinal);

    internal static bool IsEnabled(string traceId) => Volatile.Read(ref _enabled).Contains(traceId);

    internal static void Apply(IReadOnlyDictionary<string, object?> values)
    {
        if (!values.TryGetValue(SettingsKey, out object? rawValue))
        {
            Volatile.Write(ref _enabled, new HashSet<string>(StringComparer.Ordinal));
            return;
        }

        var enabled = new HashSet<string>(StringComparer.Ordinal);
        foreach (string traceId in ReadTraceIds(rawValue))
        {
            if (SupportedIds.Contains(traceId)) enabled.Add(traceId);
        }

        Volatile.Write(ref _enabled, enabled);
    }

    private static IEnumerable<string> ReadTraceIds(object? rawValue)
    {
        if (rawValue is IEnumerable<string> values) return values;
        if (rawValue is JsonElement { ValueKind: JsonValueKind.Array } array)
        {
            return array.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))!;
        }

        return [];
    }
}
#endif
