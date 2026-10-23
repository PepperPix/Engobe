namespace Engobe.Core.Tests.Services;

using Engobe.Services;

public class PluginLockFileTests
{
    [Test]
    public async Task SetAsync_CreatesLockFileAndPersistsEntries()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();

            await lockFile.SetAsync(projectDir, "email-protect", new PluginLockEntry(
                "Engobe.Plugin.EmailProtect",
                "1.0.0",
                "nuget"));

            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries).ContainsKey("email-protect");
            await Assert.That(entries["email-protect"].PackageId).IsEqualTo("Engobe.Plugin.EmailProtect");
            await Assert.That(entries["email-protect"].Version).IsEqualTo("1.0.0");

            var json = await File.ReadAllTextAsync(Path.Combine(projectDir, ".engobe", "plugins.lock.json"));
            await Assert.That(json).Contains("\"packageId\": \"Engobe.Plugin.EmailProtect\"");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task SetAsync_UpdatesExistingEntry_WithoutLosingOthers()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "alpha", new PluginLockEntry("Alpha.Plugin", "1.0.0", "nuget"));
            await lockFile.SetAsync(projectDir, "beta", new PluginLockEntry("Beta.Plugin", "2.0.0", "nuget"));
            await lockFile.SetAsync(projectDir, "alpha", new PluginLockEntry("Alpha.Plugin", "1.1.0", "nuget"));

            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries.Count).IsEqualTo(2);
            await Assert.That(entries["alpha"].Version).IsEqualTo("1.1.0");
            await Assert.That(entries["beta"].Version).IsEqualTo("2.0.0");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task RemoveAsync_DeletesEntryAndIsNoOp_WhenMissing()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "gamma", new PluginLockEntry("Gamma.Plugin", "3.0.0", "nuget"));
            await lockFile.RemoveAsync(projectDir, "gamma");
            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries).IsEmpty();

            await lockFile.RemoveAsync(projectDir, "missing");
            var after = await lockFile.ReadAsync(projectDir);
            await Assert.That(after).IsEmpty();
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task ReadAsync_WithLegacyLockFileWithoutHash_ReadsDefaults()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectDir, ".engobe"));

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(projectDir, ".engobe", "plugins.lock.json"),
                "{\"plugins\":{\"email-protect\":{\"packageId\":\"Engobe.Plugin.EmailProtect\",\"version\":\"1.0.0\",\"source\":\"nuget\"}}}");

            var entries = await new PluginLockFile().ReadAsync(projectDir);

            await Assert.That(entries["email-protect"].PackageId).IsEqualTo("Engobe.Plugin.EmailProtect");
            await Assert.That(entries["email-protect"].ContentHash).IsNull();
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

[Test]
public async Task ReadAsync_DoesNotReadLegacyKilnLockFile()
{
    var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-lock-legacy-{Guid.NewGuid():N}");
    var legacyLockPath = Path.Combine(projectDir, ".kiln", "plugins.lock.json");
    Directory.CreateDirectory(Path.GetDirectoryName(legacyLockPath)!);
    const string legacyJson = "{\"plugins\":{\"email-protect\":{\"packageId\":\"Kiln.Plugin.EmailProtect\",\"version\":\"1.1.1\",\"source\":\"nuget\"}}}";
    await File.WriteAllTextAsync(legacyLockPath, legacyJson);

    try
    {
        var entries = await new PluginLockFile().ReadAsync(projectDir);

        await Assert.That(entries).IsEmpty();
        await Assert.That(await File.ReadAllTextAsync(legacyLockPath)).IsEqualTo(legacyJson);
    }
    finally
    {
        Directory.Delete(projectDir, recursive: true);
    }
}

[Test]
public async Task SetAsync_WritesEngobeLockAndLeavesLegacyKilnLockUntouched()
{
    var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-lock-migrate-{Guid.NewGuid():N}");
    var legacyLockPath = Path.Combine(projectDir, ".kiln", "plugins.lock.json");
    Directory.CreateDirectory(Path.GetDirectoryName(legacyLockPath)!);
    const string legacyJson = "{\"plugins\":{\"email-protect\":{\"packageId\":\"Kiln.Plugin.EmailProtect\",\"version\":\"1.1.1\",\"source\":\"nuget\"}}}";
    await File.WriteAllTextAsync(legacyLockPath, legacyJson);

    try
    {
        await new PluginLockFile().SetAsync(
            projectDir,
            "email-protect",
            new PluginLockEntry("Engobe.Plugin.EmailProtect", "2.0.0", "nuget"));

        await Assert.That(File.Exists(Path.Combine(projectDir, ".engobe", "plugins.lock.json"))).IsTrue();
        await Assert.That(await File.ReadAllTextAsync(legacyLockPath)).IsEqualTo(legacyJson);
    }
    finally
    {
        Directory.Delete(projectDir, recursive: true);
    }
}

    [Test]
    public async Task SetAsync_PersistsContentHash()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "widget", new PluginLockEntry("Contoso.Widget", "1.0.0", "nuget")
            {
                ContentHash = "abc123",
            });

            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries["widget"].ContentHash).IsEqualTo("abc123");

            var json = await File.ReadAllTextAsync(Path.Combine(projectDir, ".engobe", "plugins.lock.json"));
            await Assert.That(json).Contains("\"contentHash\": \"abc123\"");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task SetAsync_WithoutHash_DoesNotWriteHashField()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            await new PluginLockFile().SetAsync(projectDir, "alpha", new PluginLockEntry("Alpha.Plugin", "1.0.0", "nuget"));

            var json = await File.ReadAllTextAsync(Path.Combine(projectDir, ".engobe", "plugins.lock.json"));
            await Assert.That(json).DoesNotContain("contentHash");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }
}
