namespace Kiln.Core.Tests.Services;

using System.IO.Compression;
using System.Security.Cryptography;
using Kiln.Services;
using Kiln.Tests;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

public class NuGetPluginClientTests
{
    [Test]
    public async Task AddAsync_VerifiesBase64Sha512ForNormalizedIdentity_AndKeepsDirectoryHashSeparate()
    {
        await using var feed = new FakeNuGetV3Feed();
        var bytes = feed.AddPackage("Kiln.Plugin.Verified", "1.2.3");
        var client = await feed.CreateClientAsync();
        var root = Path.Combine(Path.GetTempPath(), $"kiln-hash-{Guid.NewGuid():N}");
        try
        {
            var result = await client.AddAsync("Kiln.Plugin.Verified", "1.2.3.0", root);

            await Assert.That(result.PackageVerificationWarning).IsNull();
            await Assert.That(feed.Requests["/flat/kiln.plugin.verified/1.2.3/kiln.plugin.verified.1.2.3.nupkg.sha512"]).IsEqualTo(1);
            await Assert.That(result.ContentHash).IsEqualTo(PluginContentHasher.ComputeDirectoryHash(result.InstallPath));
            await Assert.That(result.ContentHash!.Length).IsEqualTo(64);
            await Assert.That(result.ContentHash).IsNotEqualTo(Convert.ToBase64String(SHA512.HashData(bytes)));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task AddAsync_RejectsMismatchBeforeReadingEvenAnInvalidZip(bool invalidZip, bool offline)
    {
        await using var feed = new FakeNuGetV3Feed();
        var bytes = feed.AddPackage("Kiln.Plugin.Verified", "1.2.3");
        feed.SetPackageBytes("Kiln.Plugin.Verified", "1.2.3", invalidZip ? [1, 2, 3] : bytes, Convert.ToBase64String(new byte[64]));
        var client = await feed.CreateClientAsync();
        var root = Path.Combine(Path.GetTempPath(), $"kiln-hash-{Guid.NewGuid():N}");
        if (offline)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.AddAsync("Kiln.Plugin.Verified", "1.2.3", root));
            feed.Offline = true;
        }

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.AddAsync("Kiln.Plugin.Verified", "1.2.3", root));

        await Assert.That(error!.Message).Contains("SHA-512 hash mismatch");
        await Assert.That(error.Message).Contains("Kiln.Plugin.Verified 1.2.3");
        await Assert.That(Directory.Exists(root)).IsFalse();
    }

    [Test]
    public async Task AddAsync_OnlineMissingHash_WarnsAndInstalls()
    {
        await using var feed = new FakeNuGetV3Feed();
        feed.AddPackage("Kiln.Plugin.Verified", "1.2.3", hash: null);
        var client = await feed.CreateClientAsync();
        var root = Path.Combine(Path.GetTempPath(), $"kiln-hash-{Guid.NewGuid():N}");
        try
        {
            var result = await client.AddAsync("Kiln.Plugin.Verified", "1.2.3", root);

            await Assert.That(result.PackageVerificationWarning).Contains(feed.Source);
            await Assert.That(result.PackageVerificationWarning).Contains("Kiln.Plugin.Verified 1.2.3");
            await Assert.That(result.PackageVerificationWarning).Contains("without package hash verification");
            await Assert.That(File.Exists(Path.Combine(result.InstallPath, "plugin.yaml"))).IsTrue();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task AddAsync_OfflineCache_RequiresMatchingCachedHash(bool cacheHash)
    {
        await using var feed = new FakeNuGetV3Feed();
        feed.AddPackage("Kiln.Plugin.Verified", "1.2.3", cacheHash ? "match" : null);
        var client = await feed.CreateClientAsync();
        var root = Path.Combine(Path.GetTempPath(), $"kiln-hash-cache-{Guid.NewGuid():N}");
        try
        {
            await client.AddAsync("Kiln.Plugin.Verified", "1.2.3", Path.Combine(root, "warm"));
            feed.ExpireCache();
            feed.Offline = true;
            client = await feed.CreateClientAsync();
            var target = Path.Combine(root, "offline");
            if (cacheHash)
            {
                var result = await client.AddAsync("Kiln.Plugin.Verified", "1.2.3", target);
                await Assert.That(result.PackageVerificationWarning).IsNull();
                await Assert.That(File.Exists(Path.Combine(result.InstallPath, "plugin.yaml"))).IsTrue();
            }
            else
            {
                var error = await Assert.ThrowsAsync<PluginPackageVerificationException>(
                    () => client.AddAsync("Kiln.Plugin.Verified", "1.2.3", target));
                await Assert.That(error!.Warning).Contains("cannot be verified offline");
                await Assert.That(error.Warning).Contains(feed.Source);
                await Assert.That(Directory.Exists(target)).IsFalse();
            }
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PackageInspection_ReportsMissingHashBeforeOpeningTheArchive(bool info)
    {
        await using var feed = new FakeNuGetV3Feed();
        feed.SetPackageBytes("Kiln.Plugin.Verified", "1.2.3", [1, 2, 3], hash: null);
        var client = await feed.CreateClientAsync();
        var root = Path.Combine(Path.GetTempPath(), $"kiln-warning-order-{Guid.NewGuid():N}");
        string? warning = null;
        var options = new PluginInstallOptions { ReportPackageVerificationWarning = value => warning = value };

        if (info)
            await Assert.ThrowsAsync<InvalidDataException>(() => client.GetInfoAsync("Kiln.Plugin.Verified", "1.2.3", value => warning = value));
        else
            await Assert.ThrowsAsync<InvalidDataException>(() => client.AddAsync("Kiln.Plugin.Verified", "1.2.3", root, options));

        await Assert.That(warning).Contains("without package hash verification");
        await Assert.That(Directory.Exists(root)).IsFalse();
    }

    [Test]
    [Arguments("not-base64")]
    [Arguments("")]
    public async Task AddAsync_InvalidHash_FailsClosed(string hash)
    {
        await using var feed = new FakeNuGetV3Feed();
        feed.AddPackage("Kiln.Plugin.Verified", "1.2.3", hash);
        var client = await feed.CreateClientAsync();
        var root = Path.Combine(Path.GetTempPath(), $"kiln-invalid-hash-{Guid.NewGuid():N}");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.AddAsync("Kiln.Plugin.Verified", "1.2.3", root));

        await Assert.That(error!.Message).Contains("SHA-512");
        await Assert.That(error.Message).Contains("Kiln.Plugin.Verified 1.2.3");
        await Assert.That(Directory.Exists(root)).IsFalse();
    }

    [Test]
    public async Task SearchAsync_FindsKilnPluginPackagesInLocalSource()
    {
        var sourceRoot = CreateLocalPackageSource();
        var client = new NuGetPluginClient(Repository.Factory.GetCoreV3(new PackageSource(sourceRoot)));

        var results = await client.SearchAsync("kiln-plugin");

        await Assert.That(results.Count >= 1).IsTrue();
        await Assert.That(results[0].Id).IsEqualTo("Kiln.Plugin.EmailProtect");
        await Assert.That(results[0].Version).IsEqualTo("1.3.0");
        await Assert.That(results[0].Description).IsEqualTo("Protect emails from scrapers");
    }

    [Test]
    public async Task GetLatestVersionAsync_ResolvesHighestStableVersion()
    {
        var sourceRoot = CreateLocalPackageSource();
        var client = new NuGetPluginClient(Repository.Factory.GetCoreV3(new PackageSource(sourceRoot)));

        var version = await client.GetLatestVersionAsync("Kiln.Plugin.EmailProtect");

        await Assert.That(version).IsEqualTo("1.3.0");
    }

    [Test]
    public async Task GetLatestVersionAsync_WhenPackageDoesNotExist_ReturnsNull()
    {
        var sourceRoot = CreateLocalPackageSource();
        var client = new NuGetPluginClient(Repository.Factory.GetCoreV3(new PackageSource(sourceRoot)));

        var version = await client.GetLatestVersionAsync("Kiln.Plugin.DoesNotExist");

        await Assert.That(version).IsNull();
    }

    [Test]
    public async Task AddAsync_ExtractsPluginContent_IntoPluginsDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"kiln-nuget-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var sourceRoot = CreateLocalPackageSource();
            var client = new NuGetPluginClient(Repository.Factory.GetCoreV3(new PackageSource(sourceRoot)));

            var result = await client.AddAsync("Kiln.Plugin.EmailProtect", null, tempRoot);

            await Assert.That(result.PluginName).IsEqualTo("email-protect");
            await Assert.That(result.PackageId).IsEqualTo("Kiln.Plugin.EmailProtect");
            await Assert.That(result.Version).IsEqualTo("1.3.0");
            await Assert.That(File.Exists(Path.Combine(tempRoot, "plugins", "email-protect", "plugin.yaml"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(tempRoot, "plugins", "email-protect", "static", "plugin.js"))).IsTrue();
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateLocalPackageSource()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kiln-nuget-feed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        CreatePackage(
            root,
            "Kiln.Plugin.EmailProtect",
            "1.2.3",
            "email-protect",
            "Protect emails from scrapers");
        CreatePackage(
            root,
            "Kiln.Plugin.EmailProtect",
            "1.3.0",
            "email-protect",
            "Protect emails from scrapers");

        return root;
    }

    private static void CreatePackage(string root, string id, string version, string pluginName, string description)
    {
        var packagePath = Path.Combine(root, $"{id}.{version}.nupkg");

        using var stream = File.Create(packagePath);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var nuspec = archive.CreateEntry($"{id}.nuspec");
            using (var writer = new StreamWriter(nuspec.Open()))
            {
                writer.Write(
                    $"<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                    "<package><metadata>" +
                    $"<id>{id}</id><version>{version}</version>" +
                    $"<description>{description}</description>" +
                    "<tags>kiln-plugin email privacy</tags>" +
                    "<authors>Test</authors>" +
                    "</metadata></package>");
            }

            var manifest = archive.CreateEntry("content/plugin.yaml");
            using (var writer = new StreamWriter(manifest.Open()))
            {
                writer.Write($"name: {pluginName}\nversion: {version}\ndescription: {description}\n");
            }

            var script = archive.CreateEntry("content/static/plugin.js");
            using (var writer = new StreamWriter(script.Open()))
            {
                writer.Write("console.log('hello');\n");
            }
        }
    }
}
