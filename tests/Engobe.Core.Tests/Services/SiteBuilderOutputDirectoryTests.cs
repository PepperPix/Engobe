namespace Engobe.Core.Tests.Services;

using System.Security.Cryptography;
using Engobe.Abstractions;
using Engobe.Services;

public class SiteBuilderOutputDirectoryTests
{
    [Test]
    [Arguments(".")]
    [Arguments("./")]
    [Arguments("")]
    [Arguments("..")]
    [Arguments("../x")]
    [Arguments("{foreign}")]
    [Arguments("themes")]
    [Arguments("themes/default")]
    [Arguments("themes/default/out")]
    [Arguments("content")]
    [Arguments("content/posts")]
    [Arguments("content/posts/out")]
    [Arguments("static")]
    [Arguments("plugins")]
    [Arguments(".git")]
    [Arguments(".engobe")]
    [Arguments(".kiln")]
    [Arguments("_site/../content")]
    public async Task BuildAsync_WithUnsafeOutputDir_FailsAndLeavesTheFileSystemUntouched(string outputDirValue)
    {
        using var workspace = new TempWorkspace();
        var configured = outputDirValue.Replace("{foreign}", workspace.ForeignDir, StringComparison.Ordinal);
        workspace.WriteSiteYaml(configured);
        var before = workspace.Snapshot();

        var result = await CreateBuilder().BuildAsync(workspace.ProjectDir, false, BuildEnvironment.Production, CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(string.Join('\n', result.Errors)).Contains("outputDir");
        await Assert.That(workspace.Snapshot()).IsEquivalentTo(before);
    }

    [Test]
    [Arguments(BuildEnvironment.Development)]
    [Arguments(BuildEnvironment.Production)]
    public async Task BuildAsync_WithUnsafeOutputDir_InBothEnvironments_KeepsProjectFiles(BuildEnvironment environment)
    {
        using var workspace = new TempWorkspace();
        workspace.WriteSiteYaml(".");
        var before = workspace.Snapshot();

        var result = await CreateBuilder().BuildAsync(workspace.ProjectDir, false, environment, CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(workspace.Snapshot()).IsEquivalentTo(before);
    }

    [Test]
    public async Task BuildAsync_WithOutputDirOverlappingHomePageDirectory_Fails()
    {
        using var workspace = new TempWorkspace();
        workspace.WriteSiteYaml("pages", homePage: "pages/home.md");
        Directory.CreateDirectory(Path.Combine(workspace.ProjectDir, "pages"));
        await File.WriteAllTextAsync(Path.Combine(workspace.ProjectDir, "pages", "home.md"), "---\ntitle: Home\n---\nhome");
        var before = workspace.Snapshot();

        var result = await CreateBuilder().BuildAsync(workspace.ProjectDir, false, BuildEnvironment.Production, CancellationToken.None);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(string.Join('\n', result.Errors)).Contains("home.page");
        await Assert.That(workspace.Snapshot()).IsEquivalentTo(before);
    }

    [Test]
    [Arguments("_site", "_site")]
    [Arguments("dist/public", "dist/public")]
    [Arguments("./_site/", "_site")]
    public async Task BuildAsync_WithSubdirectoryOutputDir_Succeeds(string outputDirValue, string expectedRelativeDir)
    {
        using var workspace = new TempWorkspace();
        workspace.WriteSiteYaml(outputDirValue);

        var result = await CreateBuilder().BuildAsync(workspace.ProjectDir, false, BuildEnvironment.Development, CancellationToken.None);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(File.Exists(Path.Combine(workspace.ProjectDir, expectedRelativeDir, "blog", "first", "index.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(workspace.ProjectDir, "content", "posts", "first.md"))).IsTrue();
    }

    [Test]
    public async Task BuildAsync_Development_PrunesAddedDeletedAndRenamedNestedBundleAssets()
    {
        using var workspace = new TempWorkspace();
        workspace.WriteSiteYaml("_site");
        var bundleDirectory = Path.Combine(workspace.ProjectDir, "content", "posts", "guides", "advanced", "install");
        var imageDirectory = Path.Combine(bundleDirectory, "images", "screens");
        var downloadDirectory = Path.Combine(bundleDirectory, "downloads");
        Directory.CreateDirectory(imageDirectory);
        Directory.CreateDirectory(downloadDirectory);
        await File.WriteAllTextAsync(Path.Combine(bundleDirectory, "index.md"), "---\ntitle: Install\n---\nInstall");
        await File.WriteAllTextAsync(Path.Combine(imageDirectory, "removed.png"), "old image");
        await File.WriteAllTextAsync(Path.Combine(imageDirectory, "keep.txt"), "keep");
        await File.WriteAllTextAsync(Path.Combine(downloadDirectory, "setup.zip"), "old download");

        var firstBuild = await CreateBuilder().BuildAsync(
            workspace.ProjectDir,
            false,
            BuildEnvironment.Development,
            CancellationToken.None);

        await Assert.That(firstBuild.Success).IsTrue();
        var bundleOutput = Path.Combine(workspace.ProjectDir, "_site", "assets", "content", "posts", "guides", "advanced", "install");
        var removedImageOutput = Path.Combine(bundleOutput, "images", "screens", "removed.png");
        var keepOutput = Path.Combine(bundleOutput, "images", "screens", "keep.txt");
        var oldDownloadOutput = Path.Combine(bundleOutput, "downloads", "setup.zip");
        await Assert.That(File.Exists(removedImageOutput)).IsTrue();
        await Assert.That(File.Exists(oldDownloadOutput)).IsTrue();

        File.Delete(Path.Combine(imageDirectory, "removed.png"));
        await File.WriteAllTextAsync(Path.Combine(imageDirectory, "added.png"), "new image");
        File.Move(Path.Combine(downloadDirectory, "setup.zip"), Path.Combine(downloadDirectory, "renamed.zip"));

        var secondBuild = await CreateBuilder().BuildAsync(
            workspace.ProjectDir,
            false,
            BuildEnvironment.Development,
            CancellationToken.None);

        await Assert.That(secondBuild.Success).IsTrue();
        await Assert.That(File.Exists(removedImageOutput)).IsFalse();
        await Assert.That(File.Exists(oldDownloadOutput)).IsFalse();
        await Assert.That(File.Exists(Path.Combine(bundleOutput, "images", "screens", "added.png"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(bundleOutput, "downloads", "renamed.zip"))).IsTrue();
        await Assert.That(await File.ReadAllTextAsync(keepOutput)).IsEqualTo("keep");
    }

    private static SiteBuilder CreateBuilder()
    {
        return new SiteBuilder(
            new ContentReader(new MarkdownProcessor()),
            new TemplateRenderer(),
            new PermalinkGenerator(),
            new SiteConfigLoader(),
            new PluginLoader(),
            [new NuglifyAssetMinifier(), new NoOpAssetMinifier()],
            new SkiaSharpImageOptimizer(),
            new AssetReferenceIndexBuilder());
    }

    private sealed class TempWorkspace : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"engobe-outdir-{Guid.NewGuid():N}");

        public TempWorkspace()
        {
            ProjectDir = Path.Combine(_root, "project");
            ForeignDir = Path.Combine(_root, "foreign");

            Directory.CreateDirectory(Path.Combine(ProjectDir, "content", "posts"));
            Directory.CreateDirectory(Path.Combine(ProjectDir, "themes", "default", "layouts"));
            Directory.CreateDirectory(Path.Combine(ProjectDir, "static"));
            Directory.CreateDirectory(Path.Combine(ProjectDir, "plugins"));
            Directory.CreateDirectory(Path.Combine(ProjectDir, ".git"));
            Directory.CreateDirectory(Path.Combine(ProjectDir, ".engobe"));
            Directory.CreateDirectory(Path.Combine(ProjectDir, ".kiln"));
            Directory.CreateDirectory(ForeignDir);

            File.WriteAllText(Path.Combine(ProjectDir, "content", "posts", "first.md"), "---\ntitle: First\n---\nfirst");
            File.WriteAllText(Path.Combine(ProjectDir, "themes", "default", "layouts", "default.html"), "<html><body>{{ page.content }}</body></html>");
            File.WriteAllText(Path.Combine(ProjectDir, "themes", "default", "layouts", "404.html"), "<html><body>Not Found</body></html>");
            File.WriteAllText(Path.Combine(ProjectDir, "static", "keep.txt"), "keep");
            File.WriteAllText(Path.Combine(ProjectDir, "plugins", "keep.txt"), "keep");
            File.WriteAllText(Path.Combine(ProjectDir, ".git", "HEAD"), "ref: refs/heads/main");
            File.WriteAllText(Path.Combine(ProjectDir, ".engobe", "keep.txt"), "keep");
            File.WriteAllText(Path.Combine(ProjectDir, ".kiln", "keep.txt"), "legacy state");
            File.WriteAllText(Path.Combine(ProjectDir, "README.md"), "precious");
            File.WriteAllText(Path.Combine(ForeignDir, "sentinel.txt"), "foreign data");
            File.WriteAllText(Path.Combine(_root, "outer.txt"), "outer data");
        }

        public string ProjectDir { get; }

        public string ForeignDir { get; }

        public void WriteSiteYaml(string outputDir, string? homePage = null)
        {
            var home = homePage is null ? string.Empty : $"home:\n  page: {homePage}\n";
            var yaml = $"""
                title: Test Site
                baseUrl: http://localhost:5555
                outputDir: '{outputDir.Replace("'", "''", StringComparison.Ordinal)}'
                {home}collections:
                  posts:
                    directory: content/posts
                    permalink: /blog/:slug/
                """;
            File.WriteAllText(Path.Combine(ProjectDir, "site.yaml"), yaml);
        }

        public List<string> Snapshot()
        {
            var entries = new List<string>();
            foreach (var path in Directory.EnumerateFileSystemEntries(_root, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(_root, path);
                entries.Add(File.Exists(path)
                    ? $"F {relative} {Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}"
                    : $"D {relative}");
            }

            entries.Sort(StringComparer.Ordinal);
            return entries;
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
    }
}
