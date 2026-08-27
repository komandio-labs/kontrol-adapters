using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kontrol.Sdk.Settings;

/// <summary>
/// Immutable, sanitized, fully validated snapshot of adapter settings.
/// Serves as the authoritative source of truth consumed by adapter physics engines and IPC channels.
/// </summary>
public sealed class AdapterSettingsSnapshot
{
    public ulong SequenceNumber { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public IReadOnlyDictionary<string, object?> Values { get; }
    public IReadOnlySet<string> ActiveKeys { get; }
    public IReadOnlyDictionary<string, NumberSettingPresentation> NumberPresentations { get; }

    [JsonConstructor]
    public AdapterSettingsSnapshot(
        ulong sequenceNumber,
        DateTime timestampUtc,
        IReadOnlyDictionary<string, object?> values,
        IReadOnlySet<string>? activeKeys = null,
        IReadOnlyDictionary<string, NumberSettingPresentation>? numberPresentations = null)
    {
        SequenceNumber = sequenceNumber;
        TimestampUtc = timestampUtc;
        Values = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(values));
        ActiveKeys = activeKeys ?? new HashSet<string>(values.Keys);
        NumberPresentations = new ReadOnlyDictionary<string, NumberSettingPresentation>(
            new Dictionary<string, NumberSettingPresentation>(numberPresentations ?? new Dictionary<string, NumberSettingPresentation>(), StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if a setting key is currently active and applicable under active VisibleWhen conditions.
    /// </summary>
    public bool IsActive(string key) => ActiveKeys.Contains(key);

    /// <summary>Gets adapter-resolved display metadata for a numeric setting.</summary>
    public bool TryGetNumberPresentation(string key, out NumberSettingPresentation presentation) =>
        NumberPresentations.TryGetValue(key, out presentation!);

    /// <summary>
    /// Safely gets a numeric value, falling back to defaultValue if missing, inactive, or invalid.
    /// </summary>
    public float GetNumber(string key, float defaultValue = 0f)
    {
        if (Values.TryGetValue(key, out var obj) && obj is not null)
        {
            if (obj is float f) return f;
            if (obj is double d) return (float)d;
            if (obj is int i) return i;
            if (obj is JsonElement je && je.ValueKind == JsonValueKind.Number && je.TryGetSingle(out float val))
                return val;
            if (float.TryParse(obj.ToString(), out float parsed))
                return parsed;
        }
        return defaultValue;
    }

    /// <summary>
    /// Safely gets a string value, falling back to defaultValue if missing or inactive.
    /// </summary>
    public string GetString(string key, string defaultValue = "")
    {
        if (Values.TryGetValue(key, out var obj) && obj is not null)
        {
            if (obj is JsonElement je && je.ValueKind == JsonValueKind.String)
                return je.GetString() ?? defaultValue;
            return obj.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Safely gets a boolean value, falling back to defaultValue if missing or inactive.
    /// </summary>
    public bool GetBoolean(string key, bool defaultValue = false)
    {
        if (Values.TryGetValue(key, out var obj) && obj is not null)
        {
            if (obj is bool b) return b;
            if (obj is JsonElement je && (je.ValueKind == JsonValueKind.True || je.ValueKind == JsonValueKind.False))
                return je.GetBoolean();
            if (bool.TryParse(obj.ToString(), out bool parsed))
                return parsed;
        }
        return defaultValue;
    }

    /// <summary>
    /// Safely gets an array value, falling back to defaultValue if missing or inactive.
    /// </summary>
    public IReadOnlyList<string> GetArray(string key, IReadOnlyList<string>? defaultValue = null)
    {
        if (Values.TryGetValue(key, out var obj) && obj is not null)
        {
            if (obj is IReadOnlyList<string> list) return list;
            if (obj is IEnumerable<object> enumerable)
                return enumerable.Select(o => o?.ToString() ?? string.Empty).ToList();
            if (obj is JsonElement je && je.ValueKind == JsonValueKind.Array)
            {
                var result = new List<string>();
                foreach (var el in je.EnumerateArray())
                {
                    result.Add(el.GetString() ?? el.ToString());
                }
                return result;
            }
        }
        return defaultValue ?? Array.Empty<string>();
    }

    /// <summary>
    /// Factory that constructs a verified snapshot by sanitizing user values against descriptors and resolving active conditions.
    /// </summary>
    public static AdapterSettingsSnapshot Create(
        IReadOnlyList<AdapterSettingDescriptor> descriptors,
        IReadOnlyDictionary<string, object?> rawValues,
        ulong sequenceNumber = 1,
        IReadOnlyDictionary<string, NumberSettingPresentation>? numberPresentationOverrides = null)
    {
        var sanitized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // 1. Initial pass: sanitize all values or assign defaults
        foreach (var desc in descriptors)
        {
            if (rawValues.TryGetValue(desc.Key, out var rawVal) && rawVal != null)
            {
                sanitized[desc.Key] = desc.Sanitize(rawVal);
            }
            else
            {
                sanitized[desc.Key] = desc switch
                {
                    NumberSettingDescriptor n => n.DefaultValue,
                    StringSettingDescriptor s => s.DefaultValue,
                    BooleanSettingDescriptor b => b.DefaultValue,
                    ArraySettingDescriptor a => a.DefaultValue,
                    _ => null
                };
            }
        }

        // 2. Resolve active conditions (VisibleWhen / EnableWhen)
        var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var desc in descriptors)
        {
            bool visible = desc.VisibleWhen == null || desc.VisibleWhen.Evaluate(sanitized);
            if (visible)
            {
                activeKeys.Add(desc.Key);
            }
        }

        var presentations = new Dictionary<string, NumberSettingPresentation>(StringComparer.OrdinalIgnoreCase);
        foreach (var number in descriptors.OfType<NumberSettingDescriptor>())
        {
            var presentation = new NumberSettingPresentation
            {
                Unit = number.PresentationUnit ?? number.CanonicalUnit,
                Multiplier = number.DisplayMultiplier ?? 1f,
                MinLabel = number.MinLabel,
                MidLabel = number.MidLabel,
                MaxLabel = number.MaxLabel
            };

            if (!string.IsNullOrWhiteSpace(number.PresentationSourceKey)
                && number.PresentationVariants is not null
                && sanitized.TryGetValue(number.PresentationSourceKey, out var sourceValue)
                && sourceValue is not null
                && number.PresentationVariants.TryGetValue(sourceValue.ToString() ?? string.Empty, out var variant))
            {
                presentation = variant;
            }

            presentations[number.Key] = presentation;
        }

        if (numberPresentationOverrides is not null)
        {
            foreach (var (key, presentation) in numberPresentationOverrides)
            {
                presentations[key] = presentation;
            }
        }

        return new AdapterSettingsSnapshot(sequenceNumber, DateTime.UtcNow, sanitized, activeKeys, presentations);
    }
}
