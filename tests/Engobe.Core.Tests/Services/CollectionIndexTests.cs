namespace Engobe.Core.Tests.Services;

using Engobe.Services;

public class CollectionIndexTests
{
    [Test]
    public async Task BuildAsync_CollectionWithPaginate_IndexRendered()
    {
        var dir = CreateSiteWithPaginatedCollection();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "blog", "index.html"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_CollectionIndex_ContainsItemLink()
    {
        var dir = CreateSiteWithPaginatedCollection();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();
            var indexHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "index.html"));
            await Assert.That(indexHtml).Contains("Hello World");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_UnpaginatedCollectionIndex_ContainsAllItemsOnOnePage()
    {
        var dir = CreateSiteWithPaginatedCollection(paginate: false, itemCount: 3);

        try
        {
            var result = await CreateBuilder().BuildAsync(dir);
            var indexPath = Path.Combine(dir, "_site", "blog", "index.html");

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.Exists(indexPath)).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "_site", "blog", "page", "2", "index.html"))).IsFalse();

            var html = await File.ReadAllTextAsync(indexPath);
            await Assert.That(html).Contains("page=1/1");
            await Assert.That(html).Contains("Hello World");
            await Assert.That(html).Contains("Post 2");
            await Assert.That(html).Contains("Post 3");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateSiteWithPaginatedCollection(bool paginate = true, int itemCount = 1)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"engobe-idx-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));

        var paginateClause = paginate ? "paginate: 10" : "";
        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            $"""
            title: Test Site
            baseUrl: http://localhost:5555
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
                {paginateClause}
            """);

        for (var i = 1; i <= itemCount; i++)
        {
            var title = i == 1 ? "Hello World" : $"Post {i}";
            File.WriteAllText(Path.Combine(dir, "content", "posts", $"post-{i}.md"),
            $"""
            ---
            title: {title}
            ---
            Content
            """);
        }

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "{{ page.content }}");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "posts-index.html"),
            """
            page={{ paginator.page }}/{{ paginator.total_pages }}
            {{ for item in paginator.items }}<a href="{{ item.url }}">{{ item.title }}</a>{{ end }}
            """);

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
