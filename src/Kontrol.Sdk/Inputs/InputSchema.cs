namespace Kontrol.Sdk.Inputs;

public enum InputSignalKind { Analog, Discrete }
public enum DiscreteBehavior { Momentary, Toggle, Trigger }

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
    float DefaultExponent = 1.0f);

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
