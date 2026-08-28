using System.Reflection;
using HarmonyLib;
using Kontrol.Sdk.Diagnostics;

namespace Kontrol.Adapters.SpaceEngineers2;

/// <summary>
/// Process-wide adapter runtime shared by all SE2 loading mechanisms.
/// It deliberately has no dependency on SE2's plugin lifecycle interface.
/// </summary>
internal sealed class SpaceEngineers2AdapterRuntime : IDisposable
{
    private static readonly Lock Gate = new();
    private static SpaceEngineers2AdapterRuntime? _current;

    private readonly AdapterConnectionReporter _connectionReporter = new("space-engineers-2");
    private readonly Timer _heartbeatTimer;
    private readonly Harmony? _harmony;
    private bool _disposed;

    private SpaceEngineers2AdapterRuntime()
    {
        _connectionReporter.ReportLoaded();
        _heartbeatTimer = new Timer(_ => _connectionReporter.Pulse(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        SpaceEngineers2AdapterDiagnostics.Write("Space Engineers 2 controls adapter started.");
        SpaceEngineers2AdapterDiagnostics.WriteDebug($"Runtime: {Environment.Version}; location: {AppDomain.CurrentDomain.BaseDirectory}; assemblies loaded: {AppDomain.CurrentDomain.GetAssemblies().Length}");

        try
        {
            _harmony = new Harmony("Kontrol.Adapters.SpaceEngineers2");
            _harmony.PatchAll(Assembly.GetExecutingAssembly());
            var finalMovementCommit = AccessTools.DeclaredMethod(
                typeof(Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers.CockpitInputHandlerComponent),
                "UpdateControlData");
            var finalMovementPrefix = AccessTools.DeclaredMethod(
                typeof(Patches.CockpitInputPatch),
                nameof(Patches.CockpitInputPatch.UpdateControlDataPrefix));
            if (finalMovementCommit is null || finalMovementPrefix is null)
            {
                throw new MissingMethodException("Could not locate SE2's final movement commit or Kontrol's input patches.");
            }

            _harmony.Patch(finalMovementCommit, prefix: new HarmonyMethod(finalMovementPrefix));
            var patchInfo = Harmony.GetPatchInfo(finalMovementCommit);
            if (patchInfo?.Prefixes.Any(prefix => prefix.owner == _harmony.Id) != true)
            {
                throw new InvalidOperationException("Harmony did not register Kontrol's final movement commit prefix.");
            }

            SpaceEngineers2AdapterDiagnostics.Write("Space Engineers 2 control integration is ready.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug("Harmony patches successfully registered and applied to JIT memory, including SE2's final movement commit.");
            _connectionReporter.ReportActive();
        }
        catch (Exception ex)
        {
            _connectionReporter.ReportError(
                "Incompatible Game Version",
                "Space Engineers 2 updated its internal flight control methods. The installed adapter cannot hook into this game version.",
                "Check the adapter Library for an updated adapter build compatible with this game patch.");
            SpaceEngineers2AdapterDiagnostics.WriteError("Space Engineers 2 control integration could not be enabled.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"Harmony patching failed: {ex}");
        }
    }

    internal static void Start()
    {
        lock (Gate)
        {
            if (_current is null || _current._disposed)
            {
                _current = new SpaceEngineers2AdapterRuntime();
            }
        }
    }

    internal static void Stop()
    {
        SpaceEngineers2AdapterRuntime? runtime;
        lock (Gate)
        {
            runtime = _current;
            _current = null;
        }

        runtime?.Dispose();
    }

    internal static bool IsRunning
    {
        get
        {
            lock (Gate) return _current is { _disposed: false };
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Patches.TranslationPresentationState.Reset();
        _heartbeatTimer.Dispose();
        _connectionReporter.Dispose();
        if (_harmony is not null) _harmony.UnpatchAll(_harmony.Id);
    }
}

internal static class SpaceEngineers2AdapterDiagnostics
{
    private static readonly AdapterLogReporter Reporter = new("space-engineers-2");

    internal static bool IsDebugLoggingEnabled
    {
        get
        {
#if DEBUG
            return true;
#else
            return string.Equals(Environment.GetEnvironmentVariable("KONTROL_ADAPTER_DEBUG"), "1", StringComparison.Ordinal);
#endif
        }
    }

    internal static void Write(string message)
    {
        try { Reporter.Write(message); } catch (Exception) { }
    }

    internal static void WriteDebug(string message)
    {
        if (!IsDebugLoggingEnabled) return;
        try { Reporter.WriteDebug(message); } catch (Exception) { }
    }

    internal static void WriteError(string message)
    {
        try { Reporter.WriteError(message); } catch (Exception) { }
    }

}
