using System.Text.Json.Serialization;

namespace Kontrol.Sdk.Settings;

/// <summary>
/// Polymorphic base descriptor for an adapter setting contract.
/// Serializes cleanly across IPC and network boundaries using System.Text.Json type discriminators.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(NumberSettingDescriptor), typeDiscriminator: "number")]
[JsonDerivedType(typeof(StringSettingDescriptor), typeDiscriminator: "string")]
[JsonDerivedType(typeof(BooleanSettingDescriptor), typeDiscriminator: "boolean")]
[JsonDerivedType(typeof(ArraySettingDescriptor), typeDiscriminator: "array")]
public abstract record AdapterSettingDescriptor
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Category { get; init; }
    public SettingType Type { get; init; }
    public SettingIcon Icon { get; init; } = SettingIcon.Sliders;
    public string? Description { get; init; }
    public string? Limitations { get; init; }
    public SettingUpdateScope UpdateScope { get; init; } = SettingUpdateScope.Realtime;
    public LayoutSpan Layout { get; init; } = LayoutSpan.Half;
    public SettingCondition? VisibleWhen { get; init; }
    public SettingCondition? EnableWhen { get; init; }
    public bool IsReadOnly { get; init; } = false;

    /// <summary>
    /// Validates a candidate value against this descriptor's constraints.
    /// Returns true if valid; otherwise false and writes an error message.
    /// </summary>
    public abstract bool Validate(object? value, out string? errorMessage);

    /// <summary>
    /// Clamps or sanitizes a candidate value so it is safe to apply in the game engine.
    /// </summary>
    public abstract object? Sanitize(object? value);
}
