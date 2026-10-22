namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class TemplateRendererTests
{
    private readonly TemplateRenderer _renderer = new();

    [Test]
    [Arguments("guides/overview.md", "/")]
    [Arguments("guides/overview.md", "/site")]
    [Arguments("a869d67a-e4e1-48c5-a08c-75241df633d1", "/")]
    [Arguments("a869d67a-e4e1-48c5-a08c-75241df633d1", "/site")]
    public async Task Render_ParentChain_UsesEffectiveAncestorsAndStructuralSection(string reference, string basePath)
    {
        var theme = CreateTempTheme(
            "{{ for a in page.ancestors }}{{ a.title }}|{{ a.url }};{{ end }}SECTION:{{ page.section.title }}|{{ page.section.path }}|{{ page.section.url }}",
            "default");
        try
        {
            var collection = CreateTestCollection();
            var parent = CreateNavigationItem(collection, "Overview", "guides/overview.md", "guides", "/blog/guides/overview/",
                id: "a869d67a-e4e1-48c5-a08c-75241df633d1");
            var middle = CreateNavigationItem(collection, "Middle", "middle.md", "", "/elsewhere/", parent: reference);
            var child = CreateNavigationItem(collection, "Child", "old/deep-section/child.md", "old/deep-section",
                "/blog/old/deep-section/child/", parent: "middle.md");
            var site = CreateTestSite(collection, basePath);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>(),
                NavigationTreeBuilder.Build([parent, middle, child], site.BasePath));

            var html = _renderer.Render(child, shared, site, theme, []);
            var prefix = site.BasePath;
            await Assert.That(html).IsEqualTo(
                $"Posts|{prefix}/blog/;Guides|{prefix}/blog/guides/;Overview|{prefix}/blog/guides/overview/;Middle|{prefix}/elsewhere/;"
                + $"SECTION:Deep Section|old/deep-section|{prefix}/blog/old/deep-section/");
        }
        finally
        {
            Directory.Delete(theme, true);
        }
    }

    [Test]
    [Arguments("", "/")]
    [Arguments("", "/site")]
    [Arguments("guides/deep-section", "/")]
    [Arguments("guides/deep-section", "/site")]
    public async Task Render_Section_IsNullAtRootOrDescribesDeepestDirectory(string section, string basePath)
    {
        var theme = CreateTempTheme(
            "{{ if page.section == null }}NULL{{ else }}{{ page.section.title }}|{{ page.section.path }}|{{ page.section.url }}{{ end }}",
            "default");
        try
        {
            var collection = CreateTestCollection();
            var sectionPrefix = section.Length == 0 ? "" : section + "/";
            var item = CreateNavigationItem(collection, "Child", sectionPrefix + "child.md", section, $"/blog/{sectionPrefix}child/");
            var site = CreateTestSite(collection, basePath);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>(),
                NavigationTreeBuilder.Build([item], site.BasePath));
            var html = _renderer.Render(item, shared, site, theme, []);
            await Assert.That(html).IsEqualTo(section.Length == 0
                ? "NULL" : $"Deep Section|guides/deep-section|{site.BasePath}/blog/guides/deep-section/");
        }
        finally
        {
            Directory.Delete(theme, true);
        }
    }

    [Test]
    [Arguments("/", null)]
    [Arguments("/site", null)]
    [Arguments("/", "parent.md")]
    [Arguments("/site", "parent.md")]
    public async Task Render_Section_UsesNavigationUrlForOverriddenItem(string basePath, string? parentReference)
    {
        var theme = CreateTempTheme(
            "{{ page.section.title }}|{{ page.section.path }}|{{ page.section.url }}|{{ navtree.posts[0].url }}",
            "default");
        try
        {
            var collection = CreateTestCollection();
            var overridden = CreateNavigationItem(collection, "Custom", "guides/custom.md", "guides", "/custom/",
                parent: parentReference);
            overridden.Extra["permalink_override"] = "/custom/";
            var normal = CreateNavigationItem(collection, "Normal", "guides/normal.md", "guides", "/blog/guides/normal/");
            var parent = CreateNavigationItem(collection, "Parent", "parent.md", "", "/blog/parent/");
            var site = CreateTestSite(collection, basePath);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>(),
                NavigationTreeBuilder.Build([overridden, normal, parent], site.BasePath));

            var html = _renderer.Render(overridden, shared, site, theme, []);

            await Assert.That(html).IsEqualTo(
                $"Guides|guides|{site.BasePath}/blog/guides/|{site.BasePath}/blog/guides/");
        }
        finally
        {
            Directory.Delete(theme, true);
        }
    }

    [Test]
    [Arguments("/")]
    [Arguments("/site")]
    public async Task Render_Section_UsesStructuralFallbackWhenOverrideRemovesSection(string basePath)
    {
        var theme = CreateTempTheme("{{ page.section.title }}|{{ page.section.path }}|{{ page.section.url }}", "default");
        try
        {
            var collection = CreateTestCollection();
            var parent = CreateNavigationItem(collection, "Parent", "parent.md", "", "/blog/parent/");
            var child = CreateNavigationItem(collection, "Child", "guides/child.md", "guides", "/custom/", parent: "parent.md");
            child.Extra["permalink_override"] = "/custom/";
            var site = CreateTestSite(collection, basePath);
            var navTree = NavigationTreeBuilder.Build([parent, child], site.BasePath);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>(), navTree);

            var html = _renderer.Render(child, shared, site, theme, []);

            await Assert.That(navTree["posts"].Count).IsEqualTo(1);
            await Assert.That(navTree["posts"][0].Title).IsEqualTo("Parent");
            await Assert.That(html).IsEqualTo($"Guides|guides|{site.BasePath}/guides/");
        }
        finally
        {
            Directory.Delete(theme, true);
        }
    }

    private static ContentItem CreateNavigationItem(
        ContentGroup collection, string title, string path, string section, string url, string? parent = null, string? id = null)
        => new()
        {
            Title = title, SourcePath = path, RelativePath = path, SectionPath = section,
            Slug = title.ToLowerInvariant(), RawContent = "", HtmlContent = "",
            Url = new Uri(url, UriKind.Relative), OutputPath = url.Trim('/') + "/index.html",
            Collection = collection, Parent = parent, Id = id
        };

    [Test]
    public async Task Render_AppliesLayoutWithSiteAndPageData()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html><title>{{ page.title }} \u2014 {{ site.title }}</title><body>{{ page.content }}</body></html>",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection();
            var item = CreateTestItem("<p>Hello</p>", collection);
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("<title>Test Post — Test Site</title>");
            await Assert.That(result).Contains("<p>Hello</p>");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_FallsBackToDefaultLayout()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html><title>{{ page.title }} — {{ site.title }}</title><body>{{ page.content }}</body></html>",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection(layout: "nonexistent");
            var item = CreateTestItem("<p>Hello</p>", collection);
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            // Should fall back to default layout since "nonexistent.html" doesn't exist
            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("<title>Test Post — Test Site</title>");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_ThrowsForMissingLayoutAndNoDefault()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html></html>",
            layoutName: "other");
        var collection = CreateTestCollection(layout: "nonexistent");
        var item = CreateTestItem("<p>Hello</p>", collection);
        var site = CreateTestSite(collection);
        var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

        await Assert.That(() => _renderer.Render(item, shared, site, tempTheme, []))
            .ThrowsExactly<FileNotFoundException>();
    }

    [Test]
    public async Task Render_ProvidesNoIndexFlagOnPageObject()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html><head>{{ page.no_index }}</head></html>",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection();
            var item = CreateTestItem("<p>Hello</p>", collection);
            var noIndexItem = new ContentItem
            {
                SourcePath = item.SourcePath,
                RelativePath = item.RelativePath,
                Title = item.Title,
                Date = item.Date,
                Slug = item.Slug,
                SectionPath = item.SectionPath,
                Layout = item.Layout,
                RawContent = item.RawContent,
                HtmlContent = item.HtmlContent,
                Url = item.Url,
                OutputPath = item.OutputPath,
                Collection = item.Collection,
                NoIndex = true
            };
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(noIndexItem, shared, site, tempTheme, []);

            await Assert.That(result).Contains("true");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_AssetUrlFunctionResolvesPath()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html><head>{{ asset_url 'css/style.css' }}</head></html>",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection();
            var item = CreateTestItem("<p>Hello</p>", collection);
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("/assets/css/style.css");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_PageAssetUrlFunctionResolvesColocatedAsset()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html>{{ page_asset_url 'hero.jpg' }}</html>",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection();
            var item = CreateTestItem("<p>Hello</p>", collection);
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("/assets/content/posts/test-post/hero.jpg");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_PageAssetUrl_IncludesSectionPathForNestedBundle()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html>{{ page_asset_url 'hero.jpg' }}</html>",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection();
            var item = CreateTestItem("<p>Hello</p>", collection, sectionPath: "guides/advanced");
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("/assets/content/posts/guides/advanced/test-post/hero.jpg");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_AssetUrlFunction_RespectsConfiguredBasePath()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html><head>{{ asset_url 'css/style.css' }}</head></html>",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection();
            var item = CreateTestItem("<p>Hello</p>", collection);
            var site = CreateTestSite(collection, basePath: "/blog");
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("/blog/assets/css/style.css");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_PublicContextUsesDevelopmentAssetFallback()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html>{{ asset_url 'css/style.css' }}|{{ plugin_asset_url 'demo' 'js/a.js' }}|{{ page_asset_url 'hero.jpg' }}|{{ site.asset_prefix }}|{{ page.url }}|{{ page.content }}</html>",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection();
            var item = CreateTestItem("<img src=\"/assets/images/hero.svg\">", collection);
            var site = CreateTestSite(collection, basePath: "/docs", assetPrefix: "https://cdn.example.com/v2/media");
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("/docs/assets/css/style.css");
            await Assert.That(result).Contains("/docs/assets/plugins/demo/js/a.js");
            await Assert.That(result).Contains("/docs/assets/content/posts/test-post/hero.jpg");
            await Assert.That(result).Contains("/docs/assets/|/docs/blog/test-post/");
            await Assert.That(result).Contains("src=\"/docs/assets/images/hero.svg\"");
            await Assert.That(site.AssetPrefix).IsEqualTo("https://cdn.example.com/v2/media");
            await Assert.That(item.HtmlContent).IsEqualTo("<img src=\"/assets/images/hero.svg\">");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_RootRelativeEffectivePrefixIncludesBasePathOnce()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html>{{ asset_url 'logo.svg' }}|{{ site.asset_prefix }}</html>",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection();
            var item = CreateTestItem("<p>Hello</p>", collection);
            var site = CreateTestSite(collection, basePath: "/docs", assetPrefix: "/media/");
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("/docs/media/logo.svg|/docs/media/");
            await Assert.That(result).DoesNotContain("/docs/docs/");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_ProvidesBasePathVariableAndPrefixedPageUrl()
    {
        var tempTheme = CreateTempTheme(
            layout: "<html>{{ site.base_path }}|{{ page.url }}|{{ page.collection.url }}</html>",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection();
            var item = CreateTestItem("<p>Hello</p>", collection);
            var site = CreateTestSite(collection, basePath: "/reponame");
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("/reponame");
            await Assert.That(result).Contains("/reponame/blog/test-post/");
            await Assert.That(result).Contains("/reponame/blog/");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_LoopOverMoreThanDefaultLoopLimit_RendersAllIterations()
    {
        const int iterations = 1500;
        var tempTheme = CreateTempTheme(
            layout: "{{ for i in 1..1500 }}x{{ end }}",
            layoutName: "default");

        try
        {
            var collection = CreateTestCollection();
            var item = CreateTestItem("<p>Hello</p>", collection);
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result.Count(c => c == 'x')).IsEqualTo(iterations);
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_ContentLargerThanDefaultStringLimit_IsNotTruncated()
    {
        const int contentLength = 2 * 1024 * 1024;
        var tempTheme = CreateTempTheme(layout: "{{ page.content }}", layoutName: "default");

        try
        {
            var collection = CreateTestCollection();
            var item = CreateTestItem(new string('a', contentLength), collection);
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result.Length).IsEqualTo(contentLength);
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    private static string CreateTempTheme(string layout, string layoutName)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-theme-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "partials"));
        File.WriteAllText(Path.Combine(dir, "layouts", $"{layoutName}.html"), layout);
        return dir;
    }

    private static ContentGroup CreateTestCollection(string layout = "default") =>
        new() { Name = "posts", Permalink = "/blog/:slug/", Layout = layout };

    [Test]
    public async Task Render_BreadcrumbAncestors_RendersForFlatItem()
    {
        const string layout = """
            <html>
            {{ for a in page.ancestors }}{{ a.title }}|{{ a.url }},{{ end }}
            </html>
            """;
        var tempTheme = CreateTempTheme(layout, "default");

        try
        {
            var collection = new ContentGroup { Name = "posts", Permalink = "/blog/:slug/", Layout = "default" };
            var item = new ContentItem
            {
                SourcePath = "/test/post.md",
                RelativePath = "post.md",
                Title = "Hello",
                Slug = "hello",
                RawContent = "",
                HtmlContent = "<p>Hi</p>",
                SectionPath = "",
                Url = new Uri("/blog/hello/", UriKind.Relative),
                OutputPath = "blog/hello/index.html",
                Collection = collection
            };
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("Posts|/");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    [Test]
    public async Task Render_BreadcrumbAncestors_RendersNestedSections()
    {
        const string layout = """
            <html>
            {{ for a in page.ancestors }}{{ a.title }}|{{ a.url }} {{ end }}
            </html>
            """;
        var tempTheme = CreateTempTheme(layout, "default");

        try
        {
            var collection = new ContentGroup { Name = "docs", Permalink = "/:slug/", Layout = "default" };
            var item = new ContentItem
            {
                SourcePath = "/test/guides/advanced/config.md",
                RelativePath = "guides/advanced/config.md",
                Title = "Config",
                Slug = "config",
                RawContent = "",
                HtmlContent = "<p>Config</p>",
                SectionPath = "guides/advanced",
                Url = new Uri("/guides/advanced/config/", UriKind.Relative),
                OutputPath = "guides/advanced/config/index.html",
                Collection = collection
            };
            var site = CreateTestSite(collection);
            var shared = SharedRenderContext.Build(site, new Dictionary<string, IReadOnlyList<TaxonomyTerm>>());

            var result = _renderer.Render(item, shared, site, tempTheme, []);

            await Assert.That(result).Contains("Docs|/");
            await Assert.That(result).Contains("Guides|/guides/");
            await Assert.That(result).Contains("Advanced|/guides/advanced/");
            await Assert.That(result).DoesNotContain("Config|");
        }
        finally
        {
            Directory.Delete(tempTheme, true);
        }
    }

    private static ContentItem CreateTestItem(string htmlContent, ContentGroup collection, string? layout = null, string sectionPath = "") => new()
    {
        SourcePath = "/test/content/test.md",
        RelativePath = "test.md",
        Title = "Test Post",
        Date = new DateTime(2026, 6, 17),
        Slug = "test-post",
        SectionPath = sectionPath,
        Layout = layout,
        RawContent = "# Test",
        HtmlContent = htmlContent,
        Url = new Uri("/blog/test-post/", UriKind.Relative),
        OutputPath = "blog/test-post/index.html",
        Collection = collection
    };

    private static SiteConfiguration CreateTestSite(ContentGroup collection, string basePath = "/", string assetPrefix = "/assets/") => new()
    {
        Title = "Test Site",
        BaseUrl = new UriBuilder(Uri.UriSchemeHttp, "localhost", 5555, basePath).Uri,
        AssetPrefix = assetPrefix,
        Collections = new Dictionary<string, ContentGroup> { ["posts"] = collection }
    };
}
