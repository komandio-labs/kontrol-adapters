using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Sandbox.Game;
using Kontrol.Sdk.Diagnostics;
using Kontrol.Sdk.IPC;
using Sandbox.Engine.Utils;
using Sandbox.Game.Gui;
using Sandbox.Game.VoiceChat;
using MyShipController = Sandbox.Game.Entities.MyShipController;
using SpaceEngineersControllableEntity = Sandbox.Game.Entities.IMyControllableEntity;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Utils;
using VRageMath;

namespace Kontrol.Adapters.SpaceEngineers.Plugin
{
    public sealed class SpaceEngineersPlugin : VRage.Plugins.IPlugin, IDisposable
    {
        private const string InputMapName = @"Local\Kontrol_Input_space-engineers";
        private const string GamePlayScreenTypeName = "Sandbox.Game.Gui.MyGuiScreenGamePlay";
        private const string ChatScreenTypeName = "Sandbox.Game.Gui.MyGuiScreenChat";
        private const string GuiSandboxTypeName = "Sandbox.Graphics.GUI.MyGuiSandbox";
        private static readonly FieldInfo GamePlayStaticField = AccessTools.Field(GamePlayScreenTypeName + ":Static");
        private static readonly MethodInfo SwitchCameraMethod = AccessTools.Method(GamePlayScreenTypeName + ":SwitchCamera");
        private static readonly Type ChatScreenType = AccessTools.TypeByName(ChatScreenTypeName);
        private static readonly Type GuiSandboxType = AccessTools.TypeByName(GuiSandboxTypeName);
        private static readonly FieldInfo ChatScreenStaticField = ChatScreenType == null ? null : AccessTools.Field(ChatScreenType, "Static");
        private static readonly MethodInfo GuiSandboxAddScreenMethod = GuiSandboxType == null
            ? null
            : GuiSandboxType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(method => method.Name == "AddScreen" && method.GetParameters().Length == 1);
        private readonly MmfChannel<InputFrame> _input = new MmfChannel<InputFrame>(InputMapName);
        private readonly LegacyFlightSettingsReader _settings = new LegacyFlightSettingsReader();
        private readonly AdapterConnectionReporter _status = new AdapterConnectionReporter("space-engineers");
        private readonly AdapterLogReporter _logs = new AdapterLogReporter("space-engineers");
        private IMyControllableEntity _lastControlled;
        private MyShipController _weaponController;
        private ulong _previousActions;
        private bool _primaryWeaponActive;
        private bool _secondaryWeaponActive;
        private bool _voiceChatActive;
        private DateTime _lastInputTraceUtc;
        private DateTime _lastHookTraceUtc;
        private DateTime _lastCameraTraceUtc;
        private DateTime _lastZoomInTraceUtc;
        private DateTime _lastZoomOutTraceUtc;
        private DateTime _lastCameraLookUtc;

        public void Init(object gameInstance)
        {
            PulsarStartupTrace.Write("Plugin Init entered.");
            try
            {
                PulsarStartupTrace.Write("Plugin Init opening input channel.");
                _input.CreateOrOpen();
                PulsarStartupTrace.Write("Plugin Init input channel ready; reporting Loaded status.");
                _status.ReportLoaded();
                PulsarStartupTrace.Write("Plugin Init status channel ready; installing final ship-control hook.");
                ShipControlCommitHook.Install(this);
                PulsarStartupTrace.Write("Plugin Init final ship-control hook installed.");
                _logs.Write("[LifecycleTrace] Kontrol Joystick / HOTAS / HOSAS ship-control hook initialized.");
                _status.Pulse();
                PulsarStartupTrace.Write("Plugin Init completed.");
            }
            catch (Exception exception)
            {
                PulsarStartupTrace.Write("Plugin Init failed: " + exception);
                _logs.WriteError("[LifecycleTrace] Could not install the final ship-control hook: " + exception);
                _status.ReportError(
                    "Space Engineers control hook unavailable",
                    "Kontrol could not initialize its Space Engineers controls plugin.",
                    "Check the Pulsar startup trace, then restart Pulsar Legacy and the game.");
            }
        }

        public void Update()
        {
            try
            {
                InputFrame frame;
                _input.Read(out frame);
                _settings.Refresh();
                var controlled = MyAPIGateway.Session == null ? null : MyAPIGateway.Session.ControlledObject;
                if (frame.IsInputEnabled == 0)
                {
                    TraceInput("[InputTrace] Input is disabled by Kontrol.");
                    StopLastControlled();
                    _previousActions = 0;
                    _status.Pulse();
                    return;
                }
                if (controlled == null)
                {
                    TraceInput("[InputTrace] No controlled entity is available.");
                    StopLastControlled();
                    _previousActions = 0;
                    _status.Pulse();
                    return;
                }
                if (!controlled.ControllerInfo.IsLocallyHumanControlled())
                {
                    TraceInput("[InputTrace] Controlled entity is not locally human-controlled.");
                    StopLastControlled();
                    _previousActions = 0;
                    _status.Pulse();
                    return;
                }

                TraceInput(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "[InputTrace] Queued pitch={0:0.000}, roll={1:0.000}, yaw={2:0.000}, forward={3:0.000}, strafe={4:0.000}, lift={5:0.000}, actions=0x{6:X} for the final ship-control commit.",
                    frame.ReadAnalog(0), frame.ReadAnalog(1), frame.ReadAnalog(2), frame.ReadAnalog(3), frame.ReadAnalog(4), frame.ReadAnalog(5), frame.TriggeredActions));

                var edges = frame.TriggeredActions & ~_previousActions;
                if ((edges & (1UL << SpaceEngineersControlLayout.DampenersAction)) != 0) { controlled.SwitchDamping(); _logs.Write("[ControlTrace] Dampeners action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.LightsAction)) != 0) { controlled.SwitchLights(); _logs.Write("[ControlTrace] Lights action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.LandingGearsAction)) != 0) { controlled.SwitchLandingGears(); _logs.Write("[ControlTrace] Park action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.HandbrakeAction)) != 0) { controlled.SwitchHandbrake(); _logs.Write("[ControlTrace] Handbrake action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.CameraModeSwitchAction)) != 0) SwitchCameraMode();
                ApplyToolbarActions(controlled, edges);
                if ((edges & (1UL << SpaceEngineersControlLayout.LeaveControlAction)) != 0) { controlled.Use(); _logs.Write("[ControlTrace] Use / Interact action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.ReactorsAction)) != 0) { controlled.SwitchReactors(); _logs.Write("[ControlTrace] Power switch action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.ShowTerminalAction)) != 0) { controlled.ShowTerminal(); _logs.Write("[ControlTrace] Terminal / Inventory action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.ShowInventoryAction)) != 0) { controlled.ShowInventory(); _logs.Write("[ControlTrace] Inventory action applied."); }
                ApplyAdditionalActions(controlled, frame.DiscreteStates, edges);
                _previousActions = frame.TriggeredActions;
                _status.Pulse();
            }
            catch (Exception exception)
            {
                StopLastControlled();
                _logs.WriteError("[LifecycleTrace] Space Engineers plugin error: " + exception);
                _status.ReportError("Space Engineers adapter error", exception.Message, "Check Pulsar Legacy's info.log and restart the game.");
            }
        }

        public void Dispose()
        {
            StopLastControlled();
            ShipControlCommitHook.Uninstall(this);
            _input.Dispose();
            _settings.Dispose();
            _status.Dispose();
            _logs.Dispose();
        }

        private void TraceInput(string message)
        {
            DateTime now = DateTime.UtcNow;
            if (now - _lastInputTraceUtc < TimeSpan.FromMilliseconds(250))
                return;
            _lastInputTraceUtc = now;
            _logs.WriteDebug(message);
        }

        internal void MergeAtFinalControlCommit(MyShipController controlled, ref Vector3 movement, ref Vector2 rotation, ref float roll)
        {
            try
            {
                InputFrame frame;
                _input.Read(out frame);
                var current = MyAPIGateway.Session == null ? null : MyAPIGateway.Session.ControlledObject;
                if (frame.IsInputEnabled == 0 ||
                    !ReferenceEquals(current, controlled) || !controlled.ControllerInfo.IsLocallyHumanControlled())
                {
                    TraceControlMerge(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "Final control merge rejected: enabled={0}, currentMatches={1}, localHuman={2}.",
                        frame.IsInputEnabled,
                        ReferenceEquals(current, controlled),
                        controlled.ControllerInfo.IsLocallyHumanControlled()));
                    return;
                }

                // Harmony runs immediately before SE1 consumes MoveAndRotate's native
                // keyboard/mouse arguments. Neutral Kontrol input is a true pass-through.
                var nativeMovement = movement;
                var nativeRotation = rotation;
                float nativeRoll = roll;
                var sensitivities = _settings.Current;
                var cameraController = controlled as IMyCameraController;
                bool cameraLookActive = cameraController != null && CameraInputMath.IsCameraLookActive(frame.DiscreteStates);
                string cameraMode = cameraController != null && cameraController.IsInFirstPersonView
                    ? "first-person-look-around"
                    : "third-person-look-around";
                float forwardAxis = frame.ReadAnalog(3);
                bool forwardThrustSuppressed = cameraLookActive && CameraInputMath.IsAxisActive(forwardAxis);
                movement = new Vector3(
                    CameraInputMath.ResolveShipAxis(nativeMovement.X, frame.ReadAnalog(4), false),
                    CameraInputMath.ResolveShipAxis(nativeMovement.Y, frame.ReadAnalog(5), false),
                    CameraInputMath.ResolveShipAxis(nativeMovement.Z, forwardAxis, cameraLookActive));
                rotation = new Vector2(
                    CameraInputMath.ResolveShipAxis(nativeRotation.X, frame.ReadAnalog(0) * sensitivities.Pitch, cameraLookActive),
                    CameraInputMath.ResolveShipAxis(nativeRotation.Y, frame.ReadAnalog(2) * sensitivities.Yaw, cameraLookActive));
                roll = CameraInputMath.ResolveShipAxis(nativeRoll, frame.ReadAnalog(1) * sensitivities.Roll, false);
                if (cameraLookActive)
                {
                    float dedicatedCameraVertical = frame.ReadAnalog(SpaceEngineersControlLayout.CameraLookVerticalAnalog);
                    float dedicatedCameraHorizontal = frame.ReadAnalog(SpaceEngineersControlLayout.CameraLookHorizontalAnalog);
                    DateTime now = DateTime.UtcNow;
                    double elapsedSeconds = _lastCameraLookUtc == default ? 0d : (now - _lastCameraLookUtc).TotalSeconds;
                    _lastCameraLookUtc = now;
                    float cameraVertical = CameraInputMath.ApplyLookAxis(CameraInputMath.ResolveLookAxis(
                        dedicatedCameraVertical,
                        frame.ReadAnalog(0)), sensitivities.CameraLook, elapsedSeconds);
                    float cameraHorizontal = CameraInputMath.ApplyLookAxis(CameraInputMath.ResolveLookAxis(
                        dedicatedCameraHorizontal,
                        frame.ReadAnalog(2)), sensitivities.CameraLook, elapsedSeconds);
                    if (CameraInputMath.IsAxisActive(cameraVertical) || CameraInputMath.IsAxisActive(cameraHorizontal))
                    {
                        ShipControlCommitHook.RotateCameraWithoutZoom(cameraController, new Vector2(
                            cameraVertical,
                            cameraHorizontal));
                    }
                    TraceCamera(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "Camera routing: mode={0}; sensitivity={1:0.000}; elapsed={2:0.0000}s; flight[pitch={3:0.000},roll={4:0.000},yaw={5:0.000},forward={6:0.000},strafe={7:0.000},lift={8:0.000}]; dedicated[horizontal={9:0.000},vertical={10:0.000},zoom={11:0.000}]; applied[horizontal={12:0.000},vertical={13:0.000}]; thrustSuppression[forward={14:0.000},applied={15}].",
                        cameraMode,
                        sensitivities.CameraLook,
                        elapsedSeconds,
                        frame.ReadAnalog(0), frame.ReadAnalog(1), frame.ReadAnalog(2), frame.ReadAnalog(3), frame.ReadAnalog(4), frame.ReadAnalog(5),
                        dedicatedCameraHorizontal, dedicatedCameraVertical, frame.ReadAnalog(SpaceEngineersControlLayout.CameraZoomAnalog),
                        cameraHorizontal, cameraVertical, forwardAxis, forwardThrustSuppressed));
                }
                else
                {
                    _lastCameraLookUtc = default(DateTime);
                }
                TraceControlMerge(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Final control argument merge: nativeMove={0}, nativeRotation={1}, nativeRoll={2:0.000}, cameraLook={3}, merged pitch={4:0.000}, yaw={5:0.000}, roll={6:0.000}, forward={7:0.000}, strafe={8:0.000}, lift={9:0.000}, forwardThrustSuppressed={10}.",
                    nativeMovement, nativeRotation, nativeRoll, cameraLookActive, rotation.X, rotation.Y, roll, movement.Z, movement.X, movement.Y, forwardThrustSuppressed));
                _lastControlled = controlled;
                TraceInput(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "[InputTrace] Merged Kontrol with native SE1 input at final ship-control commit. pitch={0:0.000}, roll={1:0.000}, yaw={2:0.000}, forward={3:0.000}, strafe={4:0.000}, lift={5:0.000}.",
                    rotation.X, roll, rotation.Y, movement.Z, movement.X, movement.Y));
            }
            catch (Exception exception)
            {
                PulsarStartupTrace.Write("Final ship-control hook failed: " + exception);
                _logs.WriteError("[LifecycleTrace] Final ship-control hook error: " + exception);
                _status.ReportError(
                    "Space Engineers control hook error",
                    exception.Message,
                    "Check Pulsar Legacy's info.log and restart the game.");
            }
        }

        private void TraceControlMerge(string message)
        {
            DateTime now = DateTime.UtcNow;
            if (now - _lastHookTraceUtc < TimeSpan.FromMilliseconds(500)) return;
            _lastHookTraceUtc = now;
            PulsarStartupTrace.Write(message);
        }

        private void TraceCamera(string message)
        {
            DateTime now = DateTime.UtcNow;
            if (now - _lastCameraTraceUtc < TimeSpan.FromMilliseconds(250)) return;
            _lastCameraTraceUtc = now;
            PulsarStartupTrace.Write(message);
            _logs.WriteDebug("[CameraTrace] " + message);
        }

        private void TraceZoom(bool zoomIn, string message)
        {
            DateTime now = DateTime.UtcNow;
            ref DateTime lastTraceUtc = ref (zoomIn ? ref _lastZoomInTraceUtc : ref _lastZoomOutTraceUtc);
            if (now - lastTraceUtc < TimeSpan.FromMilliseconds(500)) return;
            lastTraceUtc = now;
            PulsarStartupTrace.Write(message);
            _logs.WriteDebug("[CameraZoomTrace] " + message);
        }

        internal bool ShouldMergeKontrolCameraZoom()
        {
            try
            {
                InputFrame frame;
                _input.Read(out frame);
                if (frame.IsInputEnabled == 0) return false;

                var controlled = MyAPIGateway.Session == null ? null : MyAPIGateway.Session.ControlledObject;
                var cameraController = controlled as IMyCameraController;
                return controlled is MyShipController &&
                       controlled.ControllerInfo.IsLocallyHumanControlled() &&
                       cameraController != null &&
                       !cameraController.IsInFirstPersonView &&
                       CameraInputMath.IsCameraLookActive(frame.DiscreteStates);
            }
            catch (Exception exception)
            {
                _logs.WriteError("[ControlTrace] Native third-person zoom eligibility check failed: " + exception);
                return false;
            }
        }

        internal float MergeNativeCameraZoom(MyStringId context, MyStringId control, float nativeValue)
        {
            try
            {
                if (control != MyControlsSpace.CAMERA_ZOOM_IN && control != MyControlsSpace.CAMERA_ZOOM_OUT)
                    return nativeValue;

                InputFrame frame;
                _input.Read(out frame);
                if (frame.IsInputEnabled == 0) return nativeValue;

                var controlled = MyAPIGateway.Session == null ? null : MyAPIGateway.Session.ControlledObject;
                var cameraController = controlled as IMyCameraController;
                if (!(controlled is MyShipController) ||
                    !controlled.ControllerInfo.IsLocallyHumanControlled() ||
                    cameraController == null || cameraController.IsInFirstPersonView ||
                    !CameraInputMath.IsCameraLookActive(frame.DiscreteStates)) return nativeValue;

                float dedicatedZoomAxis = frame.ReadAnalog(SpaceEngineersControlLayout.CameraZoomAnalog);
                float fallbackForwardAxis = frame.ReadAnalog(3);
                float zoomAxis = CameraInputMath.ResolveLookAxis(dedicatedZoomAxis, fallbackForwardAxis);
                bool zoomIn = control == MyControlsSpace.CAMERA_ZOOM_IN;
                float kontrolValue = CameraInputMath.ResolveZoomControlValue(
                    zoomAxis,
                    _settings.Current.CameraLook,
                    zoomIn);
                float mergedValue = InputMerge.StrongerAxis(nativeValue, kontrolValue);
                TraceZoom(zoomIn, string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Native camera zoom: mode=third-person-look-around; context={0}; direction={1}; sensitivity={2:0.000}; rawKontrol[dedicated={3:0.000},fallbackForward={4:0.000}]; resolvedAxis={5:0.000}; kontrolMagnitude={6:0.000}; nativeBefore={7:0.000}; mergedAfter={8:0.000}.",
                    context, zoomIn ? "in" : "out", _settings.Current.CameraLook, dedicatedZoomAxis, fallbackForwardAxis, zoomAxis, kontrolValue, nativeValue, mergedValue));
                return mergedValue;
            }
            catch (Exception exception)
            {
                _logs.WriteError("[ControlTrace] Native third-person zoom input hook failed: " + exception);
                return nativeValue;
            }
        }

        private void ApplyAdditionalActions(IMyControllableEntity controlled, ulong states, ulong edges)
        {
            var ship = controlled as MyShipController;
            if (ship == null)
            {
                StopWeaponActions();
            }
            else
            {
                if ((edges & (1UL << SpaceEngineersControlLayout.BroadcastingAction)) != 0)
                {
                    ship.SwitchBroadcasting();
                    _logs.Write("[ControlTrace] Broadcasting action applied.");
                }
                if ((edges & (1UL << SpaceEngineersControlLayout.LocalPowerAction)) != 0)
                {
                    ship.SwitchReactorsLocal();
                    _logs.Write("[ControlTrace] Local power switch action applied.");
                }

                ApplyWeaponActions(ship, states);
            }

            if ((edges & (1UL << SpaceEngineersControlLayout.ToggleHudAction)) != 0)
            {
                MyHud.ToggleGamepadHud();
                _logs.Write("[ControlTrace] HUD toggle action applied.");
            }
            if ((edges & (1UL << SpaceEngineersControlLayout.ChatScreenAction)) != 0)
                ToggleChatScreen();

            ApplyVoiceChat(states);
        }

        private void ApplyWeaponActions(MyShipController ship, ulong states)
        {
            if (!ReferenceEquals(_weaponController, ship))
            {
                StopWeaponActions();
                _weaponController = ship;
            }

            bool primary = CameraInputMath.IsActionActive(states, SpaceEngineersControlLayout.PrimaryAction);
            bool secondary = CameraInputMath.IsActionActive(states, SpaceEngineersControlLayout.SecondaryAction);
            if (primary != _primaryWeaponActive)
            {
                if (primary) ship.BeginShootSync(MyShootActionEnum.PrimaryAction);
                else ship.EndShootSync(MyShootActionEnum.PrimaryAction);
                _primaryWeaponActive = primary;
            }
            if (secondary != _secondaryWeaponActive)
            {
                if (secondary) ship.BeginShootSync(MyShootActionEnum.SecondaryAction);
                else ship.EndShootSync(MyShootActionEnum.SecondaryAction);
                _secondaryWeaponActive = secondary;
            }
        }

        private void ApplyVoiceChat(ulong states)
        {
            bool active = CameraInputMath.IsActionActive(states, SpaceEngineersControlLayout.VoiceChatAction);
            if (active == _voiceChatActive) return;

            var voiceChat = MyVoiceChatSessionComponent.Static;
            if (voiceChat != null)
            {
                if (active) voiceChat.StartRecording();
                else voiceChat.StopRecording();
            }
            _voiceChatActive = active;
        }

        private void ToggleChatScreen()
        {
            if (ChatScreenType == null || ChatScreenStaticField == null || GuiSandboxAddScreenMethod == null)
            {
                _logs.WriteWarning("[ControlTrace] Chat screen action is unavailable because SE1's GUI types were not found.");
                return;
            }

            object activeChat = ChatScreenStaticField.GetValue(null);
            if (activeChat != null)
            {
                var close = AccessTools.Method(activeChat.GetType(), "CloseScreenNow", [typeof(bool)]);
                if (close == null) throw new MissingMethodException(ChatScreenTypeName, "CloseScreenNow");
                close.Invoke(activeChat, [false]);
                _logs.Write("[ControlTrace] Chat screen closed.");
                return;
            }

            object chatScreen = Activator.CreateInstance(ChatScreenType, new Vector2(0.029f, 0.8f));
            GuiSandboxAddScreenMethod.Invoke(null, [chatScreen]);
            _logs.Write("[ControlTrace] Chat screen opened.");
        }

        private void SwitchCameraMode()
        {
            try
            {
                var gamePlay = GamePlayStaticField == null ? null : GamePlayStaticField.GetValue(null);
                if (gamePlay == null || SwitchCameraMethod == null)
                {
                    _logs.WriteWarning("[ControlTrace] Camera Mode Switch received before the SE1 gameplay screen was ready.");
                    return;
                }

                SwitchCameraMethod.Invoke(gamePlay, null);
                _logs.Write("[ControlTrace] Camera Mode Switch action applied.");
            }
            catch (Exception exception)
            {
                _logs.WriteError("[ControlTrace] Camera Mode Switch failed: " + exception);
            }
        }

        private void ApplyToolbarActions(IMyControllableEntity controlled, ulong edges)
        {
            var toolbarOwner = controlled as SpaceEngineersControllableEntity;
            if (toolbarOwner == null || toolbarOwner.Toolbar == null) return;

            for (int action = SpaceEngineersControlLayout.ToolbarFirstAction;
                 action < SpaceEngineersControlLayout.ToolbarFirstAction + SpaceEngineersControlLayout.ToolbarActionCount;
                 action++)
            {
                if ((edges & (1UL << action)) == 0) continue;

                int slot = action - SpaceEngineersControlLayout.ToolbarFirstAction;
                try
                {
                    toolbarOwner.Toolbar.ActivateItemAtSlot(slot);
                    _logs.Write("[ControlTrace] Toolbar slot " + (slot + 1) + " action applied.");
                }
                catch (Exception exception)
                {
                    _logs.WriteError("[ControlTrace] Toolbar slot " + (slot + 1) + " failed: " + exception);
                }
            }
        }

        private void StopLastControlled()
        {
            StopWeaponActions();
            StopVoiceChat();
            if (_lastControlled != null) _lastControlled.MoveAndRotateStopped();
            _lastControlled = null;
            _lastCameraLookUtc = default(DateTime);
        }

        private void StopWeaponActions()
        {
            if (_weaponController != null)
            {
                if (_primaryWeaponActive) _weaponController.EndShootSync(MyShootActionEnum.PrimaryAction);
                if (_secondaryWeaponActive) _weaponController.EndShootSync(MyShootActionEnum.SecondaryAction);
            }
            _weaponController = null;
            _primaryWeaponActive = false;
            _secondaryWeaponActive = false;
        }

        private void StopVoiceChat()
        {
            if (_voiceChatActive && MyVoiceChatSessionComponent.Static != null)
                MyVoiceChatSessionComponent.Static.StopRecording();
            _voiceChatActive = false;
        }

    }
}
