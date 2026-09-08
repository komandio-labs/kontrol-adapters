namespace Kontrol.Sdk.Diagnostics;

/// <summary>
/// Adapter-owned metadata for one optional diagnostic trace.
/// The identifier is opaque to the host and is stable only within its adapter.
/// </summary>
public sealed record AdapterTraceDescriptor(
    string Id,
    string DisplayName,
    string Description);
