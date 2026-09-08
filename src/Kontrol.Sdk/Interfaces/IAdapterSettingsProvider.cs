using Kontrol.Sdk.Diagnostics;
using Kontrol.Sdk.Settings;

namespace Kontrol.Sdk.Interfaces;

/// <summary>
/// Implemented by game adapters that expose configurable settings to the Kontrol host.
/// Provides schemas, default values, category metadata, and snapshot validation.
/// </summary>
public interface IAdapterSettingsProvider
{
    /// <summary>Revision of the adapter's declarative settings schema.</summary>
    int SchemaVersion => 1;

    /// <summary>Unique adapter identifier (e.g. "space-engineers-2").</summary>
    string AdapterId { get; }

    /// <summary>Ordered list of categories for UI grouping.</summary>
    IReadOnlyList<SettingCategoryGroup> Categories { get; }

    /// <summary>The complete setting schema descriptors supported by this adapter.</summary>
    IReadOnlyList<AdapterSettingDescriptor> Descriptors { get; }

    /// <summary>
    /// Adapter-owned optional diagnostic traces available in the current build.
    /// Trace identifiers are opaque to the host; an empty list means none are available.
    /// </summary>
    IReadOnlyList<AdapterTraceDescriptor> SupportedTraces => [];

    /// <summary>Produces the initial factory-default snapshot.</summary>
    AdapterSettingsSnapshot GetDefaultSnapshot();

    /// <summary>
    /// Validates candidate values against the descriptor schema.
    /// Returns true if all values are valid; otherwise false with field-specific errors.
    /// </summary>
    bool ValidateSettings(IReadOnlyDictionary<string, object?> values, out IReadOnlyDictionary<string, string> errors);

    /// <summary>
    /// Constructs a verified, sanitized snapshot from candidate values.
    /// </summary>
    AdapterSettingsSnapshot CreateSnapshot(IReadOnlyDictionary<string, object?> rawValues, ulong sequenceNumber = 1);
}
