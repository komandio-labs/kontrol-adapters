namespace Kontrol.Sdk.Inputs;

public enum InputSignalKind { Analog, Discrete }
public enum DiscreteBehavior { Momentary, Toggle, Trigger }
public enum InputSourceKind { Axis, Button, ButtonPair }
public enum DiscreteDeliveryMode { State, Event }

public sealed record DirectionLabels(string Negative, string Positive);
public sealed record AxisThresholdDefaults(float ActivateAt = 60f, float ReleaseBelow = 40f)
{
    public bool IsValid => ReleaseBelow >= 0f && ReleaseBelow < ActivateAt && ActivateAt <= 100f;
}

public sealed record InputDescriptor(
    string Id,
    string DisplayName,
    string Hint,
    string Category,
    int Order,
    InputSignalKind SignalKind,
    DiscreteBehavior? DiscreteBehavior = null,
    bool AllowInvert = false,
    float DefaultDeadzone = 0.05f,
    float DefaultExponent = 1.0f,
    IReadOnlyList<InputSourceKind>? AllowedSourceKinds = null,
    DiscreteBehavior? ActionBehavior = null,
    DiscreteDeliveryMode? DeliveryMode = null,
    DirectionLabels? DirectionLabels = null,
    AxisThresholdDefaults? AxisThresholdDefaults = null)
{
    // Keep the constructor shipped by SDK 1.1.x.  Adapter assemblies are
    // loaded into the host process and therefore bind to the CLR constructor
    // signature, not C# optional-parameter defaults.  Adding metadata to the
    // primary record constructor must not break an already-installed adapter.
    public InputDescriptor(
        string id,
        string displayName,
        string hint,
        string category,
        int order,
        InputSignalKind signalKind,
        DiscreteBehavior? discreteBehavior,
        bool allowInvert,
        float defaultDeadzone,
        float defaultExponent)
        : this(
            id,
            displayName,
            hint,
            category,
            order,
            signalKind,
            discreteBehavior,
            allowInvert,
            defaultDeadzone,
            defaultExponent,
            null,
            null,
            null,
            null,
            null)
    {
    }

    // Old descriptors have no structured source metadata. Preserve their
    // existing signal semantics while exposing the generic cross-kind choices;
    // labels remain the stable Negative/Positive fallback until an adapter
    // supplies semantic DirectionLabels.
    public IReadOnlyList<InputSourceKind> EffectiveAllowedSourceKinds => AllowedSourceKinds is { Count: > 0 }
        ? AllowedSourceKinds
        : SignalKind == InputSignalKind.Analog
            ? [InputSourceKind.Axis, InputSourceKind.ButtonPair]
            : [InputSourceKind.Button, InputSourceKind.Axis];
    public DiscreteBehavior EffectiveActionBehavior => ActionBehavior ?? DiscreteBehavior ?? Inputs.DiscreteBehavior.Momentary;
    public DiscreteDeliveryMode EffectiveDeliveryMode => DeliveryMode
        ?? (EffectiveActionBehavior == Inputs.DiscreteBehavior.Momentary ? DiscreteDeliveryMode.State : DiscreteDeliveryMode.Event);
}

public sealed record AdapterInputSchema(int Version, IReadOnlyList<InputDescriptor> Inputs)
{
    public static readonly AdapterInputSchema Empty = new(1, Array.Empty<InputDescriptor>());

    public static readonly AdapterInputSchema Default6Dof = new(1, new InputDescriptor[]
    {
        new("Pitch", "Pitch", "Nose Up / Nose Down", "Flight Controls", 1, InputSignalKind.Analog, AllowInvert: true),
        new("Roll", "Roll", "Bank Left / Bank Right", "Flight Controls", 2, InputSignalKind.Analog, AllowInvert: true),
        new("Yaw", "Yaw", "Turn Left / Turn Right", "Flight Controls", 3, InputSignalKind.Analog, AllowInvert: true),
        new("Surge", "Forward / Backward", "Longitudinal Thrust", "Linear Thrusters", 4, InputSignalKind.Analog, AllowInvert: true),
        new("Sway", "Left / Right", "Lateral Thrust", "Linear Thrusters", 5, InputSignalKind.Analog, AllowInvert: true),
        new("Heave", "Up / Down", "Vertical Thrust", "Linear Thrusters", 6, InputSignalKind.Analog, AllowInvert: true),
        new("Dampeners", "Inertial Dampeners", "Toggle automatic stopping", "Flight Systems", 7, InputSignalKind.Discrete, DiscreteBehavior: DiscreteBehavior.Toggle),
        new("Lights", "Headlights / Spotlights", "Toggle ship lights", "Flight Systems", 8, InputSignalKind.Discrete, DiscreteBehavior: DiscreteBehavior.Toggle),
        new("PrimaryAction", "Primary Action", "Fire primary tools or weapons", "Actions", 9, InputSignalKind.Discrete, DiscreteBehavior: DiscreteBehavior.Momentary),
        new("SecondaryAction", "Secondary Action", "Fire secondary tools or weapons", "Actions", 10, InputSignalKind.Discrete, DiscreteBehavior: DiscreteBehavior.Momentary),
    });
}
