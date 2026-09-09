using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRageMath;

namespace Kontrol.Adapters.SpaceEngineers1.Plugin
{
    public sealed class SpaceEngineers1Plugin : VRage.Plugins.IPlugin, IDisposable
    {
        private const uint SchemaVersion = 1;
        private const string InputMapName = @"Local\Kontrol_Input_space-engineers-1";
        private readonly LegacyMmfChannel<InputFrame> _input = new LegacyMmfChannel<InputFrame>(InputMapName);
        private readonly PulsarStatusReporter _status = new PulsarStatusReporter();
        private IMyControllableEntity _lastControlled;
        private ulong _previousActions;

        public void Init(object gameInstance)
        {
            _input.CreateOrOpen();
            _status.ReportLoaded();
        }

        public void Update()
        {
            try
            {
                InputFrame frame;
                _input.Read(out frame);
                var controlled = MyAPIGateway.Session == null ? null : MyAPIGateway.Session.ControlledObject;
                if (frame.SchemaVersion != SchemaVersion || frame.IsInputEnabled == 0 || controlled == null || !controlled.ControllerInfo.IsLocallyHumanControlled())
                {
                    StopLastControlled();
                    _previousActions = 0;
                    _status.Pulse();
                    return;
                }

                var movement = new Vector3(frame.ReadAnalog(4), frame.ReadAnalog(5), frame.ReadAnalog(3));
                var rotation = new Vector2(frame.ReadAnalog(0), frame.ReadAnalog(2));
                controlled.MoveAndRotate(movement, rotation, frame.ReadAnalog(1));
                _lastControlled = controlled;

                var edges = frame.TriggeredActions & ~_previousActions;
                if ((edges & (1UL << SpaceEngineers1ControlLayout.DampenersAction)) != 0) controlled.SwitchDamping();
                if ((edges & (1UL << SpaceEngineers1ControlLayout.LightsAction)) != 0) controlled.SwitchLights();
                if ((edges & (1UL << SpaceEngineers1ControlLayout.LandingGearsAction)) != 0) controlled.SwitchLandingGears();
                if ((edges & (1UL << SpaceEngineers1ControlLayout.HandbrakeAction)) != 0) controlled.SwitchHandbrake();
                _previousActions = frame.TriggeredActions;
                _status.Pulse();
            }
            catch (Exception exception)
            {
                StopLastControlled();
                _status.ReportError("SE1 adapter error", exception.Message, "Check Pulsar Legacy's info.log and restart the game.");
            }
        }

        public void Dispose()
        {
            StopLastControlled();
            _input.Dispose();
            _status.Dispose();
        }

        private void StopLastControlled()
        {
            if (_lastControlled != null) _lastControlled.MoveAndRotateStopped();
            _lastControlled = null;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private unsafe struct InputFrame
        {
            public const int MaxAnalogInputs = 32;
            public uint SchemaVersion;
            public byte IsInputEnabled;
            public fixed float AnalogValues[MaxAnalogInputs];
            public ulong DiscreteStates;
            public ulong TriggeredActions;

            public float ReadAnalog(int index)
            {
                if (index < 0 || index >= MaxAnalogInputs) return 0f;
                fixed (float* values = AnalogValues) return values[index];
            }
        }
    }
}
