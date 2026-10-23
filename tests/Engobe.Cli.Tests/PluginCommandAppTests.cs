namespace Engobe.Cli.Tests;

using Engobe.Cli.Commands;
using Engobe.Cli.Tests.Fakes;
using Engobe.Models;
using Engobe.Services;
using Engobe.Tests;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using Spectre.Console.Cli.Testing;
using Spectre.Console.Testing;

public class PluginCommandAppTests
{
    [Test]
    [Arguments(false, "match", false)]
    [Arguments(true, "match", false)]
    [Arguments(false, "missing", false)]
    [Arguments(true, "missing", false)]
    [Arguments(false, "mismatch", false)]
    [Arguments(true, "mismatch", false)]
    [Arguments(false, "offline-match", false)]
    [Arguments(true, "offline-match", false)]
    [Arguments(false, "offline-missing", false)]
    [Arguments(true, "offline-missing", false)]
    [Arguments(false, "missing", true)]
    [Arguments(true, "missing", true)]
    public async Task PluginCommands_EnforcePackageHashVerification_WithRealV3Feed(bool update, string mode, bool consent)
    {
        await using var feed = new FakeNuGetV3Feed();
        var hash = mode == "mismatch" ? Convert.ToBase64String(new byte[64]) : "match";
        if (mode.Contains("missing", StringComparison.Ordinal))
            hash = null;
        feed.AddPackage("Engobe.Plugin.Verified", "1.2.3", hash);
        var client = await feed.CreateClientAsync();
        var root = Path.Combine(Path.GetTempPath(), $"engobe-cli-hash-{Guid.NewGuid():N}");
        var project = Path.Combine(root, "project");
        var lockFile = new PluginLockFile();
        Directory.CreateDirectory(project);
        try
        {
            if (mode.StartsWith("offline", StringComparison.Ordinal))
            {
                await client.AddAsync("Engobe.Plugin.Verified", "1.2.3", Path.Combine(root, "warm"));
                await client.GetLatestVersionAsync("Engobe.Plugin.Verified");
                feed.ExpireCache();
                feed.Offline = true;
                client = await feed.CreateClientAsync();
            }
            if (update)
            {
                var installed = Path.Combine(project, "plugins", "verified");
                Directory.CreateDirectory(installed);
                await File.WriteAllTextAsync(Path.Combine(installed, "plugin.yaml"), "name: verified\nversion: 1.0.0\n");
                await lockFile.SetAsync(project, "verified", new PluginLockEntry("Engobe.Plugin.Verified", "1.0.0", "nuget")
                {
                    ContentHash = PluginContentHasher.ComputeDirectoryHash(installed),
                    Trust = PluginTrustLevel.FirstParty,
                });
            }
            var before = SnapshotFileHashes(project);
            var (app, console) = CreateApp(client, lockFile);
            console.Profile.Width = 1000;
            console.Profile.Capabilities.Interactive = consent;
            if (consent)
                console.Input.PushTextWithEnter("y");
            string[] arguments = update
                ? ["plugin", "update", "verified", project]
                : ["plugin", "add", "Engobe.Plugin.Verified", project, "--version", "1.2.3"];
            if (!consent)
                arguments = [..arguments, "--yes"];
            var result = await app.RunAsync(arguments);

            var failure = mode is "mismatch" or "offline-missing";
            await Assert.That(result.ExitCode).IsEqualTo(failure ? 1 : 0);
            if (failure)
            {
                await Assert.That(SnapshotFileHashes(project)).IsEquivalentTo(before);
                await Assert.That(console.Output).Contains(mode == "mismatch" ? "SHA-512 hash mismatch" : "cannot be verified offline");
            }
            else
            {
                var entries = await lockFile.ReadAsync(project);
                await Assert.That(entries["verified"].Version).IsEqualTo("1.2.3");
                await Assert.That(entries["verified"].ContentHash).IsEqualTo(PluginContentHasher.ComputeDirectoryHash(Path.Combine(project, "plugins", "verified")));
            }
            if (mode is "missing" or "offline-missing")
            {
                await Assert.That(console.Output).Contains("WARN:");
                await Assert.That(console.Output).Contains(feed.Source);
                await Assert.That(console.Output).Contains("Engobe.Plugin.Verified 1.2.3");
            }
            if (mode == "missing")
            {
                await Assert.That(console.Output).Contains("without package hash verification");
                await Assert.That(console.Output.Split("without package hash verification").Length).IsEqualTo(2);
                if (consent)
                {
                    var prompt = update ? "Update 'verified'" : "Install community plugin";
                    var warningPosition = console.Output.IndexOf("WARN:", StringComparison.Ordinal);
                    var promptPosition = console.Output.IndexOf(prompt, StringComparison.Ordinal);
                    await Assert.That(promptPosition).IsGreaterThan(warningPosition);
                }
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string[] SnapshotFileHashes(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => $"{Path.GetRelativePath(root, path)}:{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}")
            .Order(StringComparer.Ordinal)
            .ToArray();

    [Test]
    public async Task PluginSearchCommand_ShowsResultsFromNuGet()
    {
        var client = new FakeNuGetPluginClient
        {
            SearchResults =
            [
                new PluginSearchResult("Engobe.Plugin.EmailProtect", "1.2.3", "Protect emails from scrapers")
            ]
        };

        var (app, console) = CreateApp(client, new PluginLockFile());

        var result = await app.RunAsync(["plugin", "search", "email-protect"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Engobe.Plugin.EmailProtect");
        await Assert.That(console.Output).Contains("1.2.3");
    }

    [Test]
    public async Task PluginAddCommand_WritesLockFileAndWarnsAboutSecurity()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-add-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var client = new FakeNuGetPluginClient
            {
                InstallResult = new PluginPackageInstallResult("Engobe.Plugin.EmailProtect", "1.2.3", "email-protect", Path.Combine(projectDir, "plugins", "email-protect"))
            };

            var (app, console) = CreateApp(client, new PluginLockFile());
            var result = await app.RunAsync(["plugin", "add", "Engobe.Plugin.EmailProtect", "--version", "1.2.3", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(console.Output).Contains("trustworthy source");
            var lockFile = new PluginLockFile();
            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries["email-protect"].PackageId).IsEqualTo("Engobe.Plugin.EmailProtect");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginUpdateCommand_WhenLockEntryMissing_ExitsNonZero()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-update-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var client = new FakeNuGetPluginClient();
            var (app, console) = CreateApp(client, new PluginLockFile());
            var result = await app.RunAsync(["plugin", "update", "email-protect", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("kein Lock-Eintrag");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginUpdateCommand_AlreadyCurrent_SkipsDownload()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-update-current-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "email-protect", new PluginLockEntry("Engobe.Plugin.EmailProtect", "1.2.3", "nuget"));

            var client = new FakeNuGetPluginClient
            {
                LatestVersion = "1.2.3"
            };

            var (app, console) = CreateApp(client, lockFile);
            var result = await app.RunAsync(["plugin", "update", "email-protect", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(console.Output).Contains("bereits aktuell");
            await Assert.That(client.AddCallCount).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginRemoveCommand_RemovesFolderAndLockEntry()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-remove-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(projectDir, "plugins", "email-protect"));
        var lockFile = new PluginLockFile();
        await lockFile.SetAsync(projectDir, "email-protect", new PluginLockEntry("Engobe.Plugin.EmailProtect", "1.2.3", "nuget"));

        var client = new FakeNuGetPluginClient();
        var (app, console) = CreateApp(client, lockFile);
        var result = await app.RunAsync(["plugin", "remove", "email-protect", projectDir, "--yes"]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(Directory.Exists(Path.Combine(projectDir, "plugins", "email-protect"))).IsFalse();
        var entries = await lockFile.ReadAsync(projectDir);
        await Assert.That(entries).DoesNotContainKey("email-protect");
        await Assert.That(console.Output).Contains("site.yaml");
    }

    [Test]
    [Arguments("..")]
    [Arguments(".")]
    [Arguments("../x")]
    [Arguments("a/b")]
    [Arguments("a\\b")]
    [Arguments("{victim}")]
    [Arguments("Upper")]
    [Arguments("with space")]
    public async Task PluginRemoveCommand_WithInvalidName_FailsAndLeavesFileSystemUntouched(string nameArgument)
    {
        var container = Path.Combine(Path.GetTempPath(), $"engobe-plugin-remove-unsafe-{Guid.NewGuid():N}");
        var projectDir = Path.Combine(container, "project");
        var victim = Path.Combine(container, "victim");

        try
        {
            Directory.CreateDirectory(Path.Combine(projectDir, "plugins", "email-protect"));
            Directory.CreateDirectory(Path.Combine(projectDir, "plugins", "a", "b"));
            Directory.CreateDirectory(Path.Combine(projectDir, "x"));
            Directory.CreateDirectory(victim);
            await File.WriteAllTextAsync(Path.Combine(projectDir, "site.yaml"), "title: precious");
            await File.WriteAllTextAsync(Path.Combine(projectDir, "plugins", "email-protect", "plugin.yaml"), "name: email-protect");
            await File.WriteAllTextAsync(Path.Combine(projectDir, "plugins", "a", "b", "file.txt"), "keep");
            await File.WriteAllTextAsync(Path.Combine(projectDir, "x", "file.txt"), "keep");
            await File.WriteAllTextAsync(Path.Combine(victim, "file.txt"), "keep");
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "email-protect", new PluginLockEntry("Engobe.Plugin.EmailProtect", "1.2.3", "nuget"));
            var before = SnapshotPaths(container);

            var (app, _) = CreateApp(new FakeNuGetPluginClient(), lockFile);
            var name = nameArgument.Replace("{victim}", victim, StringComparison.Ordinal);
            var result = await app.RunAsync(["plugin", "remove", name, projectDir, "--yes"]);

            await Assert.That(result.ExitCode).IsNotEqualTo(0);
            await Assert.That(SnapshotPaths(container)).IsEquivalentTo(before);
            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries).ContainsKey("email-protect");
        }
        finally
        {
            Directory.Delete(container, recursive: true);
        }
    }

    [Test]
    public async Task PluginListCommand_ShowsSourceColumn()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-list-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectDir, "plugins", "email-protect"));
        await File.WriteAllTextAsync(Path.Combine(projectDir, "plugins", "email-protect", "plugin.yaml"), "name: Email Protect\nversion: 1.2.3\ndescription: Protect emails\n");

        var lockFile = new PluginLockFile();
        await lockFile.SetAsync(projectDir, "email-protect", new PluginLockEntry("Engobe.Plugin.EmailProtect", "1.2.3", "nuget"));

        var (app, console) = CreateApp(new FakeNuGetPluginClient(), lockFile);
        var result = await app.RunAsync(["plugin", "list", projectDir]);

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(console.Output).Contains("Engobe.Plugin.EmailProtect");
        await Assert.That(console.Output).Contains("nuget");

        Directory.Delete(projectDir, recursive: true);
    }

    [Test]
    [Arguments("Legacy_Name")]
    [Arguments("My Plugin")]
    [Arguments("Upper[red]")]
    public async Task PluginRemoveCommand_WithLegacyDirectoryName_RemovesFolderAndLockEntry(string name)
    {
        var container = Path.Combine(Path.GetTempPath(), $"engobe-plugin-remove-legacy-{Guid.NewGuid():N}");
        var projectDir = Path.Combine(container, "project");

        try
        {
            Directory.CreateDirectory(Path.Combine(projectDir, "plugins", name));
            Directory.CreateDirectory(Path.Combine(projectDir, "plugins", "keep"));
            await File.WriteAllTextAsync(Path.Combine(projectDir, "plugins", name, "file.txt"), "x");
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, name, new PluginLockEntry("Engobe.Plugin.Legacy", "1.0.0", "nuget"));

            var (app, console) = CreateApp(new FakeNuGetPluginClient(), lockFile);
            var result = await app.RunAsync(["plugin", "remove", name, projectDir, "--yes"]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(console.Output).Contains(name);
            await Assert.That(Directory.Exists(Path.Combine(projectDir, "plugins", name))).IsFalse();
            await Assert.That(Directory.Exists(Path.Combine(projectDir, "plugins", "keep"))).IsTrue();
            await Assert.That(await lockFile.ReadAsync(projectDir)).DoesNotContainKey(name);
        }
        finally
        {
            Directory.Delete(container, recursive: true);
        }
    }

    [Test]
    public async Task PluginRemoveCommand_WhenPluginDoesNotExist_ExitsNonZero()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-remove-missing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectDir, "plugins"));

        try
        {
            var (app, console) = CreateApp(new FakeNuGetPluginClient(), new PluginLockFile());
            var result = await app.RunAsync(["plugin", "remove", "ghost", projectDir, "--yes"]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("was not found");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginAddCommand_PassesForceAndLockEntries_AndStoresHash()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-add-opts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "other", new PluginLockEntry("Engobe.Plugin.Other", "1.0.0", "nuget"));
            var client = new FakeNuGetPluginClient
            {
                InstallResult = new PluginPackageInstallResult("Contoso.Widget", "2.0.0", "widget", Path.Combine(projectDir, "plugins", "widget"))
                {
                    ContentHash = "hash-1",
                },
            };

            var (app, _) = CreateApp(client, lockFile);
            var result = await app.RunAsync(["plugin", "add", "Contoso.Widget", "--force", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(client.LastOptions!.Force).IsTrue();
            await Assert.That(client.LastOptions.ExistingLockEntries!).ContainsKey("other");
            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries["widget"].ContentHash).IsEqualTo("hash-1");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginAddCommand_WithoutFlags_DoesNotForce()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-add-default-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var client = new FakeNuGetPluginClient();
            var (app, _) = CreateApp(client, new PluginLockFile());
            var result = await app.RunAsync(["plugin", "add", "Engobe.Plugin.Good", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(client.LastOptions!.Force).IsFalse();
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginAddCommand_ForUntaggedPackage_ExitsNonZeroAndDoesNotWriteLock()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-add-untagged-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var client = new FakeNuGetPluginClient
            {
                AddException = new InvalidOperationException("Package 'Contoso.Widget' does not carry the 'engobe-plugin' tag and cannot be installed as a Engobe plugin."),
            };
            var lockFile = new PluginLockFile();
            var (app, console) = CreateApp(client, lockFile);
            var result = await app.RunAsync(["plugin", "add", "Contoso.Widget", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("engobe-plugin");
            await Assert.That(console.Output).DoesNotContain("Use --");
            await Assert.That(await lockFile.ReadAsync(projectDir)).IsEmpty();
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginAddCommand_WhenInstallIsRejected_PrintsEscapedMessageAndDoesNotWriteLock()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-add-reject-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var client = new FakeNuGetPluginClient { AddException = new InvalidOperationException("Plugin 'x[red]y' has local changes. Use --force.") };
            var lockFile = new PluginLockFile();
            var (app, console) = CreateApp(client, lockFile);
            var result = await app.RunAsync(["plugin", "add", "Engobe.Plugin.Good", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("x[red]y");
            await Assert.That(console.Output).Contains("--force");
            await Assert.That(await lockFile.ReadAsync(projectDir)).IsEmpty();
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginUpdateCommand_PassesForce_AndStoresNewHash()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-update-opts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "widget", new PluginLockEntry("Contoso.Widget", "1.0.0", "nuget") { ContentHash = "old" });
            var client = new FakeNuGetPluginClient
            {
                LatestVersion = "2.0.0",
                InstallResult = new PluginPackageInstallResult("Contoso.Widget", "2.0.0", "widget", Path.Combine(projectDir, "plugins", "widget"))
                {
                    ContentHash = "new",
                },
            };

            var (app, _) = CreateApp(client, lockFile);
            var result = await app.RunAsync(["plugin", "update", "widget", "--force", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(client.LastOptions!.Force).IsTrue();
            var entries = await lockFile.ReadAsync(projectDir);
            await Assert.That(entries["widget"].ContentHash).IsEqualTo("new");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginUpdateCommand_WithMarkupCharactersInLockEntry_PrintsThemLiterally()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-update-markup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "x[red]y", new PluginLockEntry("Engobe.Plugin.A[b]", "1.0.0", "nuget"));
            var client = new FakeNuGetPluginClient { LatestVersion = "2.0.0" };

            var (app, console) = CreateApp(client, lockFile);
            var result = await app.RunAsync(["plugin", "update", "x[red]y", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(console.Output).Contains("Checking x[red]y");
            await Assert.That(console.Output).Contains("A[b]");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginUpdateCommand_WhenInstallIsRejected_ExitsNonZeroAndKeepsLockEntry()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-update-reject-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);

        try
        {
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "good", new PluginLockEntry("Engobe.Plugin.Good", "1.0.0", "nuget"));
            var client = new FakeNuGetPluginClient
            {
                LatestVersion = "2.0.0",
                AddException = new InvalidOperationException("Plugin 'good' has local changes. Use --force to overwrite them."),
            };

            var (app, console) = CreateApp(client, lockFile);
            var result = await app.RunAsync(["plugin", "update", "good", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(1);
            await Assert.That(console.Output).Contains("--force");
            await Assert.That((await lockFile.ReadAsync(projectDir))["good"].Version).IsEqualTo("1.0.0");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginListCommand_MarksLocallyModifiedPlugin()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-list-modified-{Guid.NewGuid():N}");
        var pluginDir = Path.Combine(projectDir, "plugins", "email-protect");
        Directory.CreateDirectory(pluginDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(pluginDir, "plugin.yaml"), "name: email-protect\nversion: 1.2.3\n");
            var lockFile = new PluginLockFile();
            await lockFile.SetAsync(projectDir, "email-protect", new PluginLockEntry("Engobe.Plugin.EmailProtect", "1.2.3", "nuget")
            {
                ContentHash = PluginContentHasher.ComputeDirectoryHash(pluginDir),
            });

            var (firstApp, firstConsole) = CreateApp(new FakeNuGetPluginClient(), lockFile);
            var unmodified = await firstApp.RunAsync(["plugin", "list", projectDir]);
            await File.AppendAllTextAsync(Path.Combine(pluginDir, "plugin.yaml"), "description: edited\n");
            var (secondApp, secondConsole) = CreateApp(new FakeNuGetPluginClient(), lockFile);
            var modified = await secondApp.RunAsync(["plugin", "list", projectDir]);

            await Assert.That(unmodified.ExitCode).IsEqualTo(0);
            await Assert.That(firstConsole.Output).DoesNotContain("modified");
            await Assert.That(modified.ExitCode).IsEqualTo(0);
            await Assert.That(secondConsole.Output).Contains("modified");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Test]
    public async Task PluginListCommand_WithMarkupCharactersInManifest_PrintsThemLiterally()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"engobe-plugin-list-markup-{Guid.NewGuid():N}");
        var pluginDir = Path.Combine(projectDir, "plugins", "odd");
        Directory.CreateDirectory(pluginDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(pluginDir, "plugin.yaml"), "name: odd\nversion: 1.0.0\ndescription: 'has [red]markup[/]'\n");

            var (app, console) = CreateApp(new FakeNuGetPluginClient(), new PluginLockFile());
            var result = await app.RunAsync(["plugin", "list", projectDir]);

            await Assert.That(result.ExitCode).IsEqualTo(0);
            await Assert.That(console.Output).Contains("[red]markup[/]");
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    private static List<string> SnapshotPaths(string root)
    {
        var paths = Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();
        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    private static (CommandAppTester App, TestConsole Console) CreateApp(
        INuGetPluginClient client,
        IPluginLockFile lockFile)
    {
        var (app, console) = CommandAppTesterFactory.Create(services =>
        {
            services.AddSingleton(client);
            services.AddSingleton(lockFile);
            services.AddSingleton<IPluginLoader, PluginLoader>();
        });

        app.Configure(config =>
        {
            config.AddBranch("plugin", p =>
            {
                p.AddCommand<PluginSearchCommand>("search");
                p.AddCommand<PluginInfoCommand>("info");
                p.AddCommand<PluginAddCommand>("add");
                p.AddCommand<PluginUpdateCommand>("update");
                p.AddCommand<PluginRemoveCommand>("remove");
                p.AddCommand<PluginListCommand>("list");
            });
        });

        return (app, console);
    }
}
