namespace Engobe.Cli.Tests;

using System.Diagnostics;
using Engobe.Services;

public class DotnetSuggestCompletionTests
{
    [Test]
    public async Task CompletionRequest_ReturnsEngobeCommands()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var (exitCode, output) = await RunSuggestAsync(directory, "engobe b");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)).Contains("build");
            await Assert.That(output).DoesNotContain("kiln");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task CompletionRequest_ReturnsLockedPluginNamesForRemove()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            await new PluginLockFile().SetAsync(
                directory,
                "email-protect",
                new PluginLockEntry("Engobe.Plugin.EmailProtect", "1.0.0", "nuget"));

            var (exitCode, output) = await RunSuggestAsync(directory, "engobe plugin remove ema");

            await Assert.That(exitCode).IsEqualTo(0);
            await Assert.That(output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
                .Contains("email-protect");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunSuggestAsync(string workingDirectory, string commandLine)
    {
        var cliDll = Path.Combine(
            Path.GetDirectoryName(typeof(DotnetSuggestCompletionTests).Assembly.Location)!,
            "Engobe.Cli.dll");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(cliDll);
        startInfo.ArgumentList.Add($"[suggest:{commandLine.Length}]");
        startInfo.ArgumentList.Add(commandLine);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the Engobe CLI process.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, await standardOutput + await standardError);
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"engobe-suggest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
