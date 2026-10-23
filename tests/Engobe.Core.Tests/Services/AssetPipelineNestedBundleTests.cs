namespace Engobe.Core.Tests.Services;

using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Engobe.Abstractions;
using Engobe.Services;

public class AssetPipelineNestedBundleTests
{
    [Test]
    public async Task BuildAsync_Production_NestedBundleAssetsAreCopiedFingerprintAndLinked()
    {
        var dir = AssetPipelineIntegrationTests.CreateSite();
        var bundleDirectory = Path.Combine(dir, "content", "posts", "guides", "advanced", "install");
        var imageDirectory = Path.Combine(bundleDirectory, "images", "screens");
        var downloadDirectory = Path.Combine(bundleDirectory, "downloads");
        Directory.CreateDirectory(imageDirectory);
        Directory.CreateDirectory(downloadDirectory);

        await File.WriteAllTextAsync(Path.Combine(bundleDirectory, "index.md"),
            """
            ---
            title: Install
            ---
            ![Diagram](images/screens/diagram.svg)

            [Download](downloads/setup.zip?download=1#file)
            """);
        await File.WriteAllTextAsync(Path.Combine(imageDirectory, "diagram.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        await File.WriteAllTextAsync(Path.Combine(downloadDirectory, "setup.zip"), "download");
        var cssPath = Path.Combine(dir, "themes", "default", "static", "css", "site.css");
        await File.AppendAllTextAsync(
            cssPath,
            "\n.bundle { background-image: url('/assets/content/posts/guides/advanced/install/images/screens/diagram.svg'); }\n");

        try
        {
            var result = await AssetPipelineIntegrationTests.CreateBuilder([new NuglifyAssetMinifier()])
                .BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();
            var bundleOutput = Path.Combine(dir, "_site", "assets", "content", "posts", "guides", "advanced", "install");
            var imageOutputDirectory = Path.Combine(bundleOutput, "images", "screens");
            var fingerprintedImageNames = Directory.GetFiles(imageOutputDirectory, "diagram.*.svg")
                .Select(Path.GetFileName)
                .Where(static name => name is not null)
                .Cast<string>()
                .ToArray();
            await Assert.That(fingerprintedImageNames).Count().IsEqualTo(1);
            await Assert.That(Regex.IsMatch(fingerprintedImageNames[0], "^diagram\\.[0-9a-f]{8}\\.svg$")).IsTrue();
            await Assert.That(File.Exists(Path.Combine(imageOutputDirectory, "diagram.svg"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(bundleOutput, "downloads", "setup.zip"))).IsTrue();

            var fingerprintedImagePath = $"/assets/content/posts/guides/advanced/install/images/screens/{fingerprintedImageNames[0]}";
            var pageHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "guides", "advanced", "install", "index.html"));
            var pageDocument = await new HtmlParser().ParseDocumentAsync(pageHtml);
            await Assert.That(pageDocument.QuerySelector("img")!.GetAttribute("src")).IsEqualTo(fingerprintedImagePath);
            await Assert.That(pageDocument.QuerySelector("a")!.GetAttribute("href"))
                .IsEqualTo("/assets/content/posts/guides/advanced/install/downloads/setup.zip?download=1#file");

            var fingerprintedCssName = AssetPipelineIntegrationTests.GetSingleFingerprintedCssFileName(
                Path.Combine(dir, "_site", "assets", "css"));
            var css = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "assets", "css", fingerprintedCssName));
            await Assert.That(css).Contains(fingerprintedImagePath);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Production_FailsOnMissingNestedBundleImage()
    {
        var dir = AssetPipelineIntegrationTests.CreateSite();
        var bundleDirectory = Path.Combine(dir, "content", "posts", "guides", "advanced", "install");
        Directory.CreateDirectory(bundleDirectory);
        await File.WriteAllTextAsync(Path.Combine(bundleDirectory, "index.md"),
            """
            ---
            title: Install
            ---
            ![Missing](images/screens/missing.svg)
            """);

        try
        {
            var result = await AssetPipelineIntegrationTests.CreateBuilder([new NuglifyAssetMinifier()])
                .BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Any(static error =>
                    error.Contains("/assets/content/posts/guides/advanced/install/images/screens/missing.svg", StringComparison.Ordinal)))
                .IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
