using System.Runtime.InteropServices;

namespace Kontrol.Sdk.IPC;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct InputFrame
{
    public const int MaxAnalogInputs = 32;
    public uint SchemaVersion;
    public byte IsInputEnabled;
    public fixed float AnalogValues[MaxAnalogInputs];
    public ulong DiscreteStates;
    public ulong TriggeredActions;

    public float ReadAnalog(int index)
    {
        if (index < 0 || index >= MaxAnalogInputs)
        {
            return 0f;
        }

        fixed (float* values = AnalogValues)
        {
            return values[index];
        }
    }
}
