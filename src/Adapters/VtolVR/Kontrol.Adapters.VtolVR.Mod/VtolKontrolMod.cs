using System;
using System.Globalization;
using System.Text.RegularExpressions;
using HarmonyLib;
using Kontrol.Sdk.Diagnostics;
using Kontrol.Sdk.IPC;
using Kontrol.Adapters.VtolVR;
using ModLoader.Framework;
using ModLoader.Framework.Attributes;
using UnityEngine;

namespace Kontrol.Adapters.VtolVR.Mod;

[ItemId("kontrol.vtol-vr-adapter")]
public sealed class VtolKontrolMod : VtolMod
{
    private const string HarmonyId = "komandio.kontrol.vtol-vr";
    private const string InputMapName = @"Local\Kontrol_Input_vtol-vr";
    private const string SettingsMapName = @"Local\Kontrol_Settings_vtol-vr";
    private const uint InputSchemaVersion = 1;
    private static readonly Regex RateControlSetting = new Regex("\\\"throttle\\.rateControl\\\"\\s*:\\s*true", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RateSetting = new Regex("\\\"throttle\\.ratePerSecond\\\"\\s*:\\s*(?<value>-?(?:\\d+\\.?\\d*|\\.\\d+)(?:[eE][+-]?\\d+)?)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly MmfChannel<InputFrame> _input = new MmfChannel<InputFrame>(InputMapName);
    private readonly MmfChannel<TelemetryData> _settings = new MmfChannel<TelemetryData>(SettingsMapName);
    private readonly AdapterConnectionReporter _status = new AdapterConnectionReporter("vtol-vr");
    private static VtolKontrolMod _active;
    private Harmony _harmony;
    private float _nextHeartbeat;
    private string _lastSettingsJson = string.Empty;
    private bool _throttleRateControl;
    private float _throttleRatePerSecond = 0.5f;

    public void Awake()
    {
        try
        {
            _input.CreateOrOpen();
            _settings.CreateOrOpen();
            _active = this;
            _harmony = new Harmony(HarmonyId);
            var target = AccessTools.Method(typeof(VehicleInputManager), "Update");
            var vehiclePostfix = AccessTools.Method(typeof(VtolKontrolMod), nameof(AfterVehicleInputUpdate));
            var throttleTarget = AccessTools.Method(typeof(VRThrottle), "Update");
            var throttlePostfix = AccessTools.Method(typeof(VtolKontrolMod), nameof(AfterThrottleUpdate));
            if (target == null || vehiclePostfix == null || throttleTarget == null || throttlePostfix == null)
                throw new MissingMethodException("A VTOL vehicle or throttle update hook was not found.");
            _harmony.Patch(target, postfix: new HarmonyMethod(vehiclePostfix));
            _harmony.Patch(throttleTarget, postfix: new HarmonyMethod(throttlePostfix));
            if (!HasKontrolPostfix(target) || !HasKontrolPostfix(throttleTarget))
                throw new InvalidOperationException("HarmonyX did not register the Kontrol VTOL input patches.");
            _status.ReportLoaded();
            _status.Pulse();
        }
        catch (Exception exception)
        {
            _harmony?.UnpatchSelf();
            if (_active == this) _active = null;
            _status.ReportError("VTOL controls unavailable", exception.Message,
                "Confirm the VTOL build and Mod Loader references match the adapter's supported build.");
        }
    }

    public void Update()
    {
        try
        {
            if (Time.unscaledTime >= _nextHeartbeat)
            {
                _status.Pulse();
                _nextHeartbeat = Time.unscaledTime + 1f;
            }
        }
        catch (Exception exception)
        {
            _status.ReportError("VTOL control update failed", exception.Message,
                "Disable the Kontrol VTOL mod and check the installed game build.");
        }
    }

    private static bool HasKontrolPostfix(System.Reflection.MethodBase target)
    {
        var patchInfo = Harmony.GetPatchInfo(target);
        if (patchInfo == null) return false;
        foreach (var patch in patchInfo.Postfixes)
            if (patch.owner == HarmonyId) return true;
        return false;
    }

    private static void AfterVehicleInputUpdate(VehicleInputManager __instance) => _active?.ApplyFlightControls(__instance);

    private static void AfterThrottleUpdate(VRThrottle __instance) => _active?.ApplyThrottle(__instance);

    private void ApplyThrottle(VRThrottle throttle)
    {
        try
        {
            _input.Read(out var frame);
            RefreshSettings();
            if (frame.SchemaVersion != InputSchemaVersion || frame.IsInputEnabled == 0 || throttle == null || !throttle.gameObject.activeInHierarchy)
                return;

            float axis = frame.ReadAnalog(3);
            if (_throttleRateControl)
            {
                float current = throttle.currentThrottle;
                float next = ThrottleRateControl.Advance(current, axis, _throttleRatePerSecond, Time.deltaTime);
                if (Mathf.Abs(next - current) > 0.00001f)
                    throttle.RemoteSetThrottle(next);
            }
            else
                throttle.RemoteSetThrottle(Mathf.Clamp01(axis));
        }
        catch (Exception exception)
        {
            _status.ReportError("VTOL throttle update failed", exception.Message,
                "Disable the Kontrol VTOL mod and check the installed game build.");
        }
    }

    private void RefreshSettings()
    {
        _settings.Read(out var packet);
        string json = packet.GetJson();
        if (string.IsNullOrWhiteSpace(json) || string.Equals(json, _lastSettingsJson, StringComparison.Ordinal)) return;

        try
        {
            _throttleRateControl = RateControlSetting.IsMatch(json);
            Match rateMatch = RateSetting.Match(json);
            if (rateMatch.Success && float.TryParse(rateMatch.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float rate) &&
                !float.IsNaN(rate) && !float.IsInfinity(rate))
                _throttleRatePerSecond = Mathf.Clamp(rate, 0.1f, 2f);
            _lastSettingsJson = json;
        }
        catch (Exception exception)
        {
            _status.ReportError("VTOL settings update failed", exception.Message,
                "Check the adapter settings and disable incremental throttle if the issue continues.");
        }
    }

    private void ApplyFlightControls(VehicleInputManager manager)
    {
        try
        {
            _input.Read(out var frame);
            if (frame.SchemaVersion != InputSchemaVersion || frame.IsInputEnabled == 0) return;

            var controls = new Vector3(ClampAxis(frame.ReadAnalog(0)), ClampAxis(frame.ReadAnalog(2)), ClampAxis(frame.ReadAnalog(1)));
            if (manager.pyrOutputs != null)
            {
                foreach (FlightControlComponent output in manager.pyrOutputs)
                    if (output != null) output.SetPitchYawRoll(controls);
            }

            if (manager.tiltController != null)
                manager.tiltController.PadInputScaled(new Vector3(0f, ClampAxis(frame.ReadAnalog(4)), 0f));
            manager.SetVirtualBrakes(Mathf.Clamp01(frame.ReadAnalog(5)));
        }
        catch (Exception exception)
        {
            _status.ReportError("VTOL flight control update failed", exception.Message,
                "Disable the Kontrol VTOL mod and check the installed game build.");
        }
    }

    private static float ClampAxis(float value) => Mathf.Clamp(value, -1f, 1f);

    public override void UnLoad()
    {
        _harmony?.UnpatchSelf();
        if (_active == this) _active = null;
        _input.Dispose();
        _settings.Dispose();
        _status.Dispose();
    }
}
