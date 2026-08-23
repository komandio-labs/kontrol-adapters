using System.Runtime.InteropServices;

namespace Kontrol.Sdk.IPC;

/// <summary>
/// Fixed-size Memory-Mapped File packet header for fast O(1) change-detection in JIT game hooks.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct AdapterSettingsIpcHeader
{
    public uint Magic;            // 'K','N','T','S' = 0x53544E4B
    public ulong SequenceNumber;  // Incremented on each setting change
    public long TimestampTicks;   // DateTime.UtcNow.Ticks
    public int PayloadBytesLength; // Length of UTF-8 JSON payload following header
    public uint Crc32Checksum;     // Integrity checksum
}
