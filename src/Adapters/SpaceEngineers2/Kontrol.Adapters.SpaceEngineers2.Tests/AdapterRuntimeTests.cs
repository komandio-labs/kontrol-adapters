using Keen.VRage.Core.Plugins;
using Kontrol.Adapters.SpaceEngineers2;
using NUnit.Framework;
using Shouldly;

namespace Kontrol.Adapters.SpaceEngineers2.Tests;

[TestFixture]
public class AdapterRuntimeTests
{
    [TearDown]
    public void TearDown() => SpaceEngineers2AdapterRuntime.Stop();

    [Test]
    public void Runtime_StartIsIdempotent_AndStopIsRepeatable()
    {
        SpaceEngineers2AdapterRuntime.Start();
        SpaceEngineers2AdapterRuntime.Start();

        SpaceEngineers2AdapterRuntime.IsRunning.ShouldBeTrue();

        Should.NotThrow(SpaceEngineers2AdapterRuntime.Stop);
        Should.NotThrow(SpaceEngineers2AdapterRuntime.Stop);
        SpaceEngineers2AdapterRuntime.IsRunning.ShouldBeFalse();
    }

    [Test]
    public void PluginWrapper_IsOnlyTheSe2PluginContract()
    {
        typeof(IPlugin).IsAssignableFrom(typeof(SpaceEngineers2Plugin)).ShouldBeTrue();
        typeof(SpaceEngineers2StartupHook).GetMethod("Initialize")!.IsStatic.ShouldBeTrue();
    }
}
