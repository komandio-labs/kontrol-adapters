using System;
using System.Reflection;
using HarmonyLib;
using Kontrol.Sdk.Diagnostics;
using Kontrol.Sdk.IPC;
using MyShipController = Sandbox.Game.Entities.MyShipController;
using SpaceEngineersControllableEntity = Sandbox.Game.Entities.IMyControllableEntity;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRageMath;

namespace Kontrol.Adapters.SpaceEngineers.Plugin
{
    public sealed class SpaceEngineersPlugin : VRage.Plugins.IPlugin, IDisposable
    {
        private const string InputMapName = @"Local\Kontrol_Input_space-engineers";
        private const string GamePlayScreenTypeName = "Sandbox.Game.Gui.MyGuiScreenGamePlay";
        private static readonly FieldInfo GamePlayStaticField = AccessTools.Field(GamePlayScreenTypeName + ":Static");
        private static readonly MethodInfo SwitchCameraMethod = AccessTools.Method(GamePlayScreenTypeName + ":SwitchCamera");
        private readonly MmfChannel<InputFrame> _input = new MmfChannel<InputFrame>(InputMapName);
        private readonly LegacyFlightSettingsReader _settings = new LegacyFlightSettingsReader();
        private readonly AdapterConnectionReporter _status = new AdapterConnectionReporter("space-engineers");
        private readonly AdapterLogReporter _logs = new AdapterLogReporter("space-engineers");
        private IMyControllableEntity _lastControlled;
        private ulong _previousActions;
        private DateTime _lastInputTraceUtc;
        private DateTime _lastHookPrefixTraceUtc;
        private DateTime _lastHookPostfixTraceUtc;

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
                if ((edges & (1UL << SpaceEngineersControlLayout.LandingGearsAction)) != 0) { controlled.SwitchLandingGears(); _logs.Write("[ControlTrace] Landing gear action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.HandbrakeAction)) != 0) { controlled.SwitchHandbrake(); _logs.Write("[ControlTrace] Handbrake action applied."); }
                if ((edges & (1UL << SpaceEngineersControlLayout.CameraModeSwitchAction)) != 0) SwitchCameraMode();
                ApplyToolbarActions(controlled, edges);
                if ((edges & (1UL << SpaceEngineersControlLayout.LeaveControlAction)) != 0) { controlled.Use(); _logs.Write("[ControlTrace] Leave vehicle / cockpit action applied."); }
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

        internal void ApplyAtFinalControlCommit(MyShipController controlled)
        {
            try
            {
                InputFrame frame;
                _input.Read(out frame);
                var current = MyAPIGateway.Session == null ? null : MyAPIGateway.Session.ControlledObject;
                if (frame.IsInputEnabled == 0 ||
                    !ReferenceEquals(current, controlled) || !controlled.ControllerInfo.IsLocallyHumanControlled())
                {
                    TraceHookPrefix(string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "Final control prefix rejected: enabled={0}, currentMatches={1}, localHuman={2}.",
                        frame.IsInputEnabled,
                        ReferenceEquals(current, controlled),
                        controlled.ControllerInfo.IsLocallyHumanControlled()));
                    return;
                }

                var movement = new Vector3(frame.ReadAnalog(4), frame.ReadAnalog(5), frame.ReadAnalog(3));
                var sensitivities = _settings.Current;
                var rotation = new Vector2(
                    frame.ReadAnalog(0) * sensitivities.Pitch,
                    frame.ReadAnalog(2) * sensitivities.Yaw);
                controlled.MoveAndRotate(movement, rotation, frame.ReadAnalog(1) * sensitivities.Roll);
                TraceHookPrefix(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "Final control prefix prepared indicators: pitch={0:0.000}, yaw={1:0.000}, roll={2:0.000}, forward={3:0.000}, strafe={4:0.000}, lift={5:0.000}.",
                    rotation.X, rotation.Y, frame.ReadAnalog(1) * sensitivities.Roll, movement.Z, movement.X, movement.Y));
                _lastControlled = controlled;
                TraceInput(string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "[InputTrace] Committed pitch={0:0.000}, roll={1:0.000}, yaw={2:0.000}, forward={3:0.000}, strafe={4:0.000}, lift={5:0.000} at SE1 final ship-control commit.",
                    frame.ReadAnalog(0), frame.ReadAnalog(1), frame.ReadAnalog(2), frame.ReadAnalog(3), frame.ReadAnalog(4), frame.ReadAnalog(5)));
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

        internal void TraceAfterFinalControlCommit(MyShipController controlled)
        {
            TraceHookPostfix(string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Final control postfix observed: lastMove={0}, lastRotation={1}, thrusters={2}, gyros={3}.",
                controlled.LastMotionIndicator,
                controlled.LastRotationIndicator,
                controlled.ControlThrusters,
                controlled.ControlGyros));
        }

        private void TraceHookPrefix(string message)
        {
            DateTime now = DateTime.UtcNow;
            if (now - _lastHookPrefixTraceUtc < TimeSpan.FromMilliseconds(500)) return;
            _lastHookPrefixTraceUtc = now;
            PulsarStartupTrace.Write(message);
        }

        private void TraceHookPostfix(string message)
        {
            DateTime now = DateTime.UtcNow;
            if (now - _lastHookPostfixTraceUtc < TimeSpan.FromMilliseconds(500)) return;
            _lastHookPostfixTraceUtc = now;
            PulsarStartupTrace.Write(message);
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
            if (_lastControlled != null) _lastControlled.MoveAndRotateStopped();
            _lastControlled = null;
        }

    }
}
