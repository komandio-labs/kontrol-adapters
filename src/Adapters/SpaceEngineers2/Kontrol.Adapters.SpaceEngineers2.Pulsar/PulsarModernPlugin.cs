using Keen.VRage.Core.Plugins;

namespace Kontrol.Adapters.SpaceEngineers2.Pulsar;

/// <summary>
/// .NET 10 entry point discovered by Pulsar Modern. The controls runtime remains
/// in the shared Kontrol adapter assembly so every supported SE2 loader follows
/// the same IPC and Harmony path.
/// </summary>
public sealed class PulsarModernPlugin : IPlugin, IDisposable
{
    private readonly SpaceEngineers2Plugin _adapter = new();

    public void Dispose() => _adapter.Dispose();
}
