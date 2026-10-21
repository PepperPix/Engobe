namespace Kiln.Core.Tests.Services;

using Kiln.Abstractions;
using Kiln.Services;

public class SearchUiIntegrationTests
{
    [Test]
    public async Task Build_SearchEnabled_RendersSearchUi()
    {
        var dir = CreateSiteWithSearch(enabled: true);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();

            var indexHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "index", "index.html"));

            await Assert.That(indexHtml).Contains("id=\"search\"");
            await Assert.That(indexHtml).Contains("/pagefind/pagefind.js");
            await Assert.That(indexHtml).DoesNotContain("/assets/pagefind/pagefind.js");
            await Assert.That(indexHtml).Contains("js/kiln-search.js");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Build_SearchDisabled_OmitsSearchUi()
    {
        var dir = CreateSiteWithSearch(enabled: false);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();

            var indexHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "index", "index.html"));

            await Assert.That(indexHtml).DoesNotContain("pagefind/pagefind.js");
            await Assert.That(indexHtml).DoesNotContain("js/kiln-search.js");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Build_CdnPrefixAndSubfolder_KeepPagefindLocal()
    {
        var dir = CreateSiteWithSearch(
            enabled: true,
            baseUrl: "https://example.org/docs/",
            assetPrefix: "https://cdn.example.com/v2/media/");

        try
        {
            var result = await CreateBuilder().BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();
            var indexHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "index", "index.html"));
            await Assert.That(indexHtml).Contains("data-pagefind-module=/docs/pagefind/pagefind.js");
            await Assert.That(indexHtml).Contains("href=/docs/pagefind/pagefind-ui.css");
            await Assert.That(indexHtml).Contains("https://cdn.example.com/v2/media/js/kiln-search.");
            await Assert.That(indexHtml).DoesNotContain("cdn.example.com/v2/media/pagefind/");
            await Assert.That(result.Warnings.Any(static warning => warning.Contains("deferred until search indexing", StringComparison.Ordinal))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments(true, "/docs/pagefind/pagefind-ui.css", true)]
    [Arguments(true, "https://example.org/docs/pagefind/pagefind.js?v=2#module", true)]
    [Arguments(false, "/docs/pagefind/pagefind-ui.css", false)]
    [Arguments(true, "/docs/pagefind/missing.css", false)]
    [Arguments(true, "/docs/pagefinder/pagefind-ui.css", false)]
    [Arguments(true, "/pagefind/pagefind-ui.css", false)]
    [Arguments(true, "/docs/pagefind/PAGEFIND-ui.css", false)]
    [Arguments(true, "https://cdn.example.com/v2/media/missing.css", false)]
    public async Task Build_OnlyDefersKnownLocalPagefindEntrypointsWhenSearchEnabled(bool enabled, string reference, bool expectedSuccess)
    {
        var dir = CreateSiteWithSearch(enabled, baseUrl: "https://example.org/docs/", assetPrefix: "https://cdn.example.com/v2/media/");
        try
        {
            var contentPath = Path.Combine(dir, "content", "pages", "index.md");
            await File.AppendAllTextAsync(contentPath, $"\n<link rel=\"stylesheet\" href=\"{reference}\">");
            var result = await CreateBuilder().BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
            await Assert.That(result.Success).IsEqualTo(expectedSuccess);
            if (expectedSuccess)
            {
                await Assert.That(result.Warnings.Any(static warning =>
                    warning.Contains("deferred until search indexing", StringComparison.Ordinal))).IsTrue();
            }
            else
            {
                await Assert.That(string.Join("; ", result.Errors)).Contains(reference);
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Build_ScaffoldedCdnSiteWithSearch_PassesChecksBeforeIndexing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"kiln-scaffold-search-{Guid.NewGuid():N}");
        try
        {
            var site = new Scaffolder().CreateSite("test", root);
            var configPath = Path.Combine(site.ProjectPath, "site.yaml");
            await File.WriteAllTextAsync(configPath, (await File.ReadAllTextAsync(configPath))
                .Replace("baseUrl: http://localhost:5555", "baseUrl: https://example.org/docs/", StringComparison.Ordinal)
                .Replace("assetPrefix: /assets/", "assetPrefix: https://cdn.example.com/v2/media/", StringComparison.Ordinal)
                + "\nsearch:\n  enabled: true\n");
            var result = await CreateBuilder().BuildAsync(site.ProjectPath, false, BuildEnvironment.Production, CancellationToken.None);
            await Assert.That(result.Errors).IsEmpty();
            await Assert.That(result.Success).IsTrue();
            var outputDir = Path.Combine(site.ProjectPath, "_site");
            var searchScript = Directory.GetFiles(Path.Combine(outputDir, "assets", "js"), "kiln-search.*.js").Single();
            var html = await File.ReadAllTextAsync(Path.Combine(outputDir, "index.html"));
            await Assert.That(html).Contains("https://cdn.example.com/v2/media/js/" + Path.GetFileName(searchScript));
            await Assert.That(html).Contains("/docs/pagefind/pagefind-ui.css");
            await Assert.That(html).Contains("/docs/pagefind/pagefind.js");
            await Assert.That(Directory.Exists(Path.Combine(outputDir, "pagefind"))).IsFalse();
            await Assert.That(result.Warnings.Any(static warning =>
                warning.Contains("/pagefind/pagefind-ui.css", StringComparison.Ordinal))).IsTrue();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateSiteWithSearch(
        bool enabled,
        string baseUrl = "http://localhost:5555",
        string? assetPrefix = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-search-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "pages"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "static", "js"));

        var searchYaml = enabled ? "  enabled: true\n" : "";
        var assetPrefixYaml = assetPrefix is null ? string.Empty : $"assetPrefix: {assetPrefix}\n";

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            $$"""
            title: Test Site
            baseUrl: {{baseUrl}}
            {{assetPrefixYaml}}
            collections:
              pages:
                directory: content/pages
                permalink: /:slug/
            search:
            {{searchYaml}}
            """);
        File.WriteAllText(Path.Combine(dir, "themes", "default", "static", "js", "kiln-search.js"), "search module marker");

        File.WriteAllText(Path.Combine(dir, "content", "pages", "index.md"),
            """
            ---
            title: Home
            ---
            Home page content
            """);

        // Layout that includes the search partial
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            """
            <html>
            {{ include "search" }}
            {{ page.content }}
            </html>
            """);
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

        // Search partial (same as real theme)
        File.WriteAllText(Path.Combine(dir, "themes", "default", "partials", "search.html"),
            """
            {{ if site.search.enabled }}
            <link href="{{ site.base_path }}/pagefind/pagefind-ui.css" rel="stylesheet" />
            <div id="search" class="kiln-search" data-pagefind-root data-pagefind-module="{{ site.base_path }}/pagefind/pagefind.js"></div>
            <script src="{{ asset_url 'js/kiln-search.js' }}" defer></script>
            {{ end }}
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
        return new SiteBuilder(
            contentReader,
            templateRenderer,
            permalinkGenerator,
            configLoader,
            pluginLoader,
            [new NuglifyAssetMinifier()],
            new SkiaSharpImageOptimizer(),
            new AssetReferenceIndexBuilder());
    }
}
