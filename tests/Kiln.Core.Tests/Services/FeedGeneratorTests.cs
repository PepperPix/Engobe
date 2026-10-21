namespace Kiln.Core.Tests.Services;

using System.Xml.Linq;
using Kiln.Services;

public class FeedGeneratorTests
{
    [Test]
    public async Task BuildAsync_GeneratesFeedXml()
    {
        var dir = CreateSiteWithFeedCollection();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "blog", "feed.xml"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Feed_IsAtomFormat()
    {
        var dir = CreateSiteWithFeedCollection();

        try
        {
            var builder = CreateBuilder();
            await builder.BuildAsync(dir);

            var feed = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "feed.xml"));

            await Assert.That(feed).Contains("http://www.w3.org/2005/Atom");
            await Assert.That(feed).Contains("<entry>");
            await Assert.That(feed).Contains("<feed");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Feed_ContainsItemEntry()
    {
        var dir = CreateSiteWithFeedCollection();

        try
        {
            var builder = CreateBuilder();
            await builder.BuildAsync(dir);

            var feed = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "feed.xml"));

            await Assert.That(feed).Contains("Hello World");
            await Assert.That(feed).Contains("http://localhost:5555/blog/hello-world/");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_FeedProjectsAssetContentWithoutChangingArticleLinks()
    {
        var dir = CreateSiteWithFeedCollection();

        try
        {
            var configPath = Path.Combine(dir, "site.yaml");
            var yaml = await File.ReadAllTextAsync(configPath);
            await File.WriteAllTextAsync(
                configPath,
                yaml.Replace("baseUrl: http://localhost:5555", "baseUrl: https://example.org/docs/", StringComparison.Ordinal)
                    .Replace("collections:\n", "assetPrefix: /media/\ncollections:\n", StringComparison.Ordinal));
            await File.WriteAllTextAsync(
                Path.Combine(dir, "content", "posts", "hello-world.md"),
                """
                ---
                title: Hello World
                date: 2026-01-01
                ---
                <img src="/assets/images/hero.svg?variant=small&amp;mode=detail#preview" srcset="/assets/images/hero.svg 1x" poster="/assets/images/poster.svg">
                """);

            var result = await CreateBuilder().BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            var feed = XDocument.Load(Path.Combine(dir, "_site", "blog", "feed.xml"));
            var feedHtml = feed.Descendants().Single(element => element.Name.LocalName == "content").Value;
            await Assert.That(feedHtml).Contains("https://example.org/docs/media/images/hero.svg?variant=small&amp;mode=detail#preview");
            await Assert.That(feedHtml).Contains("https://example.org/docs/media/images/hero.svg 1x");
            await Assert.That(feedHtml).Contains("poster=\"https://example.org/docs/media/images/poster.svg\"");
            await Assert.That(feed.Descendants().Single(element => element.Name.LocalName == "link"
                    && (string?)element.Attribute("rel") == "alternate")
                .Attribute("href")!.Value).IsEqualTo("https://example.org/docs/blog/");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Feed_ExcludesDrafts()
    {
        var dir = CreateSiteWithFeedCollection();

        try
        {
            var builder = CreateBuilder();
            await builder.BuildAsync(dir);

            var feed = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "feed.xml"));

            await Assert.That(feed).DoesNotContain("Draft Post");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_NoFeed_WhenFeedFalse()
    {
        var dir = CreateSiteWithoutFeedCollection();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "blog", "feed.xml"))).IsFalse();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateSiteWithFeedCollection()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-feed-{Guid.NewGuid():N}");
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
                feed: true
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello-world.md"),
            """
            ---
            title: Hello World
            date: 2026-01-01
            description: A test post
            ---
            Content
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "draft-post.md"),
            """
            ---
            title: Draft Post
            draft: true
            ---
            Draft
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

        return dir;
    }

    private static string CreateSiteWithoutFeedCollection()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-nofeed-{Guid.NewGuid():N}");
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

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello-world.md"),
            """
            ---
            title: Hello World
            ---
            Content
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html>{{ page.content }}</html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

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
        return new SiteBuilder(contentReader, templateRenderer, permalinkGenerator, configLoader, pluginLoader, [], new SkiaSharpImageOptimizer(), new AssetReferenceIndexBuilder());
    }
}
