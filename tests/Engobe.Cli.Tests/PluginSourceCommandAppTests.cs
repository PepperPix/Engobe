namespace Engobe.Cli.Tests;

using Engobe.Cli.Tests.Fakes;
using Engobe.Models;
using Engobe.Services;
using Engobe.Tests;

public class PluginSourceCommandAppTests
{
    private const string PackageId = "Engobe.Plugin.Verified";

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task AddAndUpdate_UseRecordedSource(bool local, bool all)
    {
        await using var feed = new FakeNuGetV3Feed();
        var bytes = feed.AddPackage(PackageId, "1.0.0");
        var root = CreateRoot();
        var project = Directory.CreateDirectory(Path.Combine(root, "project")).FullName;
        var localFeed = Directory.CreateDirectory(Path.Combine(root, "feed")).FullName;
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(localFeed, $"{PackageId}.1.0.0.nupkg"), bytes);
            var source = local ? localFeed : feed.Source;
            var expectedSource = local ? "../feed" : feed.Source;
            var defaultClient = new FakeNuGetPluginClient();
            var lockFile = new PluginLockFile();
            var (app, console) = PluginTestApp.Create(defaultClient, lockFile);
            var add = await app.RunAsync(
                ["plugin", "add", PackageId, project, "--source", source, "--version", "1.0.0", "--yes"]);

            await Assert.That(add.ExitCode).IsEqualTo(0);
            var entry = (await lockFile.ReadAsync(project))["verified"];
            await Assert.That(entry.Source).IsEqualTo(expectedSource);
            await Assert.That(entry.Trust).IsEqualTo(PluginTrustLevel.Community);
            await Assert.That(defaultClient.AddCallCount).IsEqualTo(0);
            if (local)
                await Assert.That(console.Output).Contains("without package hash verification");

            bytes = feed.AddPackage(PackageId, "2.0.0");
            await File.WriteAllBytesAsync(Path.Combine(localFeed, $"{PackageId}.2.0.0.nupkg"), bytes);
            var (updateApp, _) = PluginTestApp.Create(defaultClient, lockFile);
            string[] arguments = all
                ? ["plugin", "update", "--all", "verified", project, "--yes"]
                : ["plugin", "update", "verified", project, "--yes"];
            var update = await updateApp.RunAsync(arguments);

            await Assert.That(update.ExitCode).IsEqualTo(0);
            entry = (await lockFile.ReadAsync(project))["verified"];
            await Assert.That(entry.Version).IsEqualTo("2.0.0");
            await Assert.That(entry.Source).IsEqualTo(expectedSource);
            await Assert.That(entry.ContentHash).IsEqualTo(
                PluginContentHasher.ComputeDirectoryHash(Path.Combine(project, "plugins", "verified")));
            await Assert.That(defaultClient.AddCallCount).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Update_InvalidHashFromRecordedSource_PreservesInstalledContentAndLock()
    {
        await using var feed = new FakeNuGetV3Feed();
        feed.AddPackage(PackageId, "1.0.0");
        var root = CreateRoot();
        try
        {
            var lockFile = new PluginLockFile();
            var (app, _) = PluginTestApp.Create(new FakeNuGetPluginClient(), lockFile);
            var add = await app.RunAsync(
                ["plugin", "add", PackageId, root, "--source", feed.Source, "--version", "1.0.0", "--yes"]);
            await Assert.That(add.ExitCode).IsEqualTo(0);
            var before = (await lockFile.ReadAsync(root))["verified"];
            feed.AddPackage(PackageId, "2.0.0", Convert.ToBase64String(new byte[64]));

            var (updateApp, console) = PluginTestApp.Create(new FakeNuGetPluginClient(), lockFile);
            var update = await updateApp.RunAsync(["plugin", "update", "verified", root, "--yes"]);

            await Assert.That(update.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("SHA-512 hash mismatch");
            await Assert.That((await lockFile.ReadAsync(root))["verified"]).IsEqualTo(before);
            await Assert.That(PluginContentHasher.ComputeDirectoryHash(Path.Combine(root, "plugins", "verified")))
                .IsEqualTo(before.ContentHash);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Add_LocalFeedNamedNuget_IsNotConfusedWithDefaultSource()
    {
        await using var feed = new FakeNuGetV3Feed();
        var root = CreateRoot();
        var localFeed = Directory.CreateDirectory(Path.Combine(root, "nuget")).FullName;
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(localFeed, $"{PackageId}.1.0.0.nupkg"),
                feed.AddPackage(PackageId, "1.0.0"));
            var lockFile = new PluginLockFile();
            var (app, _) = PluginTestApp.Create(new FakeNuGetPluginClient(), lockFile);
            var add = await app.RunAsync(
                ["plugin", "add", PackageId, root, "--source", localFeed, "--version", "1.0.0", "--yes"]);

            await Assert.That(add.ExitCode).IsEqualTo(0);
            await Assert.That((await lockFile.ReadAsync(root))["verified"].Source).IsEqualTo("./nuget");
            var (updateApp, _) = PluginTestApp.Create(new FakeNuGetPluginClient(), lockFile);
            await Assert.That((await updateApp.RunAsync(["plugin", "update", "verified", root, "--yes"])).ExitCode)
                .IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Add_LocalSourceWithoutConsent_DoesNotInstall()
    {
        await using var feed = new FakeNuGetV3Feed();
        var root = CreateRoot();
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(root, $"{PackageId}.1.0.0.nupkg"),
                feed.AddPackage(PackageId, "1.0.0"));
            var project = Directory.CreateDirectory(Path.Combine(root, "project")).FullName;
            var (app, console) = PluginTestApp.Create(new FakeNuGetPluginClient(), new PluginLockFile());
            console.Profile.Capabilities.Interactive = false;

            var result = await app.RunAsync(["plugin", "add", PackageId, project, "--source", root]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("Confirmation required");
            await Assert.That(Directory.Exists(Path.Combine(project, "plugins"))).IsFalse();
            await Assert.That(await new PluginLockFile().ReadAsync(project)).IsEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    [Arguments("")]
    [Arguments("https://user:secret@example.test/v3/index.json")]
    [Arguments("https://example.test/v3/index.json?token=secret")]
    [Arguments("ftp://example.test/index.json")]
    public async Task Add_InvalidSource_FailsWithoutLeakingOrWriting(string source)
    {
        var root = CreateRoot();
        try
        {
            var client = new FakeNuGetPluginClient();
            var (app, console) = PluginTestApp.Create(client, new PluginLockFile());
            var result = await app.RunAsync(["plugin", "add", PackageId, root, "--source", source, "--yes"]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("ERROR:");
            await Assert.That(console.Output).DoesNotContain("secret");
            await Assert.That(client.AddCallCount).IsEqualTo(0);
            await Assert.That(await new PluginLockFile().ReadAsync(root)).IsEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Add_CustomSourceWithMissingPackageOrInvalidHash_DoesNotFallBack(bool invalidHash)
    {
        await using var feed = new FakeNuGetV3Feed();
        if (invalidHash)
            feed.AddPackage(PackageId, "1.0.0", Convert.ToBase64String(new byte[64]));
        var root = CreateRoot();
        try
        {
            var client = new FakeNuGetPluginClient();
            var (app, _) = PluginTestApp.Create(client, new PluginLockFile());
            var result = await app.RunAsync(
                ["plugin", "add", PackageId, root, "--version", "1.0.0", "--source", feed.Source, "--yes"]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(client.AddCallCount).IsEqualTo(0);
            await Assert.That(await new PluginLockFile().ReadAsync(root)).IsEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Update_MissingRecordedFeed_FailsAndPreservesLock()
    {
        var root = CreateRoot();
        try
        {
            var lockFile = new PluginLockFile();
            var entry = new PluginLockEntry(PackageId, "1.0.0", "./missing-feed");
            await lockFile.SetAsync(root, "verified", entry);
            var client = new FakeNuGetPluginClient();
            var (app, console) = PluginTestApp.Create(client, lockFile);
            var result = await app.RunAsync(["plugin", "update", "verified", root, "--yes"]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("ERROR:");
            await Assert.That((await lockFile.ReadAsync(root))["verified"]).IsEqualTo(entry);
            await Assert.That(client.AddCallCount).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRoot() =>
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"engobe-source-{Guid.NewGuid():N}")).FullName;
}
