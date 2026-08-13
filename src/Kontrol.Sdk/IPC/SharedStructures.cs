using System.Runtime.InteropServices;

namespace Kontrol.Sdk.IPC;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TelemetryData
{
    public ulong F0, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, F13, F14, F15,
                 F16, F17, F18, F19, F20, F21, F22, F23, F24, F25, F26, F27, F28, F29, F30, F31,
                 F32, F33, F34, F35, F36, F37, F38, F39, F40, F41, F42, F43, F44, F45, F46, F47,
                 F48, F49, F50, F51, F52, F53, F54, F55, F56, F57, F58, F59, F60, F61, F62, F63;

    public void SetJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
        int len = Math.Min(bytes.Length, 511);

        byte[] buffer = new byte[512];
        Array.Copy(bytes, buffer, len);

        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            this = Marshal.PtrToStructure<TelemetryData>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    public string GetJson()
    {
        byte[] buffer = new byte[512];
        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(this, handle.AddrOfPinnedObject(), false);
        }
        finally
        {
            handle.Free();
        }

        int len = 0;
        while (len < buffer.Length && buffer[len] != 0)
        {
            len++;
        }
        return System.Text.Encoding.UTF8.GetString(buffer, 0, len);
    }
}
