namespace Engobe.Core.Tests.Services;

using Engobe.Services;
using System.Reflection;
using System.Text.Json;

public class ScaffolderTests
{
    private const string SiteName = "testsite";
    private const string NonEmptyErrorFragment = "already exists and is not empty";
    private static readonly HashSet<string> TextFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".html", ".css", ".js", ".json", ".yaml", ".yml", ".svg", ".xml", ".txt", ".csv", ".toml"
    };

    [Test]
    public async Task CreateSite_CreatesExpectedFilesAndReplacesPlaceholders()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), $"engobe-scaffold-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootDir);

        try
        {
            var scaffolder = new Scaffolder();
            var result = scaffolder.CreateSite(SiteName, rootDir);

            await Assert.That(Directory.Exists(result.ProjectPath)).IsTrue();
            await Assert.That(result.CreatedFiles).Contains("site.yaml");
            await Assert.That(result.CreatedFiles).Contains("content/index.md");
            await Assert.That(result.CreatedFiles).Contains("themes/default/layouts/home.html");
            await Assert.That(result.CreatedFiles).Contains("themes/default/layouts/404.html");
            await Assert.That(result.CreatedFiles).Contains("themes/default/layouts/api.html");
            await Assert.That(result.CreatedFiles).Contains("themes/default/static/css/api.css");
            await Assert.That(result.CreatedFiles).Contains("themes/default/partials/recent-posts.html");
            await Assert.That(result.CreatedFiles).Contains("themes/default/partials/search.html");
            await Assert.That(result.CreatedFiles).Contains("themes/default/static/js/engobe-search.js");
            await Assert.That(result.CreatedFiles).Contains("content/posts/01-welcome-to-engobe.md");
            await Assert.That(result.CreatedFiles).Contains("content/posts/07-theming-with-engobe.md");
            await Assert.That(result.CreatedFiles).Contains("content/posts/06-page-bundles-and-images/engobe-architecture.svg");
            await Assert.That(result.CreatedFiles).Contains("content/posts/06-page-bundles-and-images/images/screens/install.svg");
            await Assert.That(result.CreatedFiles).Contains("content/posts/06-page-bundles-and-images/downloads/sample.csv");
            var searchScript = await File.ReadAllTextAsync(Path.Combine(result.ProjectPath, "themes", "default", "static", "js", "engobe-search.js"));
            await Assert.That(searchScript).Contains("data-pagefind-module");
            var generatedFiles = Directory.EnumerateFiles(result.ProjectPath, "*", SearchOption.AllDirectories).ToArray();
            await Assert.That(generatedFiles.Any(path => Path.GetFileName(path).Contains("kiln", StringComparison.OrdinalIgnoreCase))).IsFalse();
            foreach (var file in generatedFiles.Where(static path =>
                         Path.GetFileName(path).Equals(".gitignore", StringComparison.OrdinalIgnoreCase)
                         || TextFileExtensions.Contains(Path.GetExtension(path))))
                await Assert.That(await File.ReadAllTextAsync(file)).DoesNotContain("Kiln");
            var bundleDirectory = Path.Combine(result.ProjectPath, "content", "posts", "06-page-bundles-and-images");
            var bundleContent = await File.ReadAllTextAsync(Path.Combine(bundleDirectory, "index.md"));
            await Assert.That(bundleContent).Contains("![Installation screen](images/screens/install.svg)");
            await Assert.That(bundleContent).Contains("[Download sample data](downloads/sample.csv)");
            await Assert.That(bundleContent).Contains("Directories inside a page bundle are asset folders, not sections.");
            await Assert.That(File.Exists(Path.Combine(bundleDirectory, "images", "screens", "install.svg"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(bundleDirectory, "downloads", "sample.csv"))).IsTrue();

            var siteYamlPath = Path.Combine(result.ProjectPath, "site.yaml");
            var contentIndexPath = Path.Combine(result.ProjectPath, "content", "index.md");

            await Assert.That(File.Exists(siteYamlPath)).IsTrue();
            await Assert.That(File.Exists(contentIndexPath)).IsTrue();

            var siteYaml = await File.ReadAllTextAsync(siteYamlPath);
            var contentIndex = await File.ReadAllTextAsync(contentIndexPath);

            await Assert.That(siteYaml).Contains("title: testsite");
            await Assert.That(siteYaml).DoesNotContain("{{NAME}}");
            await Assert.That(contentIndex).Contains("layout: home");
            await Assert.That(contentIndex).DoesNotContain("{{DATE");
            using var toolManifest = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(result.ProjectPath, ".config", "dotnet-tools.json")));
            var tool = toolManifest.RootElement.GetProperty("tools").GetProperty("Engobe");
            var version = typeof(Scaffolder).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?? throw new InvalidOperationException("The Engobe assembly does not declare its package version.");
            await Assert.That(tool.GetProperty("version").GetString()).IsEqualTo(version.InformationalVersion.Split('+')[0]);
            var commands = tool.GetProperty("commands");
            await Assert.That(commands.GetArrayLength()).IsEqualTo(1);
            await Assert.That(commands[0].GetString()).IsEqualTo("engobe");
        }
        finally
        {
            Directory.Delete(rootDir, true);
        }
    }

    [Test]
    public async Task CreateSite_WhenTargetDirectoryIsNotEmpty_ThrowsInvalidOperationException()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), $"engobe-scaffold-nonempty-{Guid.NewGuid():N}");
        var targetDir = Path.Combine(rootDir, SiteName);
        Directory.CreateDirectory(targetDir);
        await File.WriteAllTextAsync(Path.Combine(targetDir, "existing.txt"), "content");

        try
        {
            var scaffolder = new Scaffolder();

            await Assert.That(() => scaffolder.CreateSite(SiteName, rootDir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining(NonEmptyErrorFragment);
        }
        finally
        {
            Directory.Delete(rootDir, true);
        }
    }
}
