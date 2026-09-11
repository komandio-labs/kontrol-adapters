namespace Kontrol.Adapters.SpaceEngineers;

/// <summary>Stable schema positions shared by the Kontrol host entry and Pulsar payload.</summary>
public static class SpaceEngineersControlLayout
{
    public const int DampenersAction = 6;
    public const int LightsAction = 7;
    public const int LandingGearsAction = 8;
    public const int HandbrakeAction = 9;
    public const int CameraModeSwitchAction = 10;
    public const int ToolbarFirstAction = 11;
    public const int ToolbarActionCount = 10;
    public const int LeaveControlAction = ToolbarFirstAction + ToolbarActionCount;
    public const int ReactorsAction = LeaveControlAction + 1;
    public const int ShowTerminalAction = ReactorsAction + 1;
    public const int ShowInventoryAction = ShowTerminalAction + 1;
    public const int PrimaryAction = ShowInventoryAction + 1;
    public const int SecondaryAction = PrimaryAction + 1;
    public const int BroadcastingAction = SecondaryAction + 1;
    public const int LocalPowerAction = BroadcastingAction + 1;
    public const int ToggleHudAction = LocalPowerAction + 1;
    public const int ChatScreenAction = ToggleHudAction + 1;
    public const int VoiceChatAction = ChatScreenAction + 1;
    public const int HoldLookAroundAction = VoiceChatAction + 1;
    public const int ToggleLookAroundAction = HoldLookAroundAction + 1;
    // Analog values are packed separately from discrete schema positions.
    public const int CameraLookHorizontalAnalog = 6;
    public const int CameraLookVerticalAnalog = 7;
    public const int CameraZoomAnalog = 8;
}
