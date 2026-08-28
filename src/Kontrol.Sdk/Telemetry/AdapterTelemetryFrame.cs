using Kontrol.Sdk.Settings;

namespace Kontrol.Sdk.Telemetry;

/// <summary>
/// Adapter telemetry plus optional runtime presentation updates. Presentation
/// updates let an adapter report an effective game-derived unit without giving
/// the host game-specific conversion rules.
/// </summary>
public sealed record AdapterTelemetryFrame
{
    public required IReadOnlyDictionary<string, string> Values { get; init; }
    public IReadOnlyDictionary<string, NumberSettingPresentation>? NumberPresentations { get; init; }
}
