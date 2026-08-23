namespace Kontrol.Sdk.Settings;

/// <summary>
/// Defines when and how an adapter setting update takes effect.
/// </summary>
public enum SettingUpdateScope
{
    /// <summary>Takes effect instantly in-game over shared memory IPC with zero restart.</summary>
    Realtime = 0,

    /// <summary>Requires leaving flight/cockpit mode or re-engaging autopilot systems.</summary>
    OnSessionResume = 1,

    /// <summary>Requires restarting the game adapter runtime.</summary>
    RequiresAdapterRestart = 2,

    /// <summary>Requires completely restarting the game process.</summary>
    RequiresGameRestart = 3
}
