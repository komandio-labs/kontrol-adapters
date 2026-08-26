using System.Text.Json;
using Kontrol.Sdk.Diagnostics;
using Kontrol.Sdk.Settings;

namespace Kontrol.Adapters.SpaceEngineers2.Settings;

/// <summary>
/// Manages live settings synchronization over IPC and provides snapshot access to the cockpit patch.
/// </summary>
public sealed class SpaceEngineers2SettingsManager : IDisposable
{
    private static readonly object InstanceLock = new();
    private static SpaceEngineers2SettingsManager? _instance;

    private readonly SpaceEngineers2SettingsProvider _provider;
    private AdapterSettingsSnapshot _currentSnapshot;
    private bool _disposed;

    public static SpaceEngineers2SettingsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (InstanceLock)
                {
                    if (_instance == null)
                    {
                        var provider = new SpaceEngineers2SettingsProvider();
                        _instance = new SpaceEngineers2SettingsManager(provider);
                    }
                }
            }
            return _instance;
        }
    }

    public SpaceEngineers2SettingsProvider Provider => _provider;
    public AdapterSettingsSnapshot CurrentSnapshot => _currentSnapshot;

    public string FlightModelMode => _currentSnapshot.GetString("flightModelMode", "DirectAngularFlight");
    public bool IsNativeReticleSteering => string.Equals(FlightModelMode, "NativeReticleSteering", StringComparison.OrdinalIgnoreCase);
    public bool IsDirectAngularFlight => string.Equals(FlightModelMode, "DirectAngularFlight", StringComparison.OrdinalIgnoreCase);
    public string TranslationControlMode => _currentSnapshot.GetString("translationControlMode", "DirectThrust");
    public bool IsVelocityHoldTranslation => string.Equals(TranslationControlMode, "VelocityHold", StringComparison.OrdinalIgnoreCase);
    public float VelocityHoldMaxTargetSpeed => _currentSnapshot.GetNumber("velocityHoldMaxTargetSpeed", 300f);
    public float DirectAngularAcceleration => _currentSnapshot.GetNumber("directAngularAcceleration", 1.3f);
    public float DirectAngularDeceleration => _currentSnapshot.GetNumber("directAngularDeceleration", 1.0f);
    public float DirectAngularMaxRate => _currentSnapshot.GetNumber("directAngularMaxRate", 0.85f);

    private SpaceEngineers2SettingsManager(SpaceEngineers2SettingsProvider provider)
    {
        _provider = provider;
        _currentSnapshot = provider.GetDefaultSnapshot();
    }

    /// <summary>
    /// Applies a candidate raw settings dictionary or JSON snapshot to the live flight settings.
    /// </summary>
    public void ApplySettings(IReadOnlyDictionary<string, object?> rawValues, ulong sequenceNumber = 1)
    {
        var snapshot = _provider.CreateSnapshot(rawValues, sequenceNumber);
        _currentSnapshot = snapshot;
        SpaceEngineers2AdapterDiagnostics.Write($"Applied settings snapshot seq #{sequenceNumber} (Mode: {FlightModelMode})");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
