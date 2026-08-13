namespace Kontrol.Adapters.SpaceEngineers2;

/// <summary>Entry point used by Kontrol's managed CoreCLR startup bootstrap.</summary>
public static class SpaceEngineers2StartupHook
{
    public static void Initialize() => SpaceEngineers2AdapterRuntime.Start();
}
