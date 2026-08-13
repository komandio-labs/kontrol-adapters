using Keen.VRage.Core.Plugins;

namespace Kontrol.Adapters.SpaceEngineers2;

/// <summary>SE2 plugin-loader wrapper. Used only when SE2 launches the adapter through <c>-plugins</c>.</summary>
public sealed class SpaceEngineers2Plugin : IPlugin
{
    public SpaceEngineers2Plugin() => SpaceEngineers2AdapterRuntime.Start();

    public void Init(object gameInstance) => SpaceEngineers2AdapterRuntime.Start();

    public void Update()
    {
    }

    public void Dispose() => SpaceEngineers2AdapterRuntime.Stop();
}
