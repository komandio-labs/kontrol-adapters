using System.Reflection;
using Kontrol.Sdk.Settings;
using NUnit.Framework;
using Shouldly;

namespace Kontrol.AdapterTool.Tests;

public sealed class SdkBinaryCompatibilityTests
{
    [Test]
    public void AdapterSettingsSnapshot_PreservesTheSdk11ConstructorAndFactorySignatures()
    {
        var constructor = typeof(AdapterSettingsSnapshot).GetConstructor(
            [
                typeof(ulong),
                typeof(DateTime),
                typeof(IReadOnlyDictionary<string, object?>),
                typeof(IReadOnlySet<string>)
            ]);
        constructor.ShouldNotBeNull();

        var factory = typeof(AdapterSettingsSnapshot).GetMethod(
            nameof(AdapterSettingsSnapshot.Create),
            BindingFlags.Public | BindingFlags.Static,
            [
                typeof(IReadOnlyList<AdapterSettingDescriptor>),
                typeof(IReadOnlyDictionary<string, object?>),
                typeof(ulong)
            ]);
        factory.ShouldNotBeNull();
    }
}
