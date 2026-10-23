namespace Engobe.Core.Tests.Services;

using Engobe.Abstractions;
using Engobe.Models;
using Engobe.Services;
using System.Collections.ObjectModel;

public class AssetAddressingIntegrationTests
{
    [Test]
    [Arguments("/assets/", "/docs/assets/")]
    [Arguments("/media", "/docs/media/")]
    [Arguments("https://cdn.example.org/v2/media", "https://cdn.example.org/v2/media/")]
    [Arguments("http://cdn.example.org:8080/assets/", "http://cdn.example.org:8080/assets/")]
    public async Task BuildAsync_Production_ComposesAllAssetHelpersAndCanonicalContent(string prefix, string expectedBase)
    {
        var dir = CreateSite(prefix);
        try
        {
            var result = await CreateBuilder().BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
            await Assert.That(result.Errors).IsEmpty();
            await Assert.That(result.Success).IsTrue();
            var html = await ReadPageAsync(dir);
            await Assert.That(html).Contains($"{expectedBase}css/site.css");
            await Assert.That(html).Contains($"{expectedBase}plugins/demo/js/a.js");
            await Assert.That(html).Contains($"{expectedBase}content/posts/post/hero.svg");
            await Assert.That(html).Contains($"{expectedBase}|/docs/blog/post/");
            await Assert.That(html).Contains($"{expectedBase}images/hero%20image.svg?v=2&amp;mode=detail#hero");
            await Assert.That(html).DoesNotContain("/docs/docs/");
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "assets", "images", "hero image.svg"))).IsTrue();
            await Assert.That(Directory.Exists(Path.Combine(dir, "_site", "v2"))).IsFalse();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Production_InverseMappingHonorsOriginPathPortAndEncoding()
    {
        var dir = CreateSite("https://cdn.example.org/v2/media/");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "content", "posts", "post", "index.md"),
                """
                ---
                title: Post
                layout: post
                ---
                <img src="https://cdn.example.org/v2/media/images/hero%20image.svg?v=2#hero">
                <img src="https://cdn.example.org/v2/media-other/missing.svg">
                <img src="https://cdn.example.org.evil/v2/media/missing.svg">
                <img src="https://cdn.example.org:444/v2/media/missing.svg">
                <img src="http://cdn.example.org/v2/media/missing.svg">
                """);
            var configPath = Path.Combine(dir, "site.yaml");
            await File.WriteAllTextAsync(configPath, (await File.ReadAllTextAsync(configPath))
                .Replace("linkCheck: false", "linkCheck: true", StringComparison.Ordinal));
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
            await Assert.That(result.Success).IsTrue();
            var html = await ReadPageAsync(dir);
            await Assert.That(html).Contains("https://cdn.example.org/v2/media/images/hero%20image.svg?v=2#hero");
            await Assert.That(html).Contains("https://cdn.example.org/v2/media-other/missing.svg");
            await Assert.That(html).Contains("https://cdn.example.org.evil/v2/media/missing.svg");
            await Assert.That(html).Contains("https://cdn.example.org:444/v2/media/missing.svg");
            await Assert.That(html).Contains("http://cdn.example.org/v2/media/missing.svg");

            File.Delete(Path.Combine(dir, "static", "images", "hero image.svg"));
            var missing = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
            await Assert.That(missing.Success).IsFalse();
            await Assert.That(missing.Errors.Any(static error =>
                error.Contains("hero%20image.svg", StringComparison.Ordinal))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_RepeatedModesDoNotChangeLoadedConfigurationOrCanonicalContent()
    {
        var dir = CreateSite("https://cdn.example.org/v2/media/");
        try
        {
            var loader = new SiteConfigLoader();
            var config = loader.Load(dir);
            var contentReader = new ContentReader(new MarkdownProcessor());
            var items = contentReader.ReadCollection(config.Collections["posts"], dir);
            var canonical = items.Single().HtmlContent;
            var builder = new SiteBuilder(
                new FixedContentReader(items), new TemplateRenderer(), new PermalinkGenerator(), new FixedSiteConfigLoader(config),
                new PluginLoader(), [new NoOpAssetMinifier()], new SkiaSharpImageOptimizer(),
                new AssetReferenceIndexBuilder());
            foreach (var environment in new[] { BuildEnvironment.Development, BuildEnvironment.Production, BuildEnvironment.Development })
            {
                var result = await builder.BuildAsync(dir, false, environment, CancellationToken.None);
                await Assert.That(result.Success).IsTrue();
                var expectedBase = environment == BuildEnvironment.Production
                    ? "https://cdn.example.org/v2/media/"
                    : "/docs/assets/";
                await Assert.That(await ReadPageAsync(dir)).Contains(expectedBase + "css/site.css");
                await Assert.That(config.AssetPrefix).IsEqualTo("https://cdn.example.org/v2/media/");
                await Assert.That(items.Single().HtmlContent).IsEqualTo(canonical);
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments("../secret.svg")]
    [Arguments("%2e%2e/secret.svg")]
    public async Task BuildAsync_RejectsEscapingAssetHelperKeys(string key)
    {
        var dir = CreateSite("/media/");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "layouts", "post.html"),
                $"<html><body>{{{{ asset_url '{key}' }}}}</body></html>");
            var result = await CreateBuilder().BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors).IsNotEmpty();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "secret.svg"))).IsFalse();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_ResolvesRelativeCssTargetsWithoutEscapingAssets()
    {
        var dir = CreateSite("/media/");
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "static", "css", "site.css"),
                ".font { src: url('../fonts/main%20font.woff2?v=2#font'); } .foreign { src: url('../../../secret.css'); }");
            var result = await CreateBuilder().BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
            await Assert.That(result.Success).IsTrue();
            var css = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "assets", "css", "site.css"));
            await Assert.That(css).Contains("/docs/media/fonts/main%20font.woff2?v=2#font");
            await Assert.That(css).Contains("../../../secret.css");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CreateSite(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"engobe-addressing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts", "post"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "static", "css"));
        Directory.CreateDirectory(Path.Combine(dir, "static", "images"));
        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            $"""
            title: Test
            baseUrl: https://example.org/docs/
            assetPrefix: {prefix}
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
            assets:
              minifier: noop
            build:
              fingerprint: false
              linkCheck: false
              minify:
                css: false
                js: false
                svg: false
                html: false
            """);
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "post.html"),
            "<html><body>{{ asset_url 'css/site.css' }}|{{ plugin_asset_url 'demo' 'js/a.js' }}|"
            + "{{ page_asset_url 'hero.svg' }}|{{ site.asset_prefix }}|{{ page.url }}|{{ page.content }}</body></html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"), "<html><body>{{ page.content }}</body></html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"), "<html><body>Not found</body></html>");
        File.WriteAllText(Path.Combine(dir, "content", "posts", "post", "index.md"),
            "---\ntitle: Post\nlayout: post\n---\n<img src=\"/assets/images/hero%20image.svg?v=2&amp;mode=detail#hero\">");
        File.WriteAllText(Path.Combine(dir, "static", "css", "site.css"), "body { color: black; }");
        File.WriteAllText(Path.Combine(dir, "static", "images", "hero image.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
        return dir;
    }

    private static Task<string> ReadPageAsync(string dir) =>
        File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "post", "index.html"));

    private static ISiteBuilder CreateBuilder() =>
        new SiteBuilder(
            new ContentReader(new MarkdownProcessor()), new TemplateRenderer(), new PermalinkGenerator(),
            new SiteConfigLoader(), new PluginLoader(), [new NoOpAssetMinifier()],
            new SkiaSharpImageOptimizer(), new AssetReferenceIndexBuilder());

    private sealed class FixedSiteConfigLoader(SiteConfiguration configuration) : ISiteConfigLoader
    {
        public SiteConfiguration Load(string projectPath) => configuration;
    }

    private sealed class FixedContentReader(IReadOnlyList<ContentItem> items) : IContentReader
    {
        public IReadOnlyList<ContentItem> ReadCollection(
            ContentGroup collection,
            string projectPath,
            IReadOnlyList<PluginDefinition>? plugins = null,
            Collection<string>? warnings = null) => items;

        public ContentItem ReadSingleFile(
            string absoluteFilePath,
            ContentGroup owningCollection,
            IReadOnlyList<PluginDefinition>? plugins = null,
            Collection<string>? warnings = null) =>
            throw new NotSupportedException("This fixture contains collection content only.");
    }
}
