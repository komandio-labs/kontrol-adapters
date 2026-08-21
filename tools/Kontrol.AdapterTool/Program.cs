using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

return await AdapterToolProgram.RunAsync(args);

public static class AdapterToolProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args.Length == 0) return Usage();
            string root = AdapterRepository.FindRoot(Directory.GetCurrentDirectory());
            switch (args[0].ToLowerInvariant())
            {
                case "validate":
                    return Validate(root, args);
                case "affected":
                    return Affected(root, args);
                case "pack":
                    return Pack(root, CommandOptions.Parse(args.Skip(1)));
                case "verify-package":
                    var verifyOptions = CommandOptions.Parse(args.Skip(1));
                    AdapterPackage.Verify(verifyOptions.Required("package"), verifyOptions.Get("public-key"), verifyOptions.Get("require-signature") == "true");
                    Console.WriteLine("Package is valid.");
                    return 0;
                case "sign-package":
                    var signOptions = CommandOptions.Parse(args.Skip(1));
                    AdapterPackage.Sign(signOptions.Required("package"), signOptions.Required("private-key"), signOptions.Get("key-id") ?? AdapterPackage.OfficialKeyId);
                    Console.WriteLine("Package is signed.");
                    return 0;
                case "release":
                    return Release(root, args);
                case "inspect-assembly":
                    var inspectOptions = CommandOptions.Parse(args.Skip(1));
                    Console.WriteLine(AdapterAssemblyInspection.Inspect(inspectOptions.Required("path")).ToJsonString());
                    return 0;
                case "catalog":
                    return Catalog(root, args);
                default:
                    return Usage();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
        finally
        {
            await Task.CompletedTask;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("Usage: validate repository | validate adapter --adapter <slug> | validate compatibility --adapter <slug> --inspection <path> | affected paths <paths...> | pack --adapter <slug> --output <zip> [--configuration Release] [--overwrite true] | sign-package --package <zip> --private-key <key> [--key-id kontrol-p256-2026-01] | verify-package --package <zip> [--require-signature true --public-key <key>] | release create --adapter <slug> --package <zip> --package-url <url> --tag <tag> --commit <sha> --output <json> [--channel stable|beta] | release sign --descriptor <json> --private-key <key> | release validate --descriptor <json> [--package <zip>] | catalog build --releases <directory> --generated-at <ISO-8601> --output <json> | catalog sign --catalog <json> --private-key <key> | catalog validate --catalog <json> | inspect-assembly --path <dll>");
        return 2;
    }

    private static int Validate(string root, string[] args)
    {
        if (args.Length < 2) return Usage();
        var options = CommandOptions.Parse(args.Skip(2));
        switch (args[1].ToLowerInvariant())
        {
            case "repository":
                AdapterRepository.ValidateRepository(root);
                Console.WriteLine("Repository metadata is valid.");
                return 0;
            case "adapter":
                AdapterRepository.ValidateAdapter(root, options.Required("adapter"));
                Console.WriteLine("Adapter metadata is valid.");
                return 0;
            case "compatibility":
                AdapterRepository.ValidateCompatibility(root, options.Required("adapter"), options.Required("inspection"));
                Console.WriteLine($"Compatibility classification: {AdapterRepository.EvaluateCompatibility(root, options.Required("adapter"), options.Required("inspection"))}.");
                return 0;
            default:
                return Usage();
        }
    }

    private static int Affected(string root, string[] args)
    {
        if (args.Length < 3) return Usage();
        if (string.Equals(args[1], "--base", StringComparison.OrdinalIgnoreCase))
        {
            foreach (string adapter in AdapterRepository.GetAffectedAdapters(root, args[2])) Console.WriteLine(adapter);
            return 0;
        }
        if (string.Equals(args[1], "paths", StringComparison.OrdinalIgnoreCase))
        {
            foreach (string adapter in AdapterRepository.GetAffectedAdaptersFromPaths(root, args.Skip(2))) Console.WriteLine(adapter);
            return 0;
        }
        return Usage();
    }

    private static int Pack(string root, CommandOptions options)
    {
        string output = options.Required("output");
        string configuration = options.Get("configuration") ?? "Release";
        AdapterPackage.Create(root, options.Required("adapter"), configuration, output, options.Get("overwrite") == "true");
        Console.WriteLine(Path.GetFullPath(output));
        return 0;
    }

    private static int Catalog(string root, string[] args)
    {
        if (args.Length < 2) return Usage();
        var options = CommandOptions.Parse(args.Skip(2));
        switch (args[1].ToLowerInvariant())
        {
            case "build":
                AdapterCatalog.Build(root, options.Required("releases"), options.Required("generated-at"), options.Required("output"));
                Console.WriteLine(Path.GetFullPath(options.Required("output")));
                return 0;
            case "validate":
                AdapterCatalog.Validate(options.Required("catalog"));
                Console.WriteLine("Catalog is valid.");
                return 0;
            case "sign":
                AdapterCatalog.Sign(options.Required("catalog"), options.Required("private-key"));
                Console.WriteLine("Catalog is signed.");
                return 0;
            default:
                return Usage();
        }
    }

    private static int Release(string root, string[] args)
    {
        if (args.Length < 2) return Usage();
        var options = CommandOptions.Parse(args.Skip(2));
        switch (args[1].ToLowerInvariant())
        {
            case "create":
                bool overwrite = string.Equals(options.Get("overwrite"), "true", StringComparison.OrdinalIgnoreCase);
                string? publishedAt = options.Get("published-at");
                AdapterRelease.Create(root, options.Required("adapter"), options.Required("package"),
                    options.Required("package-url"), options.Required("tag"), options.Required("commit"), options.Required("output"), options.Get("channel") ?? "stable", overwrite, publishedAt);
                Console.WriteLine(Path.GetFullPath(options.Required("output")));
                return 0;
            case "validate":
                AdapterRelease.Validate(options.Required("descriptor"), options.Get("package"));
                Console.WriteLine("Release descriptor is valid.");
                return 0;
            case "sign":
                AdapterRelease.Sign(options.Required("descriptor"), options.Required("private-key"));
                Console.WriteLine("Release descriptor is signed.");
                return 0;
            case "update-descriptors":
                AdapterRelease.UpdateDescriptors(root, options.Required("releases"));
                Console.WriteLine("Release descriptors updated.");
                return 0;
            default:
                return Usage();
        }
    }
}

public sealed class CommandOptions
{
    private readonly Dictionary<string, string> _values;
    private CommandOptions(Dictionary<string, string> values) => _values = values;

    public static CommandOptions Parse(IEnumerable<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] input = args.ToArray();
        for (int index = 0; index < input.Length; index++)
        {
            if (!input[index].StartsWith("--", StringComparison.Ordinal)) continue;
            if (index + 1 >= input.Length || input[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Option '{input[index]}' requires a value.");
            values[input[index][2..]] = input[++index];
        }
        return new CommandOptions(values);
    }

    public string Required(string name) => Get(name) ?? throw new ArgumentException($"Missing required option '--{name}'.");
    public string? Get(string name) => _values.GetValueOrDefault(name);
}

public sealed record AdapterManifest(
    string Path,
    string AdapterId,
    string Slug,
    string DisplayName,
    string AdapterVersion,
    string SdkVersion,
    string EntryAssembly,
    int InputSchemaVersion,
    string TargetFramework,
    IReadOnlyList<string> Architectures,
    IReadOnlyList<string> PackageFiles,
    string? GameProductVersion = null,
    IReadOnlyList<string>? RelevantAssemblies = null);

public enum CompatibilityClassification
{
    Tested,
    Untested,
    KnownIncompatible,
    Unknown
}

public static class AdapterRepository
{
    private static readonly Regex SemVersion = new("^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?$", RegexOptions.Compiled);
    private static readonly string[] ForbiddenExtensions = [".obj", ".bin", ".log", ".dmp", ".nupkg", ".snupkg", ".pdb"];

    public static string FindRoot(string start)
    {
        for (var current = new DirectoryInfo(Path.GetFullPath(start)); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "Kontrol.Adapters.slnx"))) return current.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate the kontrol-adapters repository root.");
    }

    public static IReadOnlyList<AdapterManifest> GetManifests(string root)
    {
        string adapters = Path.Combine(root, "src", "Adapters");
        return Directory.EnumerateFiles(adapters, "package.json", SearchOption.AllDirectories)
            .Select(ParseManifest).OrderBy(manifest => manifest.Slug, StringComparer.Ordinal).ToArray();
    }

    public static AdapterManifest GetManifest(string root, string slug) =>
        GetManifests(root).SingleOrDefault(manifest => string.Equals(manifest.Slug, slug, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"No adapter manifest exists for '{slug}'.");

    public static void ValidateRepository(string root)
    {
        var manifests = GetManifests(root);
        if (manifests.Count == 0) throw new InvalidOperationException("No adapter manifests were found.");
        if (manifests.Select(manifest => manifest.AdapterId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifests.Count)
            throw new InvalidOperationException("Adapter IDs must be unique.");
        if (manifests.Select(manifest => manifest.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifests.Count)
            throw new InvalidOperationException("Adapter slugs must be unique.");

        foreach (AdapterManifest manifest in manifests) ValidateManifest(root, manifest);
        foreach (string record in Directory.EnumerateFiles(Path.Combine(root, "src", "Adapters"), "*.json", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}compatibility{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            ValidateCompatibilityRecord(record, manifests);
        }
    }

    public static void ValidateAdapter(string root, string slug) => ValidateManifest(root, GetManifest(root, slug));

    public static void ValidateCompatibility(string root, string slug, string inspectionPath)
    {
        AdapterManifest manifest = GetManifest(root, slug);
        var inspection = JsonNode.Parse(File.ReadAllText(inspectionPath))?.AsObject() ?? throw new InvalidOperationException("Inspection JSON is invalid.");
        if (!string.Equals(inspection["adapterId"]?.GetValue<string>(), manifest.AdapterId, StringComparison.Ordinal))
            throw new InvalidOperationException("Inspection adapter ID does not match the selected manifest.");
        if (string.IsNullOrWhiteSpace(inspection["productVersion"]?.GetValue<string>()))
            throw new InvalidOperationException("Inspection does not contain a product version.");
        if (inspection["relevantAssemblies"] is not JsonObject assemblies || assemblies.Count == 0)
            throw new InvalidOperationException("Inspection does not contain relevant assembly evidence.");
        foreach ((string name, JsonNode? value) in assemblies)
        {
            string? hash = value?["sha256"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(hash) || !Regex.IsMatch(hash, "^[A-Fa-f0-9]{64}$"))
                throw new InvalidOperationException($"Inspection has invalid SHA-256 evidence for '{name}'.");
        }
    }

    public static CompatibilityClassification EvaluateCompatibility(string root, string slug, string inspectionPath)
    {
        ValidateCompatibility(root, slug, inspectionPath);
        AdapterManifest manifest = GetManifest(root, slug);
        var inspection = JsonNode.Parse(File.ReadAllText(inspectionPath))!.AsObject();
        string productVersion = inspection["productVersion"]!.GetValue<string>();
        var inspectedAssemblies = inspection["relevantAssemblies"]!.AsObject();
        var classifications = new List<CompatibilityClassification>();

        string compatibilityRoot = Path.Combine(AdapterRoot(manifest), "compatibility", "game-builds");
        if (!Directory.Exists(compatibilityRoot)) return CompatibilityClassification.Unknown;
        foreach (string recordPath in Directory.EnumerateFiles(compatibilityRoot, "*.json"))
        {
            var record = JsonNode.Parse(File.ReadAllText(recordPath))?.AsObject();
            if (record is null || !string.Equals(record["adapterVersion"]?.GetValue<string>(), manifest.AdapterVersion, StringComparison.Ordinal)) continue;
            var game = record["game"]?.AsObject();
            if (!string.Equals(game?["productVersion"]?.GetValue<string>(), productVersion, StringComparison.Ordinal)) continue;
            var requiredAssemblies = game?["relevantAssemblies"]?.AsObject();
            if (requiredAssemblies is null || !FingerprintsMatch(requiredAssemblies, inspectedAssemblies)) continue;
            classifications.Add(record["validation"]?["result"]?.GetValue<string>() switch
            {
                "tested" => CompatibilityClassification.Tested,
                "known-incompatible" => CompatibilityClassification.KnownIncompatible,
                _ => CompatibilityClassification.Untested
            });
        }

        if (classifications.Contains(CompatibilityClassification.KnownIncompatible)) return CompatibilityClassification.KnownIncompatible;
        if (classifications.Contains(CompatibilityClassification.Tested)) return CompatibilityClassification.Tested;
        return CompatibilityClassification.Untested;
    }

    public static IReadOnlyList<string> GetAffectedAdapters(string root, string baseRef)
    {
        var startInfo = new ProcessStartInfo("git", $"diff --name-only {baseRef}..HEAD") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not execute git diff.");
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(process.StandardError.ReadToEnd());
        return GetAffectedAdaptersFromPaths(root, output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }

    public static IReadOnlyList<string> GetAffectedAdaptersFromPaths(string root, IEnumerable<string> paths)
    {
        string[] changes = paths.Select(path => path.Replace('/', '\\')).ToArray();
        var all = GetManifests(root).Select(manifest => manifest.Slug).ToArray();
        if (changes.Any(path => path.StartsWith("src\\Kontrol.Sdk\\", StringComparison.OrdinalIgnoreCase) || path.StartsWith("tools\\", StringComparison.OrdinalIgnoreCase) || path.StartsWith("scripts\\", StringComparison.OrdinalIgnoreCase) || path.StartsWith("schemas\\", StringComparison.OrdinalIgnoreCase) || path.StartsWith("eng\\", StringComparison.OrdinalIgnoreCase))) return all;
        return all.Where(slug => changes.Any(path => path.StartsWith($"src\\Adapters\\{AdapterFolder(slug)}\\", StringComparison.OrdinalIgnoreCase))).ToArray();
    }

    public static string AdapterRoot(AdapterManifest manifest) => Path.GetDirectoryName(manifest.Path) ?? throw new InvalidOperationException("Manifest has no parent directory.");

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static AdapterManifest ParseManifest(string path)
    {
        var document = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new InvalidOperationException($"Manifest '{path}' is invalid JSON.");
        if (document["entryPoints"] is not null) throw new InvalidOperationException($"Manifest '{path}' must not duplicate loading entry points.");
        if (document["manifestVersion"]?.GetValue<int>() != 1) throw new InvalidOperationException($"Manifest '{path}' must use manifestVersion 1.");
        string Required(string name) => document[name]?.GetValue<string>() ?? throw new InvalidOperationException($"Manifest '{path}' is missing '{name}'.");
        var package = document["package"]?.AsObject() ?? throw new InvalidOperationException($"Manifest '{path}' is missing package metadata.");
        var include = package["include"]?.AsArray().Select(node => node?.GetValue<string>() ?? throw new InvalidOperationException($"Manifest '{path}' has an invalid package include.")).ToArray()
            ?? throw new InvalidOperationException($"Manifest '{path}' is missing package.include.");
        var architectures = document["architectures"]?.AsArray().Select(node => node?.GetValue<string>() ?? throw new InvalidOperationException($"Manifest '{path}' has an invalid architecture.")).ToArray()
            ?? throw new InvalidOperationException($"Manifest '{path}' is missing architectures.");
        string? gameProductVersion = document["gameProductVersion"]?.GetValue<string>();
        var relevantAssemblies = document["relevantAssemblies"]?.AsArray().Select(node => node?.GetValue<string>() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray();
        return new AdapterManifest(path, Required("adapterId"), Required("slug"), Required("displayName"), Required("adapterVersion"), Required("sdkVersion"), Required("entryAssembly"), document["inputSchemaVersion"]?.GetValue<int>() ?? 0, Required("targetFramework"), architectures, include, gameProductVersion, relevantAssemblies);
    }

    private static void ValidateManifest(string root, AdapterManifest manifest)
    {
        if (!SemVersion.IsMatch(manifest.AdapterVersion) || !SemVersion.IsMatch(manifest.SdkVersion)) throw new InvalidOperationException($"Manifest '{manifest.Path}' contains an invalid semantic version.");
        if (!Regex.IsMatch(manifest.Slug, "^[a-z0-9][a-z0-9-]*$")) throw new InvalidOperationException($"Manifest '{manifest.Path}' has an invalid slug.");
        if (string.IsNullOrWhiteSpace(manifest.DisplayName)) throw new InvalidOperationException($"Manifest '{manifest.Path}' has an empty displayName.");
        if (!manifest.EntryAssembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Manifest '{manifest.Path}' entryAssembly must be a DLL.");
        if (manifest.InputSchemaVersion < 1 || manifest.Architectures.Count == 0) throw new InvalidOperationException($"Manifest '{manifest.Path}' has invalid schema or architecture metadata.");
        if (!manifest.PackageFiles.Contains(manifest.EntryAssembly, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException($"Manifest '{manifest.Path}' package must include its entry assembly.");
        if (!manifest.PackageFiles.Contains("LICENSE", StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException($"Manifest '{manifest.Path}' package must include the Apache-2.0 LICENSE.");
        if (manifest.PackageFiles.Distinct(StringComparer.OrdinalIgnoreCase).Count() != manifest.PackageFiles.Count) throw new InvalidOperationException($"Manifest '{manifest.Path}' package contains duplicate files.");
        foreach (string file in manifest.PackageFiles)
        {
            if (Path.IsPathRooted(file) || file.Contains("..", StringComparison.Ordinal) || file.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0 || ForbiddenExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Manifest '{manifest.Path}' contains forbidden package file '{file}'.");
        }

        string propsPath = Path.Combine(AdapterRoot(manifest), "AdapterVersion.props");
        string projectPath = Directory.EnumerateFiles(AdapterRoot(manifest), "*.csproj", SearchOption.AllDirectories)
            .SingleOrDefault(path => File.ReadAllText(path).Contains($"<AssemblyName>{Path.GetFileNameWithoutExtension(manifest.EntryAssembly)}</AssemblyName>", StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Could not find the entry project for '{manifest.Slug}'.");
        string adapterVersion = XDocument.Load(propsPath).Descendants("AdapterVersion").SingleOrDefault()?.Value ?? throw new InvalidOperationException($"AdapterVersion.props is invalid for '{manifest.Slug}'.");
        if (!string.Equals(adapterVersion, manifest.AdapterVersion, StringComparison.Ordinal)) throw new InvalidOperationException($"Manifest version and AdapterVersion.props disagree for '{manifest.Slug}'.");
        string sdkVersion = XDocument.Load(Path.Combine(root, "src", "Kontrol.Sdk", "Versions.props")).Descendants("KontrolSdkVersion").SingleOrDefault()?.Value ?? throw new InvalidOperationException("src/Kontrol.Sdk/Versions.props is invalid.");
        if (!string.Equals(sdkVersion, manifest.SdkVersion, StringComparison.Ordinal)) throw new InvalidOperationException($"Manifest SDK version and src/Kontrol.Sdk/Versions.props disagree for '{manifest.Slug}'.");

        string? projectManifest = Directory.EnumerateFiles(AdapterRoot(manifest), "adapter.manifest.json", SearchOption.AllDirectories).FirstOrDefault();
        if (projectManifest != null)
        {
            var pDoc = JsonNode.Parse(File.ReadAllText(projectManifest))?.AsObject();
            string? pVer = pDoc?["version"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(pVer) && !string.Equals(pVer, manifest.AdapterVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"adapter.manifest.json version '{pVer}' does not match package.json version '{manifest.AdapterVersion}' for '{manifest.Slug}'.");
            }
        }
        _ = projectPath;
    }

    private static void ValidateCompatibilityRecord(string path, IReadOnlyList<AdapterManifest> manifests)
    {
        var document = JsonNode.Parse(File.ReadAllText(path))?.AsObject() ?? throw new InvalidOperationException($"Compatibility record '{path}' is invalid JSON.");
        if (document["schemaVersion"]?.GetValue<int>() != 1) throw new InvalidOperationException($"Compatibility record '{path}' must use schemaVersion 1.");
        string adapterId = document["adapterId"]?.GetValue<string>() ?? throw new InvalidOperationException($"Compatibility record '{path}' is missing adapterId.");
        string adapterVersion = document["adapterVersion"]?.GetValue<string>() ?? throw new InvalidOperationException($"Compatibility record '{path}' is missing adapterVersion.");
        AdapterManifest manifest = manifests.SingleOrDefault(item => string.Equals(item.AdapterId, adapterId, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"Compatibility record '{path}' references an unknown adapter.");
        if (!string.Equals(manifest.AdapterVersion, adapterVersion, StringComparison.Ordinal)) throw new InvalidOperationException($"Compatibility record '{path}' references a different adapter version.");
        var game = document["game"]?.AsObject() ?? throw new InvalidOperationException($"Compatibility record '{path}' is missing game metadata.");
        var assemblies = game["relevantAssemblies"]?.AsObject() ?? throw new InvalidOperationException($"Compatibility record '{path}' is missing relevant assemblies.");
        if (assemblies.Count == 0) throw new InvalidOperationException($"Compatibility record '{path}' contains no relevant assemblies.");
        foreach ((string name, JsonNode? value) in assemblies)
        {
            var assembly = value?.AsObject() ?? throw new InvalidOperationException($"Compatibility record '{path}' has invalid evidence for '{name}'.");
            string? hash = assembly["sha256"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(hash) || !Regex.IsMatch(hash, "^[A-Fa-f0-9]{64}$")) throw new InvalidOperationException($"Compatibility record '{path}' has invalid SHA-256 for '{name}'.");
        }
    }

    private static bool FingerprintsMatch(JsonObject requiredAssemblies, JsonObject inspectedAssemblies)
    {
        foreach ((string name, JsonNode? requiredNode) in requiredAssemblies)
        {
            var required = requiredNode?.AsObject();
            var inspected = inspectedAssemblies[name]?.AsObject();
            if (required is null || inspected is null) return false;
            if (!string.Equals(required["sha256"]?.GetValue<string>(), inspected["sha256"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase)) return false;
            string? requiredMvid = required["mvid"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(requiredMvid) && !string.Equals(requiredMvid, inspected["mvid"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    public static string NormalizeSlug(string slug) => slug switch
    {
        "spaceengineers2" => "space-engineers-2",
        "dummyadapter" => "dummy-adapter",
        _ => slug
    };

    private static string AdapterFolder(string slug) => slug switch
    {
        "space-engineers-2" or "spaceengineers2" => "SpaceEngineers2",
        "dummy-adapter" or "dummyadapter" => "DummyAdapter",
        _ => slug
    };
}

public static class AdapterPackage
{
    public const string OfficialKeyId = "kontrol-p256-2026-01";
    public const string SignatureAlgorithm = "ECDSA-P256-SHA256";
    // ZIP's DOS timestamp format cannot represent dates before 1980.
    private static readonly DateTimeOffset PackageTimestamp = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Create(string root, string slug, string configuration, string outputPath, bool overwrite = false)
    {
        AdapterManifest manifest = AdapterRepository.GetManifest(root, slug);
        AdapterRepository.ValidateAdapter(root, slug);
        string adapterRoot = AdapterRepository.AdapterRoot(manifest);
        string outputDirectory = Path.Combine(adapterRoot, Path.GetFileNameWithoutExtension(manifest.EntryAssembly), "bin", configuration, manifest.TargetFramework);
        if (!Directory.Exists(outputDirectory)) throw new DirectoryNotFoundException($"Build output was not found: {outputDirectory}");
        string destination = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? throw new InvalidOperationException("Package output directory is invalid."));
        if (File.Exists(destination) && !overwrite)
            throw new IOException($"Package destination already exists: {destination}. Use --overwrite true only for an unpublished local package.");
        if (File.Exists(destination)) File.Delete(destination);

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["package.json"] = manifest.Path
        };
        foreach (string include in manifest.PackageFiles)
        {
            // Every package redistributes Kontrol SDK code and must therefore
            // carry the repository Apache-2.0 license. Other declared files
            // are adapter runtime outputs, except an adapter-specific notice.
            string source = include switch
            {
                "LICENSE" => Path.Combine(root, "LICENSE"),
                "THIRD_PARTY_NOTICES.md" => Path.Combine(adapterRoot, "THIRD_PARTY_NOTICES.md"),
                _ => Path.Combine(outputDirectory, include)
            };
            if (!File.Exists(source)) throw new FileNotFoundException($"Required package file is missing: {source}");
            files[include] = source;
        }

        var checksums = new JsonObject();
        foreach ((string packagePath, string source) in files.OrderBy(item => item.Key, StringComparer.Ordinal)) checksums[packagePath] = AdapterRepository.HashFile(source);
        byte[] checksumBytes = JsonSerializer.SerializeToUtf8Bytes(new JsonObject { ["algorithm"] = "SHA-256", ["files"] = checksums }, new JsonSerializerOptions { WriteIndented = true });

        using (var archive = ZipFile.Open(destination, ZipArchiveMode.Create))
        {
            foreach ((string packagePath, string source) in files.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                ZipArchiveEntry entry = archive.CreateEntry(packagePath, CompressionLevel.Optimal);
                entry.LastWriteTime = PackageTimestamp;
                using Stream target = entry.Open();
                using Stream input = File.OpenRead(source);
                input.CopyTo(target);
            }
            ZipArchiveEntry checksumEntry = archive.CreateEntry("checksums.json", CompressionLevel.Optimal);
            checksumEntry.LastWriteTime = PackageTimestamp;
            using Stream checksumStream = checksumEntry.Open();
            checksumStream.Write(checksumBytes);
        }
        Verify(destination);
    }

    public static void Sign(string packagePath, string privateKey, string keyId)
    {
        Verify(packagePath);
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Update);
        if (archive.GetEntry("signature.json") is not null)
            throw new InvalidOperationException("Package is already signed; published packages are immutable.");
        PackageSignatureInput input = ReadSignatureInput(archive);
        string payload = CreateSignaturePayload(input);
        var signature = new JsonObject
        {
            ["signatureVersion"] = 1,
            ["keyId"] = keyId,
            ["algorithm"] = SignatureAlgorithm,
            ["signature"] = AdapterSignature.Sign(payload, privateKey)
        };
        ZipArchiveEntry entry = archive.CreateEntry("signature.json", CompressionLevel.Optimal);
        entry.LastWriteTime = PackageTimestamp;
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(signature.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void Verify(string packagePath, string? publicKey = null, bool requireSignature = false)
    {
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        var entries = archive.Entries.ToDictionary(entry => entry.FullName, StringComparer.OrdinalIgnoreCase);
        if (!entries.TryGetValue("package.json", out ZipArchiveEntry? manifestEntry) || !entries.TryGetValue("checksums.json", out ZipArchiveEntry? checksumEntry)) throw new InvalidOperationException("Package must contain package.json and checksums.json.");
        JsonObject manifest;
        using (var reader = new StreamReader(manifestEntry.Open())) manifest = ParseObject(reader.ReadToEnd(), "Package manifest");
        var expected = manifest["package"]?["include"]?.AsArray().Select(node => node?.GetValue<string>() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? throw new InvalidOperationException("Package manifest has no include list.");
        expected.Add("package.json");
        expected.Add("checksums.json");
        bool hasSignature = entries.ContainsKey("signature.json");
        if (hasSignature) expected.Add("signature.json");
        if (!entries.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(expected)) throw new InvalidOperationException("Package contents do not match the manifest allowlist.");
        using var checksumReader = new StreamReader(checksumEntry.Open());
        var checksums = ParseObject(checksumReader.ReadToEnd(), "Package checksums")["files"]?.AsObject() ?? throw new InvalidOperationException("Package checksums are invalid.");
        if (!checksums.Select(item => item.Key).ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(expected.Where(path => !string.Equals(path, "checksums.json", StringComparison.OrdinalIgnoreCase) && !string.Equals(path, "signature.json", StringComparison.OrdinalIgnoreCase))))
            throw new InvalidOperationException("Package checksums do not cover exactly the package files.");
        foreach ((string packagePathName, JsonNode? expectedHash) in checksums)
        {
            if (!entries.TryGetValue(packagePathName, out ZipArchiveEntry? entry)) throw new InvalidOperationException($"Checksum references missing package file '{packagePathName}'.");
            using Stream stream = entry.Open();
            string actual = Convert.ToHexString(SHA256.HashData(stream));
            if (!string.Equals(actual, expectedHash?.GetValue<string>(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Checksum failed for '{packagePathName}'.");
        }
        if (requireSignature && !hasSignature) throw new InvalidOperationException("Package is not signed.");
        if (hasSignature && !string.IsNullOrWhiteSpace(publicKey))
        {
            PackageSignatureInput input = ReadSignatureInput(archive);
            JsonObject signature = ParseObject(new StreamReader(entries["signature.json"].Open()).ReadToEnd(), "Package signature");
            if (signature["signatureVersion"]?.GetValue<int>() != 1 ||
                !string.Equals(signature["keyId"]?.GetValue<string>(), OfficialKeyId, StringComparison.Ordinal) ||
                !string.Equals(signature["algorithm"]?.GetValue<string>(), SignatureAlgorithm, StringComparison.Ordinal) ||
                !AdapterSignature.Verify(CreateSignaturePayload(input), signature["signature"]?.GetValue<string>() ?? string.Empty, publicKey))
                throw new InvalidOperationException("Package signature is invalid.");
        }
        else if (requireSignature && string.IsNullOrWhiteSpace(publicKey)) throw new InvalidOperationException("A public key is required to verify the package signature.");
    }

    private sealed record PackageSignatureInput(string AdapterId, string Slug, string AdapterVersion, string SdkVersion, IReadOnlyList<string> RuntimeFiles, IReadOnlyDictionary<string, string> Checksums);

    private static PackageSignatureInput ReadSignatureInput(ZipArchive archive)
    {
        JsonObject manifest = ParseObject(new StreamReader(archive.GetEntry("package.json")!.Open()).ReadToEnd(), "Package manifest");
        JsonObject checksums = ParseObject(new StreamReader(archive.GetEntry("checksums.json")!.Open()).ReadToEnd(), "Package checksums")["files"]?.AsObject() ?? throw new InvalidOperationException("Package checksums are invalid.");
        string Required(string name) => manifest[name]?.GetValue<string>() ?? throw new InvalidOperationException($"Package manifest is missing '{name}'.");
        var runtime = manifest["package"]?["include"]?.AsArray().Select(item => item?.GetValue<string>() ?? throw new InvalidOperationException("Package manifest allowlist is invalid.")).ToArray() ?? throw new InvalidOperationException("Package manifest has no allowlist.");
        return new PackageSignatureInput(Required("adapterId"), Required("slug"), Required("adapterVersion"), Required("sdkVersion"), runtime, checksums.ToDictionary(item => item.Key, item => item.Value?.GetValue<string>() ?? throw new InvalidOperationException("Package checksum is invalid."), StringComparer.OrdinalIgnoreCase));
    }

    private static string CreateSignaturePayload(PackageSignatureInput input) =>
        $"PACKAGE:1:{input.AdapterId}:{input.Slug}:{input.AdapterVersion}:{input.SdkVersion}:ALLOWLIST:{string.Join(";", input.RuntimeFiles.OrderBy(file => file, StringComparer.Ordinal).Select(file => file.ToLowerInvariant()))}:HASHES:{string.Join(";", input.Checksums.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{item.Key}={item.Value.ToUpperInvariant()}"))}";

    private static JsonObject ParseObject(string json, string description)
    {
        try
        {
            return JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException($"{description} is invalid.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{description} is invalid.", ex);
        }
    }
}

public static class AdapterRelease
{
    private static readonly Regex Sha256 = new("^[A-Fa-f0-9]{64}$", RegexOptions.Compiled);

    public static void Create(string root, string slug, string packagePath, string packageUrl, string tag, string commit, string outputPath, string channel, bool overwrite = false, string? publishedAtUtc = null)
    {
        AdapterManifest manifest = AdapterRepository.GetManifest(root, slug);
        AdapterPackage.Verify(packagePath);
        string expectedTag = $"adapters/{manifest.Slug}/v{manifest.AdapterVersion}";
        if (!string.Equals(tag, expectedTag, StringComparison.Ordinal)) throw new InvalidOperationException($"Release tag must be '{expectedTag}'.");
        if (!Uri.TryCreate(packageUrl, UriKind.Absolute, out _)) throw new InvalidOperationException("Package URL must be absolute.");
        if (!Regex.IsMatch(commit, "^[0-9a-fA-F]{7,64}$")) throw new InvalidOperationException("Source commit must be a Git SHA.");
        string expectedFileName = $"kontrol-adapter-{manifest.Slug}-{manifest.AdapterVersion}-win-x64.zip";
        if (!string.Equals(Path.GetFileName(packagePath), expectedFileName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Package filename must be '{expectedFileName}'.");
        ValidateChannel(channel, manifest.AdapterVersion);

        string timestamp;
        if (!string.IsNullOrWhiteSpace(publishedAtUtc))
        {
            if (!DateTimeOffset.TryParse(publishedAtUtc, out var parsed))
                throw new InvalidOperationException("Release published-at must be an ISO-8601 timestamp.");
            timestamp = parsed.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        }
        else
        {
            timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        var descriptor = new JsonObject
        {
            ["descriptorVersion"] = 1,
            ["adapterId"] = manifest.AdapterId,
            ["slug"] = manifest.Slug,
            ["adapterVersion"] = manifest.AdapterVersion,
            ["sdkVersion"] = manifest.SdkVersion,
            ["channel"] = channel,
            ["publishedAtUtc"] = timestamp,
            ["source"] = new JsonObject { ["tag"] = tag, ["commit"] = commit.ToLowerInvariant() },
            ["package"] = new JsonObject
            {
                ["fileName"] = expectedFileName,
                ["sha256"] = AdapterRepository.HashFile(packagePath),
                ["url"] = packageUrl,
                ["architecture"] = "x64"
            }
        };

        if (!string.IsNullOrWhiteSpace(manifest.GameProductVersion))
        {
            descriptor["gameProductVersion"] = manifest.GameProductVersion;
        }

        string compatibilityRoot = Path.Combine(AdapterRepository.AdapterRoot(manifest), "compatibility", "game-builds");
        if (Directory.Exists(compatibilityRoot))
        {
            var verifiedVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            JsonObject? latestAssembliesNode = null;

            foreach (string recordPath in Directory.EnumerateFiles(compatibilityRoot, "*.json"))
            {
                var record = JsonNode.Parse(File.ReadAllText(recordPath))?.AsObject();
                if (record is null || !string.Equals(record["adapterVersion"]?.GetValue<string>(), manifest.AdapterVersion, StringComparison.Ordinal)) continue;
                if (!string.Equals(record["validation"]?["result"]?.GetValue<string>(), "tested", StringComparison.OrdinalIgnoreCase)) continue;

                var game = record["game"]?.AsObject();
                if (game?["productVersion"]?.GetValue<string>() is { } gpv && !string.IsNullOrWhiteSpace(gpv))
                {
                    verifiedVersions.Add(gpv);
                }

                if (latestAssembliesNode is null && game?["relevantAssemblies"] is JsonObject assembliesObj && assembliesObj.Count > 0)
                {
                    latestAssembliesNode = new JsonObject();
                    foreach ((string name, JsonNode? val) in assembliesObj)
                    {
                        if (val is JsonObject entryObj)
                        {
                            latestAssembliesNode[name] = new JsonObject
                            {
                                ["sha256"] = entryObj["sha256"]?.GetValue<string>() ?? "",
                                ["fileVersion"] = entryObj["fileVersion"]?.GetValue<string>() ?? ""
                            };
                        }
                    }
                }
            }

            if (latestAssembliesNode != null)
            {
                descriptor["assemblies"] = latestAssembliesNode;
            }

            if (verifiedVersions.Count > 0)
            {
                descriptor["gameProductVersion"] = verifiedVersions.First();
                var arr = new JsonArray();
                foreach (string v in verifiedVersions)
                {
                    arr.Add(v);
                }
                descriptor["verifiedGameVersions"] = arr;
            }
        }

        WriteJson(outputPath, descriptor, overwrite);
        Validate(outputPath, packagePath);
    }

    public static JsonObject ReadAndValidate(string descriptorPath, string? packagePath = null)
    {
        Validate(descriptorPath, packagePath);
        return JsonNode.Parse(File.ReadAllText(descriptorPath))!.AsObject();
    }

    public static void Validate(string descriptorPath, string? packagePath = null)
    {
        var descriptor = JsonNode.Parse(File.ReadAllText(descriptorPath))?.AsObject() ?? throw new InvalidOperationException("Release descriptor JSON is invalid.");
        string Required(string name) => descriptor[name]?.GetValue<string>() ?? throw new InvalidOperationException($"Release descriptor is missing '{name}'.");
        if (descriptor["descriptorVersion"]?.GetValue<int>() != 1) throw new InvalidOperationException("Release descriptor must use descriptorVersion 1.");
        string slug = Required("slug"), version = Required("adapterVersion"), channel = Required("channel");
        if (!Regex.IsMatch(slug, "^[a-z0-9][a-z0-9-]*$") || !Regex.IsMatch(version, "^[0-9]+\\.[0-9]+\\.[0-9]+(?:-[0-9A-Za-z.-]+)?$")) throw new InvalidOperationException("Release descriptor has an invalid adapter identity.");
        ValidateChannel(channel, version);
        if (descriptor["publishedAtUtc"]?.GetValue<string>() is { } pub && !DateTimeOffset.TryParse(pub, out _))
            throw new InvalidOperationException("Release descriptor publishedAtUtc is invalid.");
        var source = descriptor["source"]?.AsObject() ?? throw new InvalidOperationException("Release descriptor is missing source metadata.");
        if (!string.Equals(source["tag"]?.GetValue<string>(), $"adapters/{slug}/v{version}", StringComparison.Ordinal) || !Regex.IsMatch(source["commit"]?.GetValue<string>() ?? string.Empty, "^[0-9a-fA-F]{7,64}$")) throw new InvalidOperationException("Release descriptor source metadata is invalid.");
        var package = descriptor["package"]?.AsObject() ?? throw new InvalidOperationException("Release descriptor is missing package metadata.");
        string expectedFile = $"kontrol-adapter-{slug}-{version}-win-x64.zip";
        if (!string.Equals(package["fileName"]?.GetValue<string>(), expectedFile, StringComparison.Ordinal) || !Sha256.IsMatch(package["sha256"]?.GetValue<string>() ?? string.Empty) || !Uri.TryCreate(package["url"]?.GetValue<string>(), UriKind.Absolute, out _) || package["architecture"]?.GetValue<string>() != "x64") throw new InvalidOperationException("Release descriptor package metadata is invalid.");
        if (packagePath is null) return;
        AdapterPackage.Verify(packagePath);
        if (!string.Equals(Path.GetFileName(packagePath), expectedFile, StringComparison.OrdinalIgnoreCase) || !string.Equals(AdapterRepository.HashFile(packagePath), package["sha256"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Release package does not match its descriptor.");
    }

    public static void Sign(string descriptorPath, string privateKey)
    {
        Validate(descriptorPath);
        var descriptor = JsonNode.Parse(File.ReadAllText(descriptorPath))!.AsObject();
        descriptor.Remove("signature");
        descriptor["signature"] = AdapterSignature.Sign(AdapterSignature.CreateCanonicalJsonPayload(descriptor), privateKey);
        File.WriteAllText(descriptorPath, descriptor.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
        Validate(descriptorPath);
    }

    private static void WriteJson(string outputPath, JsonObject document, bool overwrite = false)
    {
        string fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Output path is invalid."));
        if (!overwrite && File.Exists(fullPath)) throw new IOException($"Release descriptor destination already exists: {fullPath}.");
        File.WriteAllText(fullPath, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
    }

    public static void UpdateDescriptors(string root, string releasesDirectory)
    {
        AdapterRepository.ValidateRepository(root);
        if (!Directory.Exists(releasesDirectory)) throw new DirectoryNotFoundException($"Release descriptor directory was not found: {releasesDirectory}");
        var manifestsBySlug = AdapterRepository.GetManifests(root).ToDictionary(manifest => manifest.Slug, StringComparer.OrdinalIgnoreCase);

        foreach (string descriptorPath in Directory.EnumerateFiles(releasesDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            var descriptor = JsonNode.Parse(File.ReadAllText(descriptorPath))?.AsObject();
            if (descriptor is null) continue;

            string? slug = descriptor["slug"]?.GetValue<string>();
            string? adapterVersion = descriptor["adapterVersion"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(adapterVersion)) continue;
            string normalizedSlug = AdapterRepository.NormalizeSlug(slug);
            if (!manifestsBySlug.TryGetValue(normalizedSlug, out AdapterManifest? manifest)) continue;

            string compatibilityRoot = Path.Combine(AdapterRepository.AdapterRoot(manifest), "compatibility", "game-builds");
            if (!Directory.Exists(compatibilityRoot)) continue;

            var verifiedVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (descriptor["verifiedGameVersions"] is JsonArray existingVersions)
            {
                foreach (var v in existingVersions)
                {
                    if (v?.GetValue<string>() is { } s && !string.IsNullOrWhiteSpace(s))
                        verifiedVersions.Add(s);
                }
            }
            if (descriptor["gameProductVersion"]?.GetValue<string>() is { } baseGpv && !string.IsNullOrWhiteSpace(baseGpv))
            {
                verifiedVersions.Add(baseGpv);
            }

            JsonObject? latestAssembliesNode = descriptor["assemblies"]?.AsObject()?.DeepClone().AsObject();

            foreach (string recordPath in Directory.EnumerateFiles(compatibilityRoot, "*.json"))
            {
                var record = JsonNode.Parse(File.ReadAllText(recordPath))?.AsObject();
                if (record is null) continue;
                if (!string.Equals(record["adapterVersion"]?.GetValue<string>(), adapterVersion, StringComparison.Ordinal)) continue;
                if (!string.Equals(record["validation"]?["result"]?.GetValue<string>(), "tested", StringComparison.OrdinalIgnoreCase)) continue;

                var game = record["game"]?.AsObject();
                if (game?["productVersion"]?.GetValue<string>() is { } gpv && !string.IsNullOrWhiteSpace(gpv))
                {
                    verifiedVersions.Add(gpv);
                }

                if (game?["relevantAssemblies"] is JsonObject assembliesObj && assembliesObj.Count > 0)
                {
                    latestAssembliesNode ??= new JsonObject();
                    foreach ((string name, JsonNode? val) in assembliesObj)
                    {
                        if (val is JsonObject entryObj)
                        {
                            latestAssembliesNode[name] = entryObj.DeepClone();
                        }
                    }
                }
            }

            if (verifiedVersions.Count > 0)
            {
                var arr = new JsonArray();
                foreach (string v in verifiedVersions.OrderByDescending(v => v, StringComparer.OrdinalIgnoreCase))
                {
                    arr.Add(v);
                }
                descriptor["verifiedGameVersions"] = arr;
            }

            if (latestAssembliesNode is not null && latestAssembliesNode.Count > 0)
            {
                descriptor["assemblies"] = latestAssembliesNode;
            }

            File.WriteAllText(descriptorPath, descriptor.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
            Validate(descriptorPath);
        }
    }

    private static void ValidateChannel(string channel, string version)
    {
        bool isPrerelease = version.Contains('-', StringComparison.Ordinal);
        if (channel is not ("stable" or "beta")) throw new InvalidOperationException("Release channel must be 'stable' or 'beta'.");
        if (channel == "stable" && isPrerelease) throw new InvalidOperationException("Stable releases must use a non-prerelease semantic version.");
        if (channel == "beta" && !isPrerelease) throw new InvalidOperationException("Beta releases must use a prerelease semantic version such as 1.2.0-beta.1.");
    }
}

public static class AdapterCatalog
{
    public static void Build(string root, string releasesDirectory, string generatedAtUtc, string outputPath)
    {
        AdapterRepository.ValidateRepository(root);
        if (!DateTimeOffset.TryParse(generatedAtUtc, out _)) throw new InvalidOperationException("Catalog generated-at must be an ISO-8601 timestamp.");
        if (!Directory.Exists(releasesDirectory)) throw new DirectoryNotFoundException($"Release descriptor directory was not found: {releasesDirectory}");
        var descriptors = Directory.EnumerateFiles(releasesDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path => AdapterRelease.ReadAndValidate(path)).ToArray();
        if (descriptors.Length == 0) throw new InvalidOperationException("A catalog requires at least one release descriptor.");
        var manifestsBySlug = AdapterRepository.GetManifests(root).ToDictionary(manifest => manifest.Slug, StringComparer.OrdinalIgnoreCase);
        var adapters = new JsonArray();
        foreach (var group in descriptors.GroupBy(item => AdapterRepository.NormalizeSlug(item["slug"]!.GetValue<string>()), StringComparer.Ordinal).OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (!manifestsBySlug.TryGetValue(group.Key, out AdapterManifest? manifest))
                throw new InvalidOperationException($"Catalog release descriptors reference unknown adapter '{group.Key}'.");
            var ordered = group.OrderByDescending(item => Version.Parse(item["adapterVersion"]!.GetValue<string>().Split('-')[0]))
                .ThenByDescending(item => item["channel"]?.GetValue<string>() == "stable")
                .ThenByDescending(PrereleaseNumber)
                .ToArray();
            var current = ordered.FirstOrDefault(item => item["channel"]?.GetValue<string>() == "stable") ?? ordered[0];
            var releases = new JsonArray();
            for (int index = 0; index < ordered.Length; index++)
            {
                var release = ordered[index].DeepClone().AsObject();
                release["status"] = ReferenceEquals(ordered[index], current) ? "current" : "superseded";
                releases.Add(release);
            }
            adapters.Add(new JsonObject
            {
                ["adapterId"] = manifest.AdapterId,
                ["slug"] = manifest.Slug,
                ["displayName"] = manifest.DisplayName,
                ["currentVersion"] = current["adapterVersion"]!.GetValue<string>(),
                ["releases"] = releases
            });
        }
        var catalog = new JsonObject { ["catalogVersion"] = 1, ["generatedAtUtc"] = generatedAtUtc, ["adapters"] = adapters };
        string fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Catalog output path is invalid."));
        File.WriteAllText(fullPath, catalog.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
        Validate(fullPath);
    }

    private static int PrereleaseNumber(JsonNode item)
    {
        string version = item["adapterVersion"]!.GetValue<string>();
        Match match = Regex.Match(version, @"(?:^|\.)(\d+)$");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    public static void Validate(string catalogPath)
    {
        var catalog = JsonNode.Parse(File.ReadAllText(catalogPath))?.AsObject() ?? throw new InvalidOperationException("Catalog JSON is invalid.");
        if (catalog["catalogVersion"]?.GetValue<int>() != 1 || !DateTimeOffset.TryParse(catalog["generatedAtUtc"]?.GetValue<string>(), out _) || catalog["adapters"] is not JsonArray adapters || adapters.Count == 0) throw new InvalidOperationException("Catalog does not use schema version 1.");
        foreach (var adapter in adapters.Select(item => item?.AsObject() ?? throw new InvalidOperationException("Catalog contains an invalid adapter.")))
        {
            if (adapter["releases"] is not JsonArray releases || releases.Count == 0 || adapter["currentVersion"] is null) throw new InvalidOperationException("Catalog adapter has no releases.");
            if (releases.Count(item => item?["status"]?.GetValue<string>() == "current") != 1 || releases.Any(item => item?["status"]?.GetValue<string>() is not ("current" or "superseded") || item?["channel"]?.GetValue<string>() is not ("stable" or "beta"))) throw new InvalidOperationException("Catalog release channels or statuses are invalid.");
        }
    }

    public static void Sign(string catalogPath, string privateKey)
    {
        Validate(catalogPath);
        var catalog = JsonNode.Parse(File.ReadAllText(catalogPath))!.AsObject();
        catalog.Remove("signature");
        catalog["signature"] = AdapterSignature.Sign(AdapterSignature.CreateCanonicalJsonPayload(catalog), privateKey);
        File.WriteAllText(catalogPath, catalog.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine, new UTF8Encoding(false));
        Validate(catalogPath);
    }
}

public static class AdapterSignature
{
    public static string CreateCanonicalJsonPayload(JsonNode document) => $"JSON:1:{Canonicalize(document)}";
    public static string CreatePayload(string adapterId, string slug, string adapterVersion, string sdkVersion, string sha256)
    {
        return $"{adapterId}:{slug}:{adapterVersion}:{sdkVersion}:{sha256.ToUpperInvariant()}";
    }

    public static string Sign(string payload, string privateKeyPemOrBase64)
    {
        byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var ecdsa = ECDsa.Create();
        if (privateKeyPemOrBase64.Contains("---BEGIN"))
            ecdsa.ImportFromPem(privateKeyPemOrBase64);
        else
            ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyPemOrBase64.Trim()), out _);
        
        byte[] signature = ecdsa.SignData(payloadBytes, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(signature);
    }

    public static bool Verify(string payload, string signatureBase64, string publicKeyPemOrBase64)
    {
        try
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            byte[] signatureBytes = Convert.FromBase64String(signatureBase64.Trim());
            using var ecdsa = ECDsa.Create();
            if (publicKeyPemOrBase64.Contains("---BEGIN"))
                ecdsa.ImportFromPem(publicKeyPemOrBase64);
            else
                ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyPemOrBase64.Trim()), out _);

            return ecdsa.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256);
        }
        catch
        {
            return false;
        }
    }

    private static string Canonicalize(JsonNode? node) => node switch
    {
        JsonObject obj => $"{{{string.Join(",", obj.OrderBy(item => item.Key, StringComparer.Ordinal).Select(item => $"{JsonSerializer.Serialize(item.Key)}:{Canonicalize(item.Value)}"))}}}",
        JsonArray array => $"[{string.Join(",", array.Select(Canonicalize))}]",
        JsonValue value => value.ToJsonString(),
        null => "null",
        _ => throw new InvalidOperationException("Unsupported JSON node.")
    };
}
