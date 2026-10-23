namespace Engobe.Cli.Tests;

using System.Diagnostics;

public class SmokeTests
{
    [Test]
    public async Task Cli_Help_UsesEngobeName_AndShowsCommands()
    {
        var cliDll = Path.Combine(
            Path.GetDirectoryName(typeof(SmokeTests).Assembly.Location)!,
            "Engobe.Cli.dll");

        var psi = new ProcessStartInfo("dotnet", $"exec \"{cliDll}\" --help")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        await Assert.That(output).Contains("build");
        await Assert.That(output).Contains("serve");
        await Assert.That(output).Contains("new");
        await Assert.That(output).Contains("engobe");
        await Assert.That(output).DoesNotContain("kiln");
    }
}
