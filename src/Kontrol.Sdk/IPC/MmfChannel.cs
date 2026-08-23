using System.Diagnostics.CodeAnalysis;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace Kontrol.Sdk.IPC;

[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public class MmfChannel<T>(string mapName) : IDisposable
    where T : struct
{
    private readonly int _size = Marshal.SizeOf<T>();
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;

    public void CreateOrOpen()
    {
        Dispose();
        try
        {
            // Attempt to open existing memory mapped file
            _mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.ReadWrite);
        }
        catch (Exception)
        {
            try
            {
                // Create a new or open memory mapped file if it doesn't exist
                _mmf = MemoryMappedFile.CreateOrOpen(mapName, _size, MemoryMappedFileAccess.ReadWrite);
            }
            catch
            {
                // Fallback attempt to open in case of race conditions
                _mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.ReadWrite);
            }
        }
        _accessor = _mmf.CreateViewAccessor(0, _size, MemoryMappedFileAccess.ReadWrite);
    }

    public void Write(ref T data)
    {
        _accessor?.Write(0, ref data);
    }

    public void Read(out T data)
    {
        if (_accessor != null)
        {
            _accessor.Read(0, out data);
        }
        else
        {
            data = default;
        }
    }

    public void Dispose()
    {
        _accessor?.Dispose();
        _accessor = null;
        _mmf?.Dispose();
        _mmf = null;
    }
}
