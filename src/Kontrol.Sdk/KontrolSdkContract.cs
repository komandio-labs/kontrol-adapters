namespace Kontrol.Sdk;

/// <summary>
/// Version identity shared by the public SDK API and its IPC contract.
/// Wire-level negotiation is performed by the host when adapters are loaded.
/// </summary>
public static class KontrolSdkContract
{
    public const string Version = "1.0.0";
    public const int Major = 1;
}
