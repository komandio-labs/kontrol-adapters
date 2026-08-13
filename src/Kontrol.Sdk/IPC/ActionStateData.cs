using System.Runtime.InteropServices;

namespace Kontrol.Sdk.IPC;

/// <summary>Versioned generic action state. Bit position maps to the adapter-declared action order.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ActionStateData
{
    public uint ProtocolVersion;
    public ulong PressedActions;
}
