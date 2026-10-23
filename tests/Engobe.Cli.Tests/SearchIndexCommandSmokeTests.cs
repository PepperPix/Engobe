namespace Engobe.Cli.Tests;

using System.Diagnostics;

public class SearchIndexCommandSmokeTests
{
    private static string CliDll => Path.Combine(
        Path.GetDirectoryName(typeof(SearchIndexCommandSmokeTests).Assembly.Location)!,
        "Engobe.Cli.dll");

    [Test]
    public async Task SearchIndex_NoDownload_WithoutSite_ExitsNonZeroWithoutCrash()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"engobe-smoke-search-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        // Create a minimal site.yaml so the config loader doesn't throw
        await File.WriteAllTextAsync(Path.Combine(tempDir, "site.yaml"), """
            title: Smoke Test
            baseUrl: http://localhost
            search:
              enabled: true
            """);

        // Create an empty _site dir (no pagefind binary available, --no-download)
        var siteDir = Path.Combine(tempDir, "_site");
        Directory.CreateDirectory(siteDir);

        // Isolate from any pagefind binary the developer/CI machine may already have
        // cached under ~/.engobe/tools/pagefind from a previous real download.
        var isolatedCacheDir = Path.Combine(tempDir, "engobe-cache-isolated");

        try
        {
            var psi = new ProcessStartInfo(
                "dotnet",
                $"exec \"{CliDll}\" search index \"{tempDir}\" --no-download")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.Environment.Remove("ENGOBE_PAGEFIND_PATH");
            psi.Environment["ENGOBE_PAGEFIND_CACHE_DIR"] = isolatedCacheDir;
            psi.Environment["PATH"] = isolatedCacheDir;

            using var process = Process.Start(psi)!;
            var output = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            // Should fail cleanly (not crash) with exit code 1
            await Assert.That(process.ExitCode).IsEqualTo(1);

            // Should mention the missing binary in a helpful way
            var combined = output + stderr;
            await Assert.That(combined).Contains("ENGOBE_PAGEFIND_PATH");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
