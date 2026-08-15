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
            .ShouldBe(["dummyadapter"]);
    }

    [Test]
    public void SharedSdkChange_SelectsAllAdapters()
    {
        string root = AdapterRepository.FindRoot(TestContext.CurrentContext.TestDirectory);
        var affected = AdapterRepository.GetAffectedAdaptersFromPaths(root, ["src/Kontrol.Sdk/IPC/InputFrame.cs"]);
        affected.ShouldContain("dummyadapter");
        affected.ShouldContain("spaceengineers2");
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
        AdapterManifest se2 = AdapterRepository.GetManifest(root, "spaceengineers2");
        AdapterManifest dummy = AdapterRepository.GetManifest(root, "dummyadapter");

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
                        ["adapterId"] = "DummyAdapter",
                        ["slug"] = "dummyadapter",
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
            File.WriteAllText(Path.Combine(releasesDirectory, "dummyadapter-1.0.0.json"), Descriptor("1.0.0").ToJsonString());

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
            File.WriteAllText(Path.Combine(releasesDirectory, "dummyadapter-0.1.0-beta.1.json"), beta1.ToJsonString());
            File.WriteAllText(Path.Combine(releasesDirectory, "dummyadapter-0.1.0-beta.2.json"), beta2.ToJsonString());

            AdapterCatalog.Build(root, releasesDirectory, "1970-01-01T00:00:00.0000000+00:00", catalogPath);

            JsonObject catalog = JsonNode.Parse(File.ReadAllText(catalogPath))!.AsObject();
            JsonObject adapter = catalog["adapters"]!.AsArray()[0]!.AsObject();
            adapter["currentVersion"]!.GetValue<string>().ShouldBe("0.1.0-beta.2");
            adapter["releases"]!.AsArray().Single(item => item!["adapterVersion"]!.GetValue<string>() == "0.1.0-beta.2")["status"]!.GetValue<string>().ShouldBe("current");
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
                        ["adapterId"] = "DummyAdapter",
                        ["slug"] = "dummyadapter",
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

        string payload = AdapterSignature.CreatePayload("DummyAdapter", "dummyadapter", "1.0.0", "1.0.0", new string('A', 64));
        string signature = AdapterSignature.Sign(payload, privateKeyBase64);

        signature.ShouldNotBeNullOrWhiteSpace();
        AdapterSignature.Verify(payload, signature, publicKeyBase64).ShouldBeTrue();
        AdapterSignature.Verify(payload + "tampered", signature, publicKeyBase64).ShouldBeFalse();
    }

    private static JsonObject Descriptor(string version) => new()
    {
        ["descriptorVersion"] = 1,
        ["adapterId"] = "DummyAdapter",
        ["slug"] = "dummyadapter",
        ["adapterVersion"] = version,
        ["sdkVersion"] = "1.0.0",
        ["channel"] = "stable",
        ["source"] = new JsonObject { ["tag"] = $"adapters/dummyadapter/v{version}", ["commit"] = "abcdef1234567" },
        ["package"] = new JsonObject
        {
            ["fileName"] = $"kontrol-adapter-dummyadapter-{version}-win-x64.zip",
            ["sha256"] = new string('A', 64),
            ["url"] = "https://example.invalid/download.zip",
            ["architecture"] = "x64"
        }
    };

    private static void CreateMinimalPackage(string path)
    {
        const string manifest = "{\"adapterId\":\"DummyAdapter\",\"slug\":\"dummyadapter\",\"adapterVersion\":\"1.0.0\",\"sdkVersion\":\"1.0.0\",\"package\":{\"include\":[\"adapter.manifest.json\"]}}";
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

