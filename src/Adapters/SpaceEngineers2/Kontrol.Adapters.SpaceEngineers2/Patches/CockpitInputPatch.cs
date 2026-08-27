using System.Reflection;
using System.Text.Json;
using HarmonyLib;
using Keen.Game2.Client.GameSystems.PlayerControl.PlayerInput.InputHandlers;
using Keen.Game2.Simulation.GameSystems.Movement;
using Keen.Game2.Simulation.WorldObjects.CubeBlocks;
using Keen.Game2.Simulation.WorldObjects.Movement;
using Keen.VRage.Core;
using Keen.VRage.Core.Game.Components;
using Keen.VRage.Core.Game.Data;
using Keen.VRage.Library.Mathematics;
using Kontrol.Adapters.SpaceEngineers2.Settings;
using Kontrol.Sdk.IPC;
// ReSharper disable InconsistentNaming

namespace Kontrol.Adapters.SpaceEngineers2.Patches;

[HarmonyPatch]
public static class CockpitInputPatch
{
    private const int CruiseSetActionBit = 14;
    private const int CruiseIncreaseActionBit = 15;
    private const int CruiseDecreaseActionBit = 16;
    private const float CruiseThrottleDeadband = .02f;

    public readonly record struct NativeInputSnapshot(
        float PitchAnalog, float YawAnalog,
        float LookUp, float LookDown, float LookLeft, float LookRight,
        MovementInputs MovementInputs);

    private static readonly MethodInfo? SwitchGyroModeMethod = AccessTools.DeclaredMethod(
        typeof(CockpitInputHandlerComponent), "SwitchGyroMode", [typeof(bool)]);

    private static readonly MethodInfo? UpdateControlDataMethod = AccessTools.DeclaredMethod(
        typeof(CockpitInputHandlerComponent), "UpdateControlData");

    private static readonly FieldInfo? CockpitComponentField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_cockpitComponent");

    private static readonly FieldInfo? VelocityLimitsField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_velocityLimits");

    private static readonly FieldInfo? ObserverChildTransformField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_observerChildTransform");

    private static readonly FieldInfo? ObservedBlockField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_observedBlock");

    private static readonly FieldInfo? MovementInputsField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_movementInputs");

    private static readonly FieldInfo? TargetBasedGyroField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_targetBasedGyro");

    private static readonly FieldInfo? LookUpField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_lookUp");

    private static readonly FieldInfo? LookDownField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_lookDown");

    private static readonly FieldInfo? LookLeftField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_lookLeft");

    private static readonly FieldInfo? LookRightField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_lookRight");

    private static readonly FieldInfo? PitchAnalogField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_pitchAnalog");

    private static readonly FieldInfo? YawAnalogField = AccessTools.Field(
        typeof(CockpitInputHandlerComponent), "_yawAnalog");

    private static bool _wasKontrolActiveInCockpit;
    private static bool _originalDesiredTargetBasedGyro = true;
    private static bool _lastOverrideActiveState;
    private static object? _lastObservedBlock;
    private static Vector3 _currentCockpitAngularVelocity;

    [ThreadStatic]
    private static bool _committingControlData;

    private static readonly Dictionary<int, string> TriggerActions = new()
    {
        [6] = "ToggleDampeners",
        [7] = "ToggleLights",
        [8] = "ToggleParkingBrakes",
        [9] = "TogglePower",
        [10] = "InteractionActivated"
    };
    private static readonly Dictionary<(Type Type, string Name), MethodInfo?> TriggerMethods = new();

    private static readonly MmfChannel<InputFrame> ControlChannel = new("Local\\Kontrol_Input_space-engineers-2");
    private static readonly MmfChannel<TelemetryData> SettingsChannel = new("Local\\Kontrol_Settings_space-engineers-2");
    private static string? _lastSettingsJson;
    private static readonly MmfChannel<TelemetryData> TelemetryChannel = new("Local\\Kontrol_Telemetry_space-engineers-2");
    private static readonly Lock ChannelInitializationLock = new();
    private static bool _channelsInitialized;
    private static bool _channelFailureReported;
    private static bool _cockpitHookObserved;
    private static bool _missingObservedBlockReported;
    private static ulong _previousTriggeredActions;
    private static long _nextTranslationTraceTick;
    private static readonly CruiseControlState CruiseControl = new();

    private static void EnsureChannels()
    {
        if (_channelsInitialized) return;
        lock (ChannelInitializationLock)
        {
            if (_channelsInitialized) return;
            try
            {
                ControlChannel.CreateOrOpen();
                SettingsChannel.CreateOrOpen();
                TelemetryChannel.CreateOrOpen();
                _channelsInitialized = true;
                _channelFailureReported = false;
            }
            catch (Exception ex)
            {
                if (_channelFailureReported) return;
                _channelFailureReported = true;
                SpaceEngineers2AdapterDiagnostics.WriteError("The Space Engineers 2 adapter could not open its input channel.");
                SpaceEngineers2AdapterDiagnostics.WriteDebug($"IPC channel initialization error: {ex}");
            }
        }
    }

    internal static void ResetChannelsForTests()
    {
        lock (ChannelInitializationLock)
        {
            ControlChannel.Dispose();
            SettingsChannel.Dispose();
            TelemetryChannel.Dispose();
            _lastSettingsJson = null;
            _channelsInitialized = false;
            _channelFailureReported = false;
            _cockpitHookObserved = false;
            _previousTriggeredActions = 0UL;
            _wasKontrolActiveInCockpit = false;
            _lastOverrideActiveState = false;
            _lastObservedBlock = null;
            _nextTranslationTraceTick = 0;
            CruiseControl.Reset();
            TranslationPresentationState.Reset();
        }
    }

    internal static bool TryReadControlFrame(out InputFrame control)
    {
        EnsureChannels();
        if (!_channelsInitialized)
        {
            control = default;
            return false;
        }

        try
        {
            ControlChannel.Read(out control);
            return true;
        }
        catch (Exception ex)
        {
            control = default;
            if (_channelFailureReported) return false;
            _channelFailureReported = true;
            SpaceEngineers2AdapterDiagnostics.WriteError("The Space Engineers 2 adapter could not read its input channel.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"IPC channel read error: {ex}");
            return false;
        }
    }

    [HarmonyPatch(typeof(CockpitInputHandlerComponent), "UpdateRotationData")]
    [HarmonyPrefix]
    public static bool UpdateRotationDataPrefix(
        CockpitInputHandlerComponent __instance,
        ref float ____pitchAnalog,
        ref float ____yawAnalog,
        ref float ____lookUp,
        ref float ____lookDown,
        ref float ____lookLeft,
        ref float ____lookRight,
        ref MovementInputs ____movementInputs,
        object ____observedBlock,
        out NativeInputSnapshot __state
    )
    {
        __state = default;
        ApplyLiveSettings();
        var settings = SpaceEngineers2SettingsManager.Instance;

        // MODE 1: Native Reticle Steering
        if (settings.IsNativeReticleSteering)
        {
            __state = CaptureNativeInput(____pitchAnalog, ____yawAnalog, ____lookUp, ____lookDown,
                ____lookLeft, ____lookRight, ____movementInputs);
            ProcessNativeReticleOverride(__instance, ref ____pitchAnalog, ref ____yawAnalog, ref ____lookUp, ref ____lookDown,
                ref ____lookLeft, ref ____lookRight, ref ____movementInputs, ____observedBlock);
            return true; // Let SE2 run native UpdateRotationData with merged Kontrol input
        }

        // MODE 2: Direct Angular Flight (Direct Gyro Velocity)
        ApplyCurrentKontrolFrameDirect(__instance);
        return false; // Skip original smoothing/decay job when DirectAngularFlight is active
    }

    [HarmonyPatch(typeof(CockpitInputHandlerComponent), "UpdateRotationData")]
    [HarmonyPostfix]
    public static void UpdateRotationDataPostfix(
        ref float ____pitchAnalog, ref float ____yawAnalog,
        ref float ____lookUp, ref float ____lookDown, ref float ____lookLeft, ref float ____lookRight,
        ref MovementInputs ____movementInputs,
        NativeInputSnapshot __state)
    {
        if (SpaceEngineers2SettingsManager.Instance.IsNativeReticleSteering)
        {
            RestoreNativeInput(__state, ref ____pitchAnalog, ref ____yawAnalog, ref ____lookUp, ref ____lookDown,
                ref ____lookLeft, ref ____lookRight, ref ____movementInputs);
        }
    }

    [HarmonyPatch(typeof(CockpitInputHandlerComponent), "ComputeReticlePositioning")]
    [HarmonyPrefix]
    public static bool ComputeReticlePositioningPrefix(
        CockpitInputHandlerComponent __instance,
        ref float ____pitchAnalog,
        ref float ____yawAnalog,
        ref float ____lookUp,
        ref float ____lookDown,
        ref float ____lookLeft,
        ref float ____lookRight,
        ref MovementInputs ____movementInputs,
        object ____observedBlock,
        out NativeInputSnapshot __state
    )
    {
        __state = default;
        ApplyLiveSettings();
        var settings = SpaceEngineers2SettingsManager.Instance;

        // MODE 1: Native Reticle Steering
        if (settings.IsNativeReticleSteering)
        {
            __state = CaptureNativeInput(____pitchAnalog, ____yawAnalog, ____lookUp, ____lookDown,
                ____lookLeft, ____lookRight, ____movementInputs);
            ProcessNativeReticleOverride(__instance, ref ____pitchAnalog, ref ____yawAnalog, ref ____lookUp, ref ____lookDown,
                ref ____lookLeft, ref ____lookRight, ref ____movementInputs, ____observedBlock);
            return true; // Let SE2 run native ComputeReticlePositioning with merged Kontrol input
        }

        // MODE 2: Direct Angular Flight (Direct Gyro Velocity)
        ApplyCurrentKontrolFrameDirect(__instance);
        return false; // Skip original reticle integration job when DirectAngularFlight is active
    }

    [HarmonyPatch(typeof(CockpitInputHandlerComponent), "ComputeReticlePositioning")]
    [HarmonyPostfix]
    public static void ComputeReticlePositioningPostfix(
        ref float ____pitchAnalog, ref float ____yawAnalog,
        ref float ____lookUp, ref float ____lookDown, ref float ____lookLeft, ref float ____lookRight,
        ref MovementInputs ____movementInputs,
        NativeInputSnapshot __state)
    {
        if (SpaceEngineers2SettingsManager.Instance.IsNativeReticleSteering)
        {
            RestoreNativeInput(__state, ref ____pitchAnalog, ref ____yawAnalog, ref ____lookUp, ref ____lookDown,
                ref ____lookLeft, ref ____lookRight, ref ____movementInputs);
        }
    }

    public static unsafe bool UpdateControlDataPrefix(CockpitInputHandlerComponent __instance)
    {
        try
        {
            if (_committingControlData) return true;

            ApplyLiveSettings();
            var settings = SpaceEngineers2SettingsManager.Instance;

            if (!TryReadControlFrame(out var control)) return true;
            bool inputEnabled = control.IsInputEnabled != 0;
            var observedBlock = (CubeBlockComponent?)ObservedBlockField?.GetValue(__instance);
            ProcessTriggeredActions(__instance, inputEnabled ? control.TriggeredActions : 0, observedBlock);
            ActiveToolActionPatch.ApplyPrimaryFire(inputEnabled && (control.DiscreteStates & (1UL << 11)) != 0);
            ActiveToolActionPatch.ApplyReload(inputEnabled && (control.DiscreteStates & (1UL << 12)) != 0);

            // In Direct Angular Flight mode, skip native UpdateControlData to prevent it
            // from overwriting our AngularControlData and MovementInputs.
            // ApplyCurrentKontrolFrameDirect commits control data directly via
            // gridEntity.Data.UpdateControlData().
            if (settings.IsDirectAngularFlight && _wasKontrolActiveInCockpit)
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"UpdateControlDataPrefix error: {ex}");
            return true;
        }
    }

    public static unsafe void ProcessOverride(
        CockpitInputHandlerComponent instance,
        ref float pitchAnalog,
        ref float yawAnalog,
        ref float lookUp,
        ref float lookDown,
        ref float lookLeft,
        ref float lookRight,
        ref MovementInputs movementInputs,
        object observedBlock
    ) => ProcessNativeReticleOverride(instance, ref pitchAnalog, ref yawAnalog, ref lookUp, ref lookDown, ref lookLeft, ref lookRight, ref movementInputs, observedBlock);

    public static unsafe void ProcessNativeReticleOverride(
        CockpitInputHandlerComponent instance,
        ref float pitchAnalog,
        ref float yawAnalog,
        ref float lookUp,
        ref float lookDown,
        ref float lookLeft,
        ref float lookRight,
        ref MovementInputs movementInputs,
        object observedBlock
    )
    {
        try
        {
            EnsureChannels();

            if (!_cockpitHookObserved)
            {
                _cockpitHookObserved = true;
                SpaceEngineers2AdapterDiagnostics.Write("Kontrol input is available while piloting a cockpit.");
            }

            if (observedBlock != _lastObservedBlock)
            {
                _lastObservedBlock = observedBlock;
                CruiseControl.Reset();
                TranslationPresentationState.Reset();
                SpaceEngineers2AdapterDiagnostics.WriteDebug(observedBlock is null
                    ? "SE2 cleared the observed cockpit block."
                    : $"Player entered cockpit block ({observedBlock.GetType().Name}).");
            }

            if (!TryReadControlFrame(out var control))
            {
                TranslationPresentationState.Reset();
                return;
            }

            bool isInputEnabled = control.IsInputEnabled != 0;
            if (isInputEnabled != _lastOverrideActiveState)
            {
                _lastOverrideActiveState = isInputEnabled;
                SpaceEngineers2AdapterDiagnostics.WriteDebug($"Input override state changed to: {isInputEnabled}.");
            }

            if (!isInputEnabled)
            {
                TranslationPresentationState.Reset();
                if (_wasKontrolActiveInCockpit)
                {
                    NeutralizeCockpitInput(instance);
                }
                return;
            }

            _wasKontrolActiveInCockpit = true;

            try
            {
                bool currentTargetBased = TargetBasedGyroField?.GetValue(instance) is true;
                if (!currentTargetBased)
                {
                    SwitchGyroModeMethod?.Invoke(instance, [true]);
                    SpaceEngineers2AdapterDiagnostics.WriteDebug("Ensured cockpit gyro mode is target-based for Native Reticle Steering.");
                }
            }
            catch { }

            float pitch = NormalizeAxis(control.AnalogValues[0]);
            float roll = NormalizeAxis(control.AnalogValues[1]);
            float yaw = NormalizeAxis(control.AnalogValues[2]);
            float surge = NormalizeAxis(control.AnalogValues[3]);
            float sway = NormalizeAxis(control.AnalogValues[4]);
            float heave = NormalizeAxis(control.AnalogValues[5]);

            ProcessTriggeredActions(instance, control.TriggeredActions, observedBlock as CubeBlockComponent);
            ActiveToolActionPatch.ApplyPrimaryFire((control.DiscreteStates & (1UL << 11)) != 0);
            ActiveToolActionPatch.ApplyReload((control.DiscreteStates & (1UL << 12)) != 0);

            MergeTranslation(ref movementInputs, in control, surge, sway, heave, roll, instance, observedBlock as CubeBlockComponent);
            MergeRotationDirections(ref lookUp, ref lookDown, ref lookLeft, ref lookRight, pitch, yaw);

            CommitControlData(instance);

            WriteTelemetry(observedBlock as CubeBlockComponent);
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError("The Space Engineers 2 adapter encountered an input-processing error.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"ProcessNativeReticleOverride error: {ex}");
        }
    }

    public static unsafe bool ApplyCurrentKontrolFrameDirect(CockpitInputHandlerComponent instance)
    {
        try
        {
            EnsureChannels();

            if (!TryReadControlFrame(out var control) || control.IsInputEnabled == 0)
            {
                TranslationPresentationState.Reset();
                if (_wasKontrolActiveInCockpit)
                {
                    NeutralizeCockpitInput(instance);
                }
                return false;
            }

            if (!_cockpitHookObserved)
            {
                _cockpitHookObserved = true;
                SpaceEngineers2AdapterDiagnostics.Write("Kontrol input is available while piloting a cockpit.");
            }

            var observedBlock = (CubeBlockComponent?)ObservedBlockField?.GetValue(instance);
            if (observedBlock != _lastObservedBlock)
            {
                _lastObservedBlock = observedBlock;
                CruiseControl.Reset();
                TranslationPresentationState.Reset();
                SpaceEngineers2AdapterDiagnostics.WriteDebug(observedBlock is null
                    ? "SE2 cleared the observed cockpit block."
                    : $"Player entered cockpit block ({observedBlock.GetType().Name}).");
            }

            var cockpitComponent = (CockpitComponent?)CockpitComponentField?.GetValue(instance);
            bool currentTargetBased = TargetBasedGyroField?.GetValue(instance) is true;

            if (!_wasKontrolActiveInCockpit || currentTargetBased)
            {
                _originalDesiredTargetBasedGyro = cockpitComponent?.TargetBasedGyro ?? true;
                SwitchGyroModeMethod?.Invoke(instance, [false]);
                _wasKontrolActiveInCockpit = true;
                SpaceEngineers2AdapterDiagnostics.WriteDebug($"Switched cockpit gyro mode to angular (saved desired target-based={_originalDesiredTargetBasedGyro}).");
            }

            float pitch = NormalizeAxis(control.AnalogValues[0]);
            float roll = NormalizeAxis(control.AnalogValues[1]);
            float yaw = NormalizeAxis(control.AnalogValues[2]);
            float surge = NormalizeAxis(control.AnalogValues[3]);
            float sway = NormalizeAxis(control.AnalogValues[4]);
            float heave = NormalizeAxis(control.AnalogValues[5]);

            ProcessTriggeredActions(instance, control.TriggeredActions, observedBlock);
            ActiveToolActionPatch.ApplyPrimaryFire((control.DiscreteStates & (1UL << 11)) != 0);
            ActiveToolActionPatch.ApplyReload((control.DiscreteStates & (1UL << 12)) != 0);

            var observerChild = (ChildTransformComponent?)ObserverChildTransformField?.GetValue(instance);
            var gridEntity = observedBlock?.Grid?.Entity;

            if (gridEntity != null && observerChild != null)
            {
                var observerOrientation = observerChild.Data.Get<RelativeTransform>().Orientation;
                TranslationPresentationState.Set(gridEntity.DEntity, observerOrientation, surge, sway, heave);

                // Read SE2's native keyboard/mouse fields so they work alongside the joystick
                var nativeMovement = (MovementInputs?)MovementInputsField?.GetValue(instance) ?? default;
                float nativePitchAnalog = (float?)PitchAnalogField?.GetValue(instance) ?? 0f;
                float nativeYawAnalog = (float?)YawAnalogField?.GetValue(instance) ?? 0f;
                float nativeLookUp = (float?)LookUpField?.GetValue(instance) ?? 0f;
                float nativeLookDown = (float?)LookDownField?.GetValue(instance) ?? 0f;
                float nativeLookLeft = (float?)LookLeftField?.GetValue(instance) ?? 0f;
                float nativeLookRight = (float?)LookRightField?.GetValue(instance) ?? 0f;

                var settings = SpaceEngineers2SettingsManager.Instance;
                var (fwd, back, right, left, up, down) = ComputeTranslationThrust(
                    settings, surge, sway, heave, instance, observedBlock, out var maximumTargetSpeed);
                WriteTranslationTrace(
                    $"DirectAngularFlight/{settings.TranslationControlMode}", in control, surge, sway, heave,
                    fwd, back, right, left, up, down, instance, observedBlock, maximumTargetSpeed);

                // Build translation from Kontrol IPC, then merge native keyboard values via Math.Max
                var movementInputs = new MovementInputs
                {
                    Forward = Math.Max(fwd, nativeMovement.Forward),
                    Backward = Math.Max(back, nativeMovement.Backward),
                    Right = Math.Max(right, nativeMovement.Right),
                    Left = Math.Max(left, nativeMovement.Left),
                    Up = Math.Max(up, nativeMovement.Up),
                    Down = Math.Max(down, nativeMovement.Down),
                    RollRight = Math.Max(Math.Max(roll, 0f), nativeMovement.RollRight),
                    RollLeft = Math.Max(Math.Max(-roll, 0f), nativeMovement.RollLeft),
                    Pitch = pitch,
                    Yaw = yaw
                };
                if (settings.IsVelocityHoldTranslation)
                {
                    MergeVelocityHoldTranslation(ref movementInputs, fwd, back, right, left, up, down);
                }

                // Commit translation directly to grid entity without polluting instance._movementInputs
                // (which would make nativeMovement sticky across frames).
                gridEntity.Data.UpdateControlData(in movementInputs, new Quaternion?(observerOrientation), isAngular: true);

                // Angular velocity target computed and set LAST so it overrides UpdateControlData's angular output
                float maxRate = settings.DirectAngularMaxRate;
                float accelRate = settings.DirectAngularAcceleration;
                float decelRate = settings.DirectAngularDeceleration;

                float shapedPitch = ShapeResponse(pitch, exponent: 2.0f);
                float shapedRoll = ShapeResponse(roll, exponent: 2.0f);
                float shapedYaw = ShapeResponse(yaw, exponent: 2.0f);

                // Joystick angular velocity target
                Vector3 targetCockpitAngular = new Vector3(-shapedPitch, -shapedYaw, -shapedRoll) * maxRate;

                // Merge native mouse/keyboard look into angular velocity target
                // SE2's look directions: lookDown/lookUp = pitch, lookRight/lookLeft = yaw
                // pitchAnalog/yawAnalog = mouse analog contribution
                float nativePitchDir = NormalizeAxis(nativeLookDown - nativeLookUp);
                float nativeYawDir = NormalizeAxis(nativeLookRight - nativeLookLeft);
                float nativePitch = NormalizeAxis(nativePitchAnalog + nativePitchDir);
                float nativeYaw = NormalizeAxis(nativeYawAnalog + nativeYawDir);
                float nativeRoll = NormalizeAxis(nativeMovement.RollRight - nativeMovement.RollLeft);

                // Blend: pick the dominant contribution per axis (joystick vs native)
                if (MathF.Abs(nativePitch) > MathF.Abs(shapedPitch))
                    targetCockpitAngular.X = -nativePitch * maxRate;
                if (MathF.Abs(nativeYaw) > MathF.Abs(shapedYaw))
                    targetCockpitAngular.Y = -nativeYaw * maxRate;
                if (MathF.Abs(nativeRoll) > MathF.Abs(shapedRoll))
                    targetCockpitAngular.Z = -nativeRoll * maxRate;

                float dt = 1f / 60f;
                _currentCockpitAngularVelocity.X = SlewAxis(_currentCockpitAngularVelocity.X, targetCockpitAngular.X, dt, accelRate, decelRate);
                _currentCockpitAngularVelocity.Y = SlewAxis(_currentCockpitAngularVelocity.Y, targetCockpitAngular.Y, dt, accelRate, decelRate);
                _currentCockpitAngularVelocity.Z = SlewAxis(_currentCockpitAngularVelocity.Z, targetCockpitAngular.Z, dt, accelRate, decelRate);

                var cockpitOrientation = observedBlock?.Data.GetRelativeTransform().Orientation ?? Quaternion.Identity;
                Vector3 gridAngular = cockpitOrientation * _currentCockpitAngularVelocity;

                // Set AFTER UpdateControlData so our angular velocity persists
                gridEntity.Data.Set(new AngularControlData
                {
                    TargetAngularVelocity = gridAngular
                });
            }
            else
            {
                TranslationPresentationState.Reset();
            }

            WriteTelemetry(observedBlock);
            return true;
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError("Error in ApplyCurrentKontrolFrameDirect.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"ApplyCurrentKontrolFrameDirect exception: {ex}");
            return false;
        }
    }

    public static void NeutralizeCockpitInput(CockpitInputHandlerComponent instance)
    {
        try
        {
            CruiseControl.Reset();
            TranslationPresentationState.Reset();
            if (!_wasKontrolActiveInCockpit) return;
            _wasKontrolActiveInCockpit = false;

            var observedBlock = (CubeBlockComponent?)ObservedBlockField?.GetValue(instance);
            var gridEntity = observedBlock?.Grid?.Entity;
            if (gridEntity != null)
            {
                var zeroInputs = default(MovementInputs);
                gridEntity.Data.UpdateControlData(in zeroInputs, null, isAngular: false);
                gridEntity.Data.Set(new AngularControlData { TargetAngularVelocity = Vector3.Zero });
            }

            _currentCockpitAngularVelocity = Vector3.Zero;
            ActiveToolActionPatch.ApplyPrimaryFire(false);
            ActiveToolActionPatch.ApplyReload(false);

            // A transient disabled-input frame (for example while an action such
            // as ToggleDampeners is delivered) must not change the user's
            // selected flight model. Direct Angular Flight owns the angular
            // gyro path, so keep target-based gyro disabled until that setting
            // is explicitly changed. Native Reticle Steering restores the
            // cockpit's pre-Kontrol preference.
            bool targetBasedGyro = ResolveGyroModeAfterNeutralization(
                SpaceEngineers2SettingsManager.Instance.IsDirectAngularFlight,
                _originalDesiredTargetBasedGyro);
            SwitchGyroModeMethod?.Invoke(instance, [targetBasedGyro]);
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"Restored cockpit gyro mode to: {targetBasedGyro}.");
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"NeutralizeCockpitInput error: {ex}");
        }
    }

    internal static bool ResolveGyroModeAfterNeutralization(
        bool isDirectAngularFlight, bool originalDesiredTargetBasedGyro) =>
        isDirectAngularFlight ? false : originalDesiredTargetBasedGyro;

    private static unsafe void MergeTranslation(
        ref MovementInputs movementInputs, in InputFrame control, float surge, float sway, float heave, float roll,
        CockpitInputHandlerComponent? instance = null, CubeBlockComponent? observedBlock = null)
    {
        var settings = SpaceEngineers2SettingsManager.Instance;
        var observerChild = (ChildTransformComponent?)ObserverChildTransformField?.GetValue(instance);
        var gridEntity = observedBlock?.Grid?.Entity;
        var observerOrientation = observerChild?.Data.Get<RelativeTransform>().Orientation
            ?? observedBlock?.Data.GetRelativeTransform().Orientation;
        if (gridEntity is not null && observerOrientation is { } orientation)
        {
            TranslationPresentationState.Set(gridEntity.DEntity, orientation, surge, sway, heave);
        }
        else
        {
            TranslationPresentationState.Reset();
        }
        var (fwd, back, right, left, up, down) = ComputeTranslationThrust(
            settings, surge, sway, heave, instance, observedBlock, out var maximumTargetSpeed);
        WriteTranslationTrace(
            $"NativeReticleSteering/{settings.TranslationControlMode}", in control, surge, sway, heave,
            fwd, back, right, left, up, down, instance, observedBlock, maximumTargetSpeed);
        movementInputs.Forward = Math.Max(movementInputs.Forward, fwd);
        movementInputs.Backward = Math.Max(movementInputs.Backward, back);
        movementInputs.Right = Math.Max(movementInputs.Right, right);
        movementInputs.Left = Math.Max(movementInputs.Left, left);
        movementInputs.Up = Math.Max(movementInputs.Up, up);
        movementInputs.Down = Math.Max(movementInputs.Down, down);
        if (settings.IsVelocityHoldTranslation)
        {
            MergeVelocityHoldTranslation(ref movementInputs, fwd, back, right, left, up, down);
        }
        movementInputs.RollRight = Math.Max(movementInputs.RollRight, Math.Max(roll, 0f));
        movementInputs.RollLeft = Math.Max(movementInputs.RollLeft, Math.Max(-roll, 0f));
    }

    private static unsafe void WriteTranslationTrace(
        string flightMode, in InputFrame control, float surge, float sway, float heave,
        float forward, float backward, float right, float left, float up, float down,
        CockpitInputHandlerComponent? instance, CubeBlockComponent? observedBlock, float maximumTargetSpeed = 0f)
    {
        long now = Environment.TickCount64;
        if (now < _nextTranslationTraceTick) return;
        _nextTranslationTraceTick = now + 250;

        float currentSurge = 0f, currentSway = 0f, currentHeave = 0f;
        TryGetLocalVelocity(instance, observedBlock, out currentSurge, out currentSway, out currentHeave);

        var gridEntity = observedBlock?.Grid?.Entity;
        bool dampenersEnabled = gridEntity?.Data.Has<DampeningData>() == true;
        string presentationTrace = gridEntity is not null && TranslationPresentationState.TryGet(gridEntity.DEntity, out var presentation)
            ? $"grid={gridEntity.DEntity.GetHashCode()}; presentation=({presentation.VoluntaryThrust.X:F2},{presentation.VoluntaryThrust.Y:F2},{presentation.VoluntaryThrust.Z:F2})"
            : "presentation=unavailable";
        SpaceEngineers2AdapterDiagnostics.WriteDebug(
            $"[VelocityHoldTrace] mode={flightMode}; damp={dampenersEnabled}; axis=({surge:F2},{sway:F2},{heave:F2}); v=({currentSurge:F1},{currentSway:F1},{currentHeave:F1}); target=({surge * maximumTargetSpeed:F1},{sway * maximumTargetSpeed:F1},{heave * maximumTargetSpeed:F1}); cmd=(F{forward:F2}/B{backward:F2},R{right:F2}/L{left:F2},U{up:F2}/D{down:F2}); vmax={maximumTargetSpeed:F1}; cruise={CruiseControl.IsActive}; {presentationTrace}.");
    }

    internal static (float fwd, float back, float right, float left, float up, float down) ComputeProportionalThrust(
        float surge, float sway, float heave)
    {
        surge = NormalizeAxis(surge);
        sway = NormalizeAxis(sway);
        heave = NormalizeAxis(heave);

        return (
            Math.Max(surge, 0f), Math.Max(-surge, 0f),
            Math.Max(sway, 0f), Math.Max(-sway, 0f),
            Math.Max(heave, 0f), Math.Max(-heave, 0f));
    }

    internal static (float fwd, float back, float right, float left, float up, float down) ComputeVelocityHoldThrust(
        float surge, float sway, float heave,
        float actualSurge, float actualSway, float actualHeave,
        float maximumTargetSpeedMetersPerSecond,
        float responseGain = 1f) =>
        TranslationVelocityController.ComputeVelocityHoldThrust(
            surge, sway, heave, actualSurge, actualSway, actualHeave, maximumTargetSpeedMetersPerSecond, responseGain);

    internal static (float fwd, float back, float right, float left, float up, float down) ComputeCruiseVelocityHoldThrust(
        float surge, float sway, float heave,
        float actualSurge, float actualSway, float actualHeave,
        float maximumTargetSpeedMetersPerSecond) =>
        TranslationVelocityController.ComputeCruiseVelocityHoldThrust(
            surge, sway, heave, actualSurge, actualSway, actualHeave, maximumTargetSpeedMetersPerSecond);

    private static void MergeVelocityHoldTranslation(
        ref MovementInputs movementInputs,
        float forward, float backward, float right, float left, float up, float down)
    {
        MergeVelocityHoldAxis(ref movementInputs.Forward, ref movementInputs.Backward, forward, backward);
        MergeVelocityHoldAxis(ref movementInputs.Right, ref movementInputs.Left, right, left);
        MergeVelocityHoldAxis(ref movementInputs.Up, ref movementInputs.Down, up, down);
    }

    private static void MergeVelocityHoldAxis(ref float positive, ref float negative, float controlledPositive, float controlledNegative)
    {
        if (controlledPositive > 0f)
        {
            positive = Math.Max(positive, controlledPositive);
            negative = 0f;
            return;
        }

        if (controlledNegative > 0f)
        {
            positive = 0f;
            negative = Math.Max(negative, controlledNegative);
            return;
        }

        // The controller has reached its target. Preserve a native translation
        // request, but collapse opposite key inputs to one signed command.
        float merged = positive - negative;
        positive = Math.Max(merged, 0f);
        negative = Math.Max(-merged, 0f);
    }

    private static (float fwd, float back, float right, float left, float up, float down) ComputeTranslationThrust(
        SpaceEngineers2SettingsManager settings,
        float surge, float sway, float heave,
        CockpitInputHandlerComponent? instance, CubeBlockComponent? observedBlock,
        out float maximumTargetSpeed)
    {
        if (CruiseControl.IsActive && surge < -CruiseThrottleDeadband)
        {
            CruiseControl.CancelForBrake();
            SpaceEngineers2AdapterDiagnostics.WriteDebug("Cruise Control cancelled by reverse/brake throttle input.");
        }

        if (CruiseControl.IsActive && MathF.Abs(surge) <= CruiseThrottleDeadband &&
            TryGetLocalVelocity(instance, observedBlock, out var cruiseSurge, out _, out _))
        {
            maximumTargetSpeed = ResolveVelocityHoldMaximumSpeed(instance, settings.VelocityHoldMaxTargetSpeed);
            float forward = TranslationVelocityController.ComputeMinimumForwardSpeedThrust(
                CruiseControl.TargetSpeedMetersPerSecond, cruiseSurge, maximumTargetSpeed);
            return (forward, 0f, 0f, 0f, 0f, 0f);
        }

        if (CruiseControl.IsActive && surge > CruiseThrottleDeadband)
        {
            if (!settings.IsVelocityHoldTranslation)
            {
                maximumTargetSpeed = 0f;
                return ComputeProportionalThrust(surge, sway, heave);
            }

            maximumTargetSpeed = ResolveVelocityHoldMaximumSpeed(instance, settings.VelocityHoldMaxTargetSpeed);
            if (!TryGetLocalVelocity(instance, observedBlock, out var overrideSurge, out var overrideSway, out var overrideHeave))
            {
                return ComputeProportionalThrust(surge, sway, heave);
            }

            float cruiseAxis = TranslationVelocityController.ComputeCruiseForwardVelocityHoldAxis(
                surge, CruiseControl.TargetSpeedMetersPerSecond, maximumTargetSpeed);
            return ComputeCruiseVelocityHoldThrust(
                cruiseAxis, sway, heave, overrideSurge, overrideSway, overrideHeave, maximumTargetSpeed);
        }

        if (!settings.IsVelocityHoldTranslation)
        {
            maximumTargetSpeed = 0f;
            return ComputeProportionalThrust(surge, sway, heave);
        }

        maximumTargetSpeed = ResolveVelocityHoldMaximumSpeed(instance, settings.VelocityHoldMaxTargetSpeed);
        if (!TryGetLocalVelocity(instance, observedBlock, out var actualSurge, out var actualSway, out var actualHeave))
        {
            // A velocity target without a cockpit-frame measurement is unsafe. Keep
            // current direct behavior until SE2 provides the rigid body and observer.
            return ComputeProportionalThrust(surge, sway, heave);
        }

        return ComputeVelocityHoldThrust(
            surge, sway, heave, actualSurge, actualSway, actualHeave, maximumTargetSpeed, settings.VelocityHoldResponseGain);
    }

    private static bool TryGetLocalVelocity(
        CockpitInputHandlerComponent? instance, CubeBlockComponent? observedBlock,
        out float surge, out float sway, out float heave)
    {
        surge = 0f;
        sway = 0f;
        heave = 0f;
        var gridEntity = observedBlock?.Grid?.Entity;
        if (gridEntity?.Data.Has<Keen.VRage.Physics.Data.RigidBodyData>() != true) return false;

        var observerChild = (ChildTransformComponent?)ObserverChildTransformField?.GetValue(instance);
        var observerOrientation = observerChild?.Data.Get<RelativeTransform>().Orientation
            ?? observedBlock?.Data.GetRelativeTransform().Orientation
            ?? Quaternion.Identity;
        var gridWorldOrientation = gridEntity.Data.GetWorldTransform().Orientation;
        (surge, sway, heave) = ComputeLocalTranslationVelocity(
            gridWorldOrientation,
            observerOrientation,
            gridEntity.Data.Get<Keen.VRage.Physics.Data.RigidBodyData>().LinearVelocity);
        return float.IsFinite(surge) && float.IsFinite(sway) && float.IsFinite(heave);
    }

    internal static (float surge, float sway, float heave) ComputeLocalTranslationVelocity(
        Quaternion gridWorldOrientation, Quaternion observerOrientation, Vector3 worldVelocity)
    {
        // SE2 rotates cockpit/observer movement into grid-local space before
        // applying it. RigidBodyData reports world velocity, so undo both
        // transforms in reverse order to compare velocity in the input frame.
        Vector3 gridVelocity = Quaternion.Inverse(gridWorldOrientation) * worldVelocity;
        Vector3 inputVelocity = Quaternion.Inverse(observerOrientation) * gridVelocity;
        return (-inputVelocity.Z, inputVelocity.X, inputVelocity.Y);
    }

    private static float ResolveVelocityHoldMaximumSpeed(CockpitInputHandlerComponent? instance, float configuredMaximumSpeed)
    {
        SoftSpeedLimitData? softLimit = null;
        var observedBlock = (CubeBlockComponent?)ObservedBlockField?.GetValue(instance);
        var gridEntity = observedBlock?.Grid?.Entity;
        if (gridEntity?.Data.Has<SoftSpeedLimitData>() == true)
        {
            softLimit = gridEntity.Data.Get<SoftSpeedLimitData>();
        }

        object? velocityLimits = instance is null ? null : VelocityLimitsField?.GetValue(instance);
        return ResolveVelocityHoldMaximumSpeed(softLimit, velocityLimits, configuredMaximumSpeed);
    }

    internal static float ResolveVelocityHoldMaximumSpeed(
        SoftSpeedLimitData? softLimit, object? velocityLimits, float configuredMaximumSpeed)
    {
        bool hasConfiguredCap = float.IsFinite(configuredMaximumSpeed) && configuredMaximumSpeed > 0f;
        float configuredCap = hasConfiguredCap ? configuredMaximumSpeed : float.PositiveInfinity;
        if (softLimit is { Speed: > 0f } activeSoftLimit && float.IsFinite(activeSoftLimit.Speed))
        {
            return Math.Min(configuredCap, activeSoftLimit.Speed);
        }

        try
        {
            if (TryReadVelocityLimit(velocityLimits, out float gameMaximumSpeed))
            {
                return Math.Min(configuredCap, gameMaximumSpeed);
            }
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"Could not read SE2 velocity limits; using configured Velocity Hold limit. {ex.Message}");
        }

        return hasConfiguredCap
            ? configuredCap
            : TranslationVelocityController.DefaultMaximumTargetSpeedMetersPerSecond;
    }

    private static bool TryReadVelocityLimit(object? limits, out float maximumSpeed)
    {
        maximumSpeed = 0f;
        if (limits is null) return false;
        if (limits is float single && float.IsFinite(single) && single > 0f)
        {
            maximumSpeed = single;
            return true;
        }

        Type type = limits.GetType();
        foreach (string memberName in new[] { "LinearVelocityLimit", "LinearVelocity", "MaxLinearVelocity", "MaximumLinearVelocity", "MaxSpeed" })
        {
            object? value = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(limits)
                ?? type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(limits);
            if (value is float candidate && float.IsFinite(candidate) && candidate > 0f)
            {
                maximumSpeed = candidate;
                return true;
            }
        }

        return false;
    }

    private static void MergeRotationDirections(
        ref float lookUp, ref float lookDown, ref float lookLeft, ref float lookRight,
        float pitch, float yaw)
    {
        pitch = NormalizeAxis(pitch);
        yaw = NormalizeAxis(yaw);
        // UpdateControlData calculates Pitch = analog - (Up - Down) * digital
        // and Yaw = analog + (Right - Left) * digital. These mappings preserve
        // the old Kontrol signs while using SE2's key-equivalent path.
        lookDown = Math.Max(lookDown, Math.Max(pitch, 0f));
        lookUp = Math.Max(lookUp, Math.Max(-pitch, 0f));
        lookRight = Math.Max(lookRight, Math.Max(yaw, 0f));
        lookLeft = Math.Max(lookLeft, Math.Max(-yaw, 0f));
    }

    private static NativeInputSnapshot CaptureNativeInput(
        float pitchAnalog, float yawAnalog, float lookUp, float lookDown, float lookLeft, float lookRight,
        MovementInputs movementInputs) =>
        new(pitchAnalog, yawAnalog, lookUp, lookDown, lookLeft, lookRight, movementInputs);

    private static void RestoreNativeInput(
        NativeInputSnapshot snapshot,
        ref float pitchAnalog, ref float yawAnalog, ref float lookUp, ref float lookDown, ref float lookLeft, ref float lookRight,
        ref MovementInputs movementInputs)
    {
        pitchAnalog = snapshot.PitchAnalog;
        yawAnalog = snapshot.YawAnalog;
        lookUp = snapshot.LookUp;
        lookDown = snapshot.LookDown;
        lookLeft = snapshot.LookLeft;
        lookRight = snapshot.LookRight;
        movementInputs = snapshot.MovementInputs;
    }

    private static void CommitControlData(CockpitInputHandlerComponent instance)
    {
        if (_committingControlData) return;
        var method = UpdateControlDataMethod;
        if (method is null)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError("Kontrol cannot submit cockpit input because SE2's UpdateControlData method was not found.");
            return;
        }

        try
        {
            _committingControlData = true;
            method.Invoke(instance, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError("Kontrol could not submit the cockpit input state to SE2.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"SE2 movement commit error: {ex.InnerException}");
        }
        catch (Exception ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteError("Kontrol could not submit the cockpit input state to SE2.");
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"SE2 movement commit error: {ex}");
        }
        finally
        {
            _committingControlData = false;
        }
    }

    private static float ShapeResponse(float value, float exponent = 2.0f)
    {
        if (!float.IsFinite(value) || value == 0f) return 0f;
        float clamped = Math.Clamp(value, -1f, 1f);
        return MathF.Sign(clamped) * MathF.Pow(MathF.Abs(clamped), exponent);
    }

    private static float SlewAxis(float current, float target, float dt, float accelRate, float decelRate)
    {
        float rate = MathF.Abs(target) >= MathF.Abs(current) && MathF.Sign(target) == MathF.Sign(current)
            ? accelRate
            : decelRate;

        float maxDelta = rate * dt;
        float delta = target - current;
        if (MathF.Abs(delta) <= maxDelta)
        {
            return target;
        }
        return current + MathF.Sign(delta) * maxDelta;
    }

    private static float NormalizeAxis(float value) =>
        float.IsFinite(value) ? Math.Clamp(value, -1f, 1f) : 0f;

    private static void ApplyLiveSettings()
    {
        EnsureChannels();
        SettingsChannel.Read(out var packet);
        var json = packet.GetJson();
        if (string.IsNullOrWhiteSpace(json) || string.Equals(json, _lastSettingsJson, StringComparison.Ordinal)) return;
        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
            if (values is null) return;
            var prevMode = SpaceEngineers2SettingsManager.Instance.FlightModelMode;
            SpaceEngineers2SettingsManager.Instance.ApplySettings(values, (ulong)DateTime.UtcNow.Ticks);
            _lastSettingsJson = json;
            var newMode = SpaceEngineers2SettingsManager.Instance.FlightModelMode;
            if (!string.Equals(prevMode, newMode, StringComparison.OrdinalIgnoreCase))
            {
                SpaceEngineers2AdapterDiagnostics.Write($"Flight model switched: {prevMode} -> {newMode}");
                _currentCockpitAngularVelocity = Vector3.Zero;
            }
        }
        catch (JsonException ex)
        {
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"Ignoring invalid adapter settings snapshot: {ex.Message}");
        }
    }

    private static void WriteTelemetry(CubeBlockComponent? observedBlock)
    {
        if (observedBlock is null)
        {
            if (!_missingObservedBlockReported)
            {
                _missingObservedBlockReported = true;
                SpaceEngineers2AdapterDiagnostics.WriteDebug("Cockpit input handler ran before SE2 supplied an observed block; skipped cockpit telemetry.");
            }
            return;
        }
        _missingObservedBlockReported = false;

        var entity = observedBlock.Grid?.Entity;
        if (entity is null) return;

        var telemetryDict = new Dictionary<string, string>
        {
            { "Status", "In Cockpit" },
            { "Tick Count", Environment.TickCount.ToString() }
        };

        if (entity.Data.Has<Keen.VRage.Physics.Data.RigidBodyMassProperties>())
        {
            var massProps = entity.Data.Get<Keen.VRage.Physics.Data.RigidBodyMassProperties>();
            telemetryDict["Ship Mass"] = $"{massProps.Mass:N0} kg";
        }

        if (entity.Data.Has<Keen.VRage.Physics.Data.RigidBodyData>())
        {
            var rigidBody = entity.Data.Get<Keen.VRage.Physics.Data.RigidBodyData>();
            var linVel = rigidBody.LinearVelocity;
            var angVel = rigidBody.AngularVelocity;

            float linSpeed = (float)Math.Sqrt(linVel.X * linVel.X + linVel.Y * linVel.Y + linVel.Z * linVel.Z);
            float angSpeed = (float)Math.Sqrt(angVel.X * angVel.X + angVel.Y * angVel.Y + angVel.Z * angVel.Z);

            telemetryDict["Linear Speed"] = $"{linSpeed:F1} m/s";
            telemetryDict["Angular Speed"] = $"{angSpeed:F2} rad/s";
        }

        bool damp = entity.Data.Has<DampeningData>();
        telemetryDict["Dampeners"] = damp ? "Enabled" : "Disabled";

        var telemetry = new TelemetryData();
        string json = System.Text.Json.JsonSerializer.Serialize(telemetryDict);
        telemetry.SetJson(json);
        TelemetryChannel.Write(ref telemetry);
    }

    private static void ProcessTriggeredActions(object instance, ulong triggeredActions, CubeBlockComponent? observedBlock)
    {
        ulong newActions = triggeredActions & ~_previousTriggeredActions;
        _previousTriggeredActions = triggeredActions;
        if (newActions == 0) return;

        ProcessCruiseControlActions(newActions, instance as CockpitInputHandlerComponent, observedBlock);

        SpaceEngineers2AdapterDiagnostics.WriteDebug($"Received Kontrol vehicle-system action bits: 0x{newActions:X}.");
        CameraActionPatch.ProcessCameraModeSwitch(newActions);
        foreach (var (bit, methodName) in TriggerActions)
        {
            if ((newActions & (1UL << bit)) == 0) continue;
            var methodKey = (instance.GetType(), methodName);
            if (!TriggerMethods.TryGetValue(methodKey, out var method))
            {
                method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
                TriggerMethods[methodKey] = method;
            }
            if (method is null)
            {
                SpaceEngineers2AdapterDiagnostics.WriteError($"Kontrol received action '{methodName}', but SE2 does not expose the expected control method.");
                continue;
            }

            try
            {
                InvokeButtonAction(instance, method, true);
                SpaceEngineers2AdapterDiagnostics.WriteDebug($"Kontrol invoked SE2 cockpit action '{methodName}'.");
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                SpaceEngineers2AdapterDiagnostics.WriteError($"SE2 rejected Kontrol cockpit action '{methodName}'.");
                SpaceEngineers2AdapterDiagnostics.WriteDebug($"SE2 cockpit action '{methodName}' error: {ex.InnerException}");
            }
            catch (Exception ex)
            {
                SpaceEngineers2AdapterDiagnostics.WriteError($"Kontrol could not invoke SE2 cockpit action '{methodName}'.");
                SpaceEngineers2AdapterDiagnostics.WriteDebug($"SE2 cockpit action '{methodName}' error: {ex}");
            }
        }
    }

    private static void ProcessCruiseControlActions(
        ulong newActions, CockpitInputHandlerComponent? instance, CubeBlockComponent? observedBlock)
    {
        if ((newActions & (1UL << CruiseSetActionBit)) != 0)
        {
            if (!TryGetLocalVelocity(instance, observedBlock, out var currentSurge, out _, out _))
            {
                SpaceEngineers2AdapterDiagnostics.WriteDebug("Cruise Control Set ignored because current forward speed is unavailable.");
            }
            else
            {
                CruiseSetResult result = CruiseControl.SetOrReset(currentSurge, Environment.TickCount64);
                SpaceEngineers2AdapterDiagnostics.WriteDebug(result is CruiseSetResult.Set
                    ? $"Cruise Control set to {CruiseControl.TargetSpeedMetersPerSecond:F2} m/s."
                    : "Cruise Control reset by double-click.");
            }
        }

        if ((newActions & (1UL << CruiseIncreaseActionBit)) != 0)
        {
            CruiseControl.IncreaseTarget();
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"Cruise Control target increased to {CruiseControl.TargetSpeedMetersPerSecond:F2} m/s.");
        }

        if ((newActions & (1UL << CruiseDecreaseActionBit)) != 0)
        {
            CruiseControl.DecreaseTarget();
            SpaceEngineers2AdapterDiagnostics.WriteDebug($"Cruise Control target decreased to {CruiseControl.TargetSpeedMetersPerSecond:F2} m/s.");
        }
    }

    private static void InvokeButtonAction(object instance, MethodInfo method, bool value)
    {
        method.Invoke(instance, CreateButtonActionArguments(method, value));
    }

    private static object[] CreateButtonActionArguments(MethodInfo method, bool value)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != 2 || parameters[0].ParameterType != typeof(bool) ||
            parameters[1].ParameterType.FullName != "Keen.VRage.Input.ControlActivation" ||
            !parameters[1].ParameterType.IsEnum)
        {
            throw new MissingMethodException(
                $"SE2 action '{method.Name}' has an unsupported signature ({string.Join(", ", parameters.Select(parameter => parameter.ParameterType.Name))}).");
        }

        return
        [
            value,
            Enum.ToObject(parameters[1].ParameterType, value ? 0 : 2)
        ];
    }
}
