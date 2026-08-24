using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Shouldly;

namespace Kontrol.AdapterTool.Tests;

public class AdapterToolTests
{
    [Test]
    public void AffectedPaths_SelectOnlyChangedAdapter()
    {
        string root = AdapterRepository.FindRoot(TestContext.CurrentContext.TestDirectory);
        AdapterRepository.GetAffectedAdaptersFromPaths(root, ["src/Adapters/DummyAdapter/README.md"])
            .ShouldBe(["dummy-adapter"]);
    }

    [Test]
    public void SharedSdkChange_SelectsAllAdapters()
    {
        string root = AdapterRepository.FindRoot(TestContext.CurrentContext.TestDirectory);
        var affected = AdapterRepository.GetAffectedAdaptersFromPaths(root, ["src/Kontrol.Sdk/IPC/InputFrame.cs"]);
        affected.ShouldContain("dummy-adapter");
        affected.ShouldContain("space-engineers-2");
    }

    [Test]
    public void RepositoryMetadata_IsValid()
    {
        string root = AdapterRepository.FindRoot(TestContext.CurrentContext.TestDirectory);
        Should.NotThrow(() => AdapterRepository.ValidateRepository(root));
    }

    [Test]
    public void PackageManifests_DeclareRequiredLicenseAndSe2HarmonyNotice()
    {
        string root = AdapterRepository.FindRoot(TestContext.CurrentContext.TestDirectory);
        AdapterManifest se2 = AdapterRepository.GetManifest(root, "space-engineers-2");
        AdapterManifest dummy = AdapterRepository.GetManifest(root, "dummy-adapter");

        se2.PackageFiles.ShouldContain("LICENSE");
        se2.PackageFiles.ShouldContain("THIRD_PARTY_NOTICES.md");
        dummy.PackageFiles.ShouldContain("LICENSE");
        File.Exists(Path.Combine(root, "LICENSE")).ShouldBeTrue();
        File.Exists(Path.Combine(AdapterRepository.AdapterRoot(se2), "THIRD_PARTY_NOTICES.md")).ShouldBeTrue();
    }

    [Test]
    public void PackageVerification_RejectsUnexpectedFile()
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"kontrol-package-{Guid.NewGuid():N}.zip");
        try
        {
            using (ZipArchive archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                archive.CreateEntry("adapter.manifest.json").Open().Dispose();
                archive.CreateEntry("checksums.json").Open().Dispose();
                archive.CreateEntry("unexpected.dll").Open().Dispose();
            }
            Should.Throw<InvalidOperationException>(() => AdapterPackage.Verify(temporary));
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    [Test]
    public void ReleaseDescriptorValidation_AcceptsImmutablePackageBinding()
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"kontrol-release-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(temporary, Descriptor("1.0.0").ToJsonString());
            Should.NotThrow(() => AdapterRelease.Validate(temporary));
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    [Test]
    public void ReleaseDescriptorValidation_RejectsWrongTag()
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"kontrol-release-{Guid.NewGuid():N}.json");
        try
        {
            var descriptor = Descriptor("1.0.0");
            descriptor["source"]!["tag"] = "adapters/dummyadapter/v1.0.1";
            File.WriteAllText(temporary, descriptor.ToJsonString());
            Should.Throw<InvalidOperationException>(() => AdapterRelease.Validate(temporary));
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    [Test]
    public void CatalogValidation_AcceptsCurrentAndSupersededReleases()
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"kontrol-catalog-{Guid.NewGuid():N}.json");
        try
        {
            var oldRelease = Descriptor("1.0.0");
            oldRelease["status"] = "superseded";
            var currentRelease = Descriptor("1.1.0");
            currentRelease["status"] = "current";
            var catalog = new JsonObject
            {
                ["catalogVersion"] = 1,
                ["generatedAtUtc"] = "1970-01-01T00:00:00.0000000+00:00",
                ["adapters"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["adapterId"] = "dummy-adapter",
                        ["slug"] = "dummy-adapter",
                        ["displayName"] = "Kontrol Sandbox",
                        ["currentVersion"] = "1.1.0",
                        ["releases"] = new JsonArray(currentRelease, oldRelease)
                    }
                }
            };
            File.WriteAllText(temporary, catalog.ToJsonString());
            Should.NotThrow(() => AdapterCatalog.Validate(temporary));
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    [Test]
    public void CatalogBuild_CopiesTheCanonicalAdapterDisplayName()
    {
        string root = AdapterRepository.FindRoot(TestContext.CurrentContext.TestDirectory);
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"kontrol-catalog-{Guid.NewGuid():N}");
        string releasesDirectory = Path.Combine(temporaryDirectory, "releases");
        string catalogPath = Path.Combine(temporaryDirectory, "catalog.json");
        Directory.CreateDirectory(releasesDirectory);

        try
        {
            File.WriteAllText(Path.Combine(releasesDirectory, "dummy-adapter-1.0.0.json"), Descriptor("1.0.0").ToJsonString());

            AdapterCatalog.Build(root, releasesDirectory, "1970-01-01T00:00:00.0000000+00:00", catalogPath);

            JsonObject catalog = JsonNode.Parse(File.ReadAllText(catalogPath))!.AsObject();
            JsonObject adapter = catalog["adapters"]!.AsArray()[0]!.AsObject();
            adapter["displayName"]!.GetValue<string>().ShouldBe("Kontrol Sandbox");
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void CatalogBuild_SelectsNewestBetaWhenNoStableReleaseExists()
    {
        string root = AdapterRepository.FindRoot(TestContext.CurrentContext.TestDirectory);
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"kontrol-catalog-{Guid.NewGuid():N}");
        string releasesDirectory = Path.Combine(temporaryDirectory, "releases");
        string catalogPath = Path.Combine(temporaryDirectory, "catalog.json");
        Directory.CreateDirectory(releasesDirectory);

        try
        {
            var beta1 = Descriptor("0.1.0-beta.1");
            beta1["channel"] = "beta";
            var beta2 = Descriptor("0.1.0-beta.2");
            beta2["channel"] = "beta";
            File.WriteAllText(Path.Combine(releasesDirectory, "dummy-adapter-0.1.0-beta.1.json"), beta1.ToJsonString());
            File.WriteAllText(Path.Combine(releasesDirectory, "dummy-adapter-0.1.0-beta.2.json"), beta2.ToJsonString());

            AdapterCatalog.Build(root, releasesDirectory, "1970-01-01T00:00:00.0000000+00:00", catalogPath);

            JsonObject catalog = JsonNode.Parse(File.ReadAllText(catalogPath))!.AsObject();
            JsonObject adapter = catalog["adapters"]!.AsArray()[0]!.AsObject();
            adapter["currentVersion"]!.GetValue<string>().ShouldBe("0.1.0-beta.2");
            adapter["releases"]!.AsArray().Single(item => item!["adapterVersion"]!.GetValue<string>() == "0.1.0-beta.2")?["status"]!.GetValue<string>().ShouldBe("current");
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public void PackageSigning_CreatesAndVerifiesAnOfficialPackageSignature()
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"kontrol-package-{Guid.NewGuid():N}.zip");
        try
        {
            CreateMinimalPackage(temporary);
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            string privateKey = Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey());
            string publicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());

            AdapterPackage.Sign(temporary, privateKey, AdapterPackage.OfficialKeyId);

            Should.NotThrow(() => AdapterPackage.Verify(temporary, publicKey, requireSignature: true));
            using ZipArchive archive = ZipFile.OpenRead(temporary);
            archive.GetEntry("signature.json").ShouldNotBeNull();
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    [Test]
    public void CatalogValidation_AcceptsNewestStableWhenNewerBetaExists()
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"kontrol-catalog-{Guid.NewGuid():N}.json");
        try
        {
            var stable = Descriptor("1.1.0");
            stable["channel"] = "stable";
            stable["status"] = "current";
            var beta = Descriptor("1.2.0-beta.1");
            beta["channel"] = "beta";
            beta["status"] = "superseded";
            var catalog = new JsonObject
            {
                ["catalogVersion"] = 1,
                ["generatedAtUtc"] = "1970-01-01T00:00:00.0000000+00:00",
                ["adapters"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["adapterId"] = "dummy-adapter",
                        ["slug"] = "dummy-adapter",
                        ["displayName"] = "Kontrol Sandbox",
                        ["currentVersion"] = "1.1.0",
                        ["releases"] = new JsonArray(beta, stable)
                    }
                }
            };
            File.WriteAllText(temporary, catalog.ToJsonString());
            Should.NotThrow(() => AdapterCatalog.Validate(temporary));
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    [Test]
    public void ReleaseDescriptorValidation_RejectsStableChannelForPrereleaseVersion()
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"kontrol-release-{Guid.NewGuid():N}.json");
        try
        {
            var descriptor = Descriptor("1.2.0-beta.1");
            File.WriteAllText(temporary, descriptor.ToJsonString());
            Should.Throw<InvalidOperationException>(() => AdapterRelease.Validate(temporary));
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    [Test]
    public void ReleaseDescriptorSigning_AddsVerifiableSignature()
    {
        string temporary = Path.Combine(Path.GetTempPath(), $"kontrol-release-{Guid.NewGuid():N}.json");
        try
        {
            using var ecdsa = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
            string privateKey = Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey());
            string publicKey = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());
            File.WriteAllText(temporary, Descriptor("1.0.0").ToJsonString());

            AdapterRelease.Sign(temporary, privateKey);

            var signed = JsonNode.Parse(File.ReadAllText(temporary))!.AsObject();
            string signature = signed["signature"]!.GetValue<string>();
            signed.Remove("signature");
            AdapterSignature.Verify(AdapterSignature.CreateCanonicalJsonPayload(signed), signature, publicKey).ShouldBeTrue();
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    [Test]
    public void AdapterSignature_SignAndVerify_Succeeds()
    {
        using var ecdsa = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        string privateKeyBase64 = Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey());
        string publicKeyBase64 = Convert.ToBase64String(ecdsa.ExportSubjectPublicKeyInfo());

        string payload = AdapterSignature.CreatePayload("dummy-adapter", "dummy-adapter", "1.0.0", "1.0.0", new string('A', 64));
        string signature = AdapterSignature.Sign(payload, privateKeyBase64);

        signature.ShouldNotBeNullOrWhiteSpace();
        AdapterSignature.Verify(payload, signature, publicKeyBase64).ShouldBeTrue();
        AdapterSignature.Verify(payload + "tampered", signature, publicKeyBase64).ShouldBeFalse();
    }

    [Test]
    public void UpdateDescriptors_RefreshesVerifiedGameVersions_WithoutChangingPackageHash()
    {
        string root = AdapterRepository.FindRoot(TestContext.CurrentContext.TestDirectory);
        string releasesDir = Path.Combine(Path.GetTempPath(), $"kontrol-releases-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(releasesDir);
            var descriptor = Descriptor("0.1.0-beta.3");
            descriptor["channel"] = "beta";
            descriptor["slug"] = "space-engineers-2";
            descriptor["adapterId"] = "space-engineers-2";
            descriptor["gameProductVersion"] = "2.3.0.2798";
            descriptor["source"] = new JsonObject { ["tag"] = "adapters/space-engineers-2/v0.1.0-beta.3", ["commit"] = "abcdef1234567" };
            descriptor["package"] = new JsonObject
            {
                ["fileName"] = "kontrol-adapter-space-engineers-2-0.1.0-beta.3-win-x64.zip",
                ["sha256"] = new string('A', 64),
                ["url"] = "https://example.invalid/download.zip",
                ["architecture"] = "x64"
            };
            string descPath = Path.Combine(releasesDir, "space-engineers-2-0.1.0-beta.3.json");
            File.WriteAllText(descPath, descriptor.ToJsonString());

            AdapterRelease.UpdateDescriptors(root, releasesDir);

            var updated = JsonNode.Parse(File.ReadAllText(descPath))!.AsObject();
            var verified = updated["verifiedGameVersions"]!.AsArray().Select(v => v!.GetValue<string>()).ToArray();
            verified.ShouldContain("2.3.0.2798");
            updated["package"]!["sha256"]!.GetValue<string>().ShouldBe(new string('A', 64));
        }
        finally
        {
            if (Directory.Exists(releasesDir)) Directory.Delete(releasesDir, recursive: true);
        }
    }

    [Test]
    public void EndToEnd_GameUpdateWorkflow_UpdatesCatalogWithoutRePackingAdapterZip()
    {
        string root = AdapterRepository.FindRoot(TestContext.CurrentContext.TestDirectory);
        string tempDir = Path.Combine(Path.GetTempPath(), $"kontrol-e2e-{Guid.NewGuid():N}");
        string releasesDir = Path.Combine(tempDir, "releases");
        string catalogPath = Path.Combine(tempDir, "catalog.json");
        string dummyCompatDir = Path.Combine(root, "src", "Adapters", "DummyAdapter", "compatibility", "game-builds");
        string newCompatRecordPath = Path.Combine(dummyCompatDir, "9.9.9.json");

        try
        {
            Directory.CreateDirectory(releasesDir);
            Directory.CreateDirectory(dummyCompatDir);

            // 1. Existing release descriptor published on gh-pages for DummyAdapter 1.0.0
            var initialDescriptor = Descriptor("1.0.0");
            initialDescriptor["channel"] = "stable";
            initialDescriptor["gameProductVersion"] = "1.0.0";
            initialDescriptor["package"] = new JsonObject
            {
                ["fileName"] = "kontrol-adapter-dummy-adapter-1.0.0-win-x64.zip",
                ["sha256"] = "1111222233334444555566667777888899990000aaaabbbbccccddddeeeeffff",
                ["url"] = "https://example.invalid/download.zip",
                ["architecture"] = "x64"
            };
            string descPath = Path.Combine(releasesDir, "dummy-adapter-1.0.0.json");
            File.WriteAllText(descPath, initialDescriptor.ToJsonString());

            // 2. New game build 9.9.9 compatibility record is added to repository
            File.WriteAllText(newCompatRecordPath, "{\"schemaVersion\":1,\"adapterId\":\"dummy-adapter\",\"slug\":\"dummy-adapter\",\"adapterVersion\":\"1.0.0\",\"game\":{\"product\":\"Dummy Adapter\",\"productVersion\":\"9.9.9\",\"relevantAssemblies\":{\"Kontrol.Sandbox.Game.exe\":{\"fileVersion\":\"9.9.9\",\"sha256\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\",\"mvid\":\"3e9ef3aa-d7a2-41ad-a627-b068f054b48b\"}}},\"validation\":{\"result\":\"tested\"}}");

            // 3. Workflow runs: updates descriptors and rebuilds catalog
            AdapterRelease.UpdateDescriptors(root, releasesDir);
            AdapterCatalog.Build(root, releasesDir, "1970-01-01T00:00:00.0000000+00:00", catalogPath);

            // 4. Validate output
            AdapterCatalog.Validate(catalogPath);

            var catalog = JsonNode.Parse(File.ReadAllText(catalogPath))!.AsObject();
            var adapter = catalog["adapters"]!.AsArray().Single(a => a!["slug"]!.GetValue<string>() == "dummy-adapter")!.AsObject();
            var release = adapter["releases"]!.AsArray().Single(r => r!["adapterVersion"]!.GetValue<string>() == "1.0.0")!.AsObject();

            var verifiedVersions = release["verifiedGameVersions"]!.AsArray().Select(v => v!.GetValue<string>()).ToArray();
            verifiedVersions.ShouldContain("9.9.9");
            verifiedVersions.ShouldContain("1.0.0");

            // Verify package hash was completely preserved without re-packing
            release["package"]!["sha256"]!.GetValue<string>().ShouldBe("1111222233334444555566667777888899990000aaaabbbbccccddddeeeeffff");
        }
        finally
        {
            if (File.Exists(newCompatRecordPath)) File.Delete(newCompatRecordPath);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    private static JsonObject Descriptor(string version) => new()
    {
        ["descriptorVersion"] = 1,
        ["adapterId"] = "dummy-adapter",
        ["slug"] = "dummy-adapter",
        ["adapterVersion"] = version,
        ["sdkVersion"] = "1.0.0",
        ["channel"] = "stable",
        ["source"] = new JsonObject { ["tag"] = $"adapters/dummy-adapter/v{version}", ["commit"] = "abcdef1234567" },
        ["package"] = new JsonObject
        {
            ["fileName"] = $"kontrol-adapter-dummy-adapter-{version}-win-x64.zip",
            ["sha256"] = new string('A', 64),
            ["url"] = "https://example.invalid/download.zip",
            ["architecture"] = "x64"
        }
    };

    private static void CreateMinimalPackage(string path)
    {
        const string manifest = "{\"adapterId\":\"dummy-adapter\",\"slug\":\"dummy-adapter\",\"adapterVersion\":\"1.0.0\",\"sdkVersion\":\"1.0.0\",\"package\":{\"include\":[\"adapter.manifest.json\"]}}";
        const string runtime = "{}";
        var checksums = new JsonObject
        {
            ["algorithm"] = "SHA-256",
            ["files"] = new JsonObject
            {
                ["package.json"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest))),
                ["adapter.manifest.json"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(runtime)))
            }
        };
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "package.json", manifest);
        WriteEntry(archive, "adapter.manifest.json", runtime);
        WriteEntry(archive, "checksums.json", checksums.ToJsonString());
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}

