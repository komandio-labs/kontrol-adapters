using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json.Nodes;

public static class AdapterAssemblyInspection
{
    public static JsonObject Inspect(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Assembly was not found.", fullPath);
        using var stream = File.OpenRead(fullPath);
        using var reader = new PEReader(stream);
        if (!reader.HasMetadata) throw new InvalidOperationException($"'{fullPath}' is not a managed assembly.");
        MetadataReader metadata = reader.GetMetadataReader();
        return new JsonObject
        {
            ["fileVersion"] = FileVersionInfo.GetVersionInfo(fullPath).FileVersion,
            ["sha256"] = AdapterRepository.HashFile(fullPath),
            ["mvid"] = metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString()
        };
    }
}
