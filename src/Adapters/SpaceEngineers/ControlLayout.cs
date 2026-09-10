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
}
