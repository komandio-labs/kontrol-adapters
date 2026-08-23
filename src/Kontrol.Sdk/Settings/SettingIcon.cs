namespace Kontrol.Sdk.Settings;

/// <summary>
/// Closed semantic icon catalog for flight simulation, hardware mapping, physics, and targeting.
/// Fully decoupled from UI framework icon sets (e.g. WPF-UI SymbolRegular or FontAwesome).
/// </summary>
public enum SettingIcon
{
    // General & Controls
    Sliders = 0,
    Settings = 1,
    Options = 2,
    Wrench = 3,
    Gauge = 4,

    // Flight & Spacecraft
    Spacecraft = 10,
    Airplane = 11,
    Rocket = 12,
    Atmosphere = 13,
    Orbit = 14,
    Satellite = 15,

    // Physics & Inertia
    Gyroscope = 20,
    Inertia = 21,
    Thruster = 22,
    Afterburner = 23,
    Compass = 24,
    Magnet = 25,
    Gravity = 26,

    // HUD & Targeting
    Crosshair = 30,
    AngularReticle = 31,
    Radar = 32,
    Target = 33,
    Shield = 34,
    Eye = 35,

    // Hardware & Input
    Joystick = 40,
    ThrottleQuadrant = 41,
    RudderPedals = 42,
    Gamepad = 43,
    Keyboard = 44,
    Mouse = 45,

    // System & Diagnostics
    Chip = 50,
    Network = 51,
    Clock = 52,
    Bolt = 53,
    Sparkles = 54
}
