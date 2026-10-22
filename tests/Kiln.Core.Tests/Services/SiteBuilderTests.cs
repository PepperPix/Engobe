namespace Kiln.Core.Tests.Services;

using Kiln.Abstractions;
using Kiln.Models;
using Kiln.Services;

public class SiteBuilderTests
{
    [Test]
    public async Task BuildAsync_DetectsPermalinkCollision()
    {
        var tempDir = CreateSiteWithCollision();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(tempDir);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Count).IsGreaterThan(0);
            await Assert.That(result.Errors[0]).Contains("Permalink collision");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task BuildAsync_CopiesThemeAssetsToAssetsSubdir()
    {
        var tempDir = CreateSiteWithThemeAsset();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(tempDir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(tempDir, "_site", "assets", "css", "style.css"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(tempDir, "_site", "css", "style.css"))).IsFalse();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task BuildAsync_CopiesPageBundleAssetsToAssetsContentDir()
    {
        var tempDir = CreateSiteWithPageBundle();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(tempDir);

            await Assert.That(result.Success).IsTrue();
            var outputDir = Path.Combine(tempDir, "_site", "assets", "content", "posts", "with-image");
            await Assert.That(File.Exists(Path.Combine(outputDir, "hero.txt"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDir, "images", "screens", "x.jpg"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDir, "images", "screens", "logo.txt"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDir, "downloads", "setup.zip"))).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(outputDir, "images", "screens", "logo.txt"))).IsEqualTo("image logo");
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(outputDir, "downloads", "logo.txt"))).IsEqualTo("download logo");
            await Assert.That(File.Exists(Path.Combine(outputDir, "child", "photo.png"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDir, "notes", "data.zip"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDir, "images", "manual", "diagram.svg"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(outputDir, "child", "index.md"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(outputDir, "notes", "article.md"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(outputDir, "images", "manual", "README.MD"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(outputDir, ".secret"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(outputDir, ".private", "picture.jpg"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(outputDir, "images", ".cache", "x.jpg"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(outputDir, "linked-file.txt"))).IsFalse();
            await Assert.That(Directory.Exists(Path.Combine(outputDir, "linked-directory"))).IsFalse();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task BuildAsync_NestedContent_ProducesDifferentPaths()
    {
        var tempDir = CreateSiteWithNestedContent();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(tempDir);

            await Assert.That(result.Success).IsTrue();

            var guideOutput = Path.Combine(tempDir, "_site", "guides", "getting-started", "index.html");
            var deepOutput = Path.Combine(tempDir, "_site", "guides", "advanced", "config", "index.html");
            var topOutput = Path.Combine(tempDir, "_site", "top", "index.html");

            await Assert.That(File.Exists(guideOutput)).IsTrue();
            await Assert.That(File.Exists(deepOutput)).IsTrue();
            await Assert.That(File.Exists(topOutput)).IsTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task BuildAsync_SiteStaticOverridesThemeAsset_WarnsAndUsesSiteFile()
    {
        var tempDir = CreateSiteWithThemeAsset();

        try
        {
            Directory.CreateDirectory(Path.Combine(tempDir, "static", "css"));
            await File.WriteAllTextAsync(Path.Combine(tempDir, "static", "css", "style.css"), "body { color: blue; }");

            var builder = CreateBuilder();
            var result = await builder.BuildAsync(tempDir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Warnings).Any(item => item.Contains("overrides same file from theme 'default'"));
            var output = await File.ReadAllTextAsync(Path.Combine(tempDir, "_site", "assets", "css", "style.css"));
            await Assert.That(output).Contains("color: blue");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Development_PrunesDeletedOutputFiles()
    {
        var tempDir = CreateSiteWithTwoPosts();

        try
        {
            var builder = CreateBuilder();

            var firstBuild = await builder.BuildAsync(tempDir, false, BuildEnvironment.Development, CancellationToken.None);
            await Assert.That(firstBuild.Success).IsTrue();

            var firstPostOutput = Path.Combine(tempDir, "_site", "blog", "first", "index.html");
            var secondPostOutput = Path.Combine(tempDir, "_site", "blog", "second", "index.html");
            await Assert.That(File.Exists(firstPostOutput)).IsTrue();
            await Assert.That(File.Exists(secondPostOutput)).IsTrue();

            File.Delete(Path.Combine(tempDir, "content", "posts", "first.md"));

            var secondBuild = await builder.BuildAsync(tempDir, false, BuildEnvironment.Development, CancellationToken.None);
            await Assert.That(secondBuild.Success).IsTrue();

            await Assert.That(File.Exists(firstPostOutput)).IsFalse();
            await Assert.That(File.Exists(secondPostOutput)).IsTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task BuildAsync_NoIndex_AddsRobotsMetaTagForHiddenPostsAcrossBuildModes()
    {
        var tempDir = CreateSiteWithNoIndexPosts();

        try
        {
            var builder = CreateBuilder();
            foreach (var environment in new[] { BuildEnvironment.Development, BuildEnvironment.Production })
            {
                var result = await builder.BuildAsync(tempDir, false, environment, CancellationToken.None);
                await Assert.That(result.Success).IsTrue();

                var hiddenOutput = Path.Combine(tempDir, "_site", "blog", "hidden-post", "index.html");
                var visibleOutput = Path.Combine(tempDir, "_site", "blog", "visible-post", "index.html");

                var hiddenHtml = await File.ReadAllTextAsync(hiddenOutput);
                var visibleHtml = await File.ReadAllTextAsync(visibleOutput);

                await Assert.That(hiddenHtml).Contains("robots", StringComparison.OrdinalIgnoreCase);
                await Assert.That(hiddenHtml).Contains("noindex", StringComparison.OrdinalIgnoreCase);
                await Assert.That(hiddenHtml).Contains("nofollow", StringComparison.OrdinalIgnoreCase);
                await Assert.That(visibleHtml).DoesNotContain("noindex", StringComparison.OrdinalIgnoreCase);
                await Assert.That(visibleHtml).DoesNotContain("nofollow", StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task BuildAsync_NoIndex_WithoutHeadTag_EmitsWarningAndLeavesHtmlUntouched()
    {
        var tempDir = CreateSiteWithNoIndexPosts(layout: "<html><body>{{ page.content }}</body></html>");

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(tempDir, false, BuildEnvironment.Development, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Warnings).Any(item => item.Contains("noIndex: true but no </head> tag was found"));

            var hiddenOutput = Path.Combine(tempDir, "_site", "blog", "hidden-post", "index.html");
            var hiddenHtml = await File.ReadAllTextAsync(hiddenOutput);
            await Assert.That(hiddenHtml).DoesNotContain("<meta name=\"robots\" content=\"noindex, nofollow\">");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static string CreateSiteWithThemeAsset()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-assets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "static", "css"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello.md"),
            """
            ---
            title: Hello
            ---
            Content
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

        File.WriteAllText(Path.Combine(dir, "themes", "default", "static", "css", "style.css"),
            "body { color: red; }");

        return dir;
    }

    private static string CreateSiteWithPageBundle()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-bundle-{Guid.NewGuid():N}");
        var bundleDir = Path.Combine(dir, "content", "posts", "with-image");
        Directory.CreateDirectory(bundleDir);
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
            """);

        File.WriteAllText(Path.Combine(bundleDir, "index.md"),
            """
            ---
            title: Post With Image
            imageOptimization: false
            ---
            Content
            """);

        File.WriteAllText(Path.Combine(bundleDir, "hero.txt"), "asset content");
        File.WriteAllText(Path.Combine(dir, "link-target.txt"), "external target");
        Directory.CreateDirectory(Path.Combine(dir, "link-target-directory"));
        File.WriteAllText(Path.Combine(dir, "link-target-directory", "nested.txt"), "external target");
        Directory.CreateSymbolicLink(Path.Combine(bundleDir, "linked-directory"), Path.Combine(dir, "link-target-directory"));
        File.CreateSymbolicLink(Path.Combine(bundleDir, "linked-file.txt"), Path.Combine(dir, "link-target.txt"));
        WriteBundleAsset(bundleDir, "images/screens/x.jpg", "image bytes");
        WriteBundleAsset(bundleDir, "images/screens/logo.txt", "image logo");
        WriteBundleAsset(bundleDir, "downloads/setup.zip", "archive");
        WriteBundleAsset(bundleDir, "downloads/logo.txt", "download logo");
        WriteBundleAsset(bundleDir, "child/index.md", "not a nested item");
        WriteBundleAsset(bundleDir, "child/photo.png", "adjacent image");
        WriteBundleAsset(bundleDir, "notes/article.md", "not a nested item");
        WriteBundleAsset(bundleDir, "notes/data.zip", "adjacent download");
        WriteBundleAsset(bundleDir, "images/manual/README.MD", "not a nested item");
        WriteBundleAsset(bundleDir, "images/manual/diagram.svg", "adjacent image");
        WriteBundleAsset(bundleDir, ".secret", "hidden");
        WriteBundleAsset(bundleDir, ".private/picture.jpg", "hidden");
        WriteBundleAsset(bundleDir, "images/.cache/x.jpg", "hidden");

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

        return dir;
    }

    private static void WriteBundleAsset(string bundleDir, string relativePath, string content)
    {
        var path = Path.Combine(bundleDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string CreateSiteWithTwoPosts()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-prune-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "first.md"),
            """
            ---
            title: First
            ---
            first
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "second.md"),
            """
            ---
            title: Second
            ---
            second
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html><body>{{ page.content }}</body></html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html><body>Not Found</body></html>");

        return dir;
    }

    private static string CreateSiteWithNoIndexPosts(string? layout = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-noindex-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "visible-post.md"),
            """
            ---
            title: Visible Post
            ---
            visible content
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hidden-post.md"),
            """
            ---
            title: Hidden Post
            noIndex: true
            ---
            hidden content
            """);

        var defaultLayout = layout ?? "<html><head><title>{{ page.title }}</title></head><body>{{ page.content }}</body></html>";
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"), defaultLayout);
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html><body>Not Found</body></html>");

        return dir;
    }

    private static ISiteBuilder CreateBuilder()
    {
        var markdownProcessor = new MarkdownProcessor();
        var contentReader = new ContentReader(markdownProcessor);
        var templateRenderer = new TemplateRenderer();
        var permalinkGenerator = new PermalinkGenerator();
        var configLoader = new SiteConfigLoader();
        var pluginLoader = new PluginLoader();
        return new SiteBuilder(
            contentReader,
            templateRenderer,
            permalinkGenerator,
            configLoader,
            pluginLoader,
            [new NuglifyAssetMinifier(), new NoOpAssetMinifier()],
            new SkiaSharpImageOptimizer(),
            new AssetReferenceIndexBuilder());
    }

    private static string CreateSiteWithNestedContent()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-nested-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "guides", "advanced"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              docs:
                directory: content
                permalink: /:slug/
            """);

        File.WriteAllText(Path.Combine(dir, "content", "top.md"),
            """
            ---
            title: Top Level
            ---
            top
            """);

        File.WriteAllText(Path.Combine(dir, "content", "guides", "getting-started.md"),
            """
            ---
            title: Getting Started
            ---
            guide content
            """);

        File.WriteAllText(Path.Combine(dir, "content", "guides", "advanced", "config.md"),
            """
            ---
            title: Config
            ---
            deep content
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html><body>{{ page.content }}</body></html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html><body>Not Found</body></html>");

        return dir;
    }

    private static string CreateSiteWithCollision()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-collision-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /:slug/
            """);

        // Two files that will produce the same URL because they have the same slug
        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello.md"),
            """
            ---
            title: Hello One
            slug: hello
            ---
            Content one
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello2.md"),
            """
            ---
            title: Hello Two
            slug: hello
            ---
            Content two
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

        return dir;
    }
}
