namespace Kiln.Core.Tests.Services;

using Kiln.Services;

public class NavTreeIntegrationTests
{
    [Test]
    [Arguments("guides/getting-started.md", "")]
    [Arguments("guides/getting-started.md", "/site")]
    [Arguments("a869d67a-e4e1-48c5-a08c-75241df633d1", "")]
    [Arguments("a869d67a-e4e1-48c5-a08c-75241df633d1", "/site")]
    public async Task Build_ParentOverrides_ProjectEffectiveTreeAndRetainUrlPrefixAncestors(string reference, string basePath)
    {
        var dir = CreateSiteWithNavLayout();
        try
        {
            var configPath = Path.Combine(dir, "site.yaml");
            var yaml = await File.ReadAllTextAsync(configPath);
            await File.WriteAllTextAsync(configPath, yaml.Replace("http://localhost:5555", $"http://localhost:5555{basePath}", StringComparison.Ordinal));
            await File.WriteAllTextAsync(Path.Combine(dir, "content", "guides", "getting-started.md"),
                "---\ntitle: Getting Started\nid: a869d67a-e4e1-48c5-a08c-75241df633d1\nparent: top.md\n---\nParent");
            await File.WriteAllTextAsync(Path.Combine(dir, "content", "guides", "advanced", "config.md"),
                $"---\ntitle: Config\nparent: {reference}\nurl: /detached/config/\n---\nChild");
            await File.WriteAllTextAsync(Path.Combine(dir, "content", "guides", "remaining.md"), "---\ntitle: Remaining\n---\nSibling");
            Directory.CreateDirectory(Path.Combine(dir, "content", "detached"));
            await File.WriteAllTextAsync(Path.Combine(dir, "content", "detached", "other.md"), "---\ntitle: Other\n---\nPrefix sibling");
            await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
                """
                ROOT:{{ navtree.docs.size }};{{ if site.navtree }}WRONG_BINDING{{ end }}
                {{ for n in navtree.docs }}
                NODE:{{ n.title }}|{{ n.url }}|{{ n.is_active }}|{{ n.is_ancestor }};
                {{ for c in n.children }}
                EDGE:{{ n.title }}>{{ c.title }};
                NODE:{{ c.title }}|{{ c.url }}|{{ c.is_active }}|{{ c.is_ancestor }};
                {{ for g in c.children }}
                EDGE:{{ c.title }}>{{ g.title }};
                NODE:{{ g.title }}|{{ g.url }}|{{ g.is_active }}|{{ g.is_ancestor }};
                {{ end }}{{ end }}{{ end }}
                ANCESTORS:{{ for a in page.ancestors }}{{ a.title }}|{{ a.url }};{{ end }}
                {{ if page.section }}SECTION:{{ page.section.title }}|{{ page.section.path }}|{{ page.section.url }};{{ end }}
                """);

            var result = await CreateBuilder().BuildAsync(dir);
            await Assert.That(result.Errors).IsEmpty();
            await Assert.That(result.Success).IsTrue();
            var html = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "detached", "config", "index.html"));
            await Assert.That(html).Contains("ROOT:3;");
            await Assert.That(html).DoesNotContain("WRONG_BINDING");
            await Assert.That(html).Contains($"NODE:Top|{basePath}/top/|false|true;");
            await Assert.That(html).Contains($"NODE:Getting Started|{basePath}/guides/getting-started/|false|true;");
            await Assert.That(html).Contains($"NODE:Config|{basePath}/detached/config/|true|false;");
            await Assert.That(html).Contains($"NODE:Guides|{basePath}/guides/|false|false;");
            await Assert.That(html).Contains($"NODE:Detached|{basePath}/detached/|false|true;");
            await Assert.That(html).Contains("EDGE:Top>Getting Started;");
            await Assert.That(html).Contains("EDGE:Getting Started>Config;");
            await Assert.That(html).Contains("EDGE:Guides>Remaining;");
            await Assert.That(html).DoesNotContain("NODE:Advanced|");
            await Assert.That(html).Contains($"ANCESTORS:Docs|{basePath}/;Top|{basePath}/top/;Getting Started|{basePath}/guides/getting-started/;");
            await Assert.That(html).Contains("SECTION:Advanced|guides/advanced|");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments("missing.md", false)]
    [Arguments("top.md", true)]
    [Arguments("a869d67a-e4e1-48c5-a08c-75241df633d1", true)]
    public async Task Build_InvalidOrUnpublishedParent_FailsBeforeRendering(string reference, bool draftTarget)
    {
        var dir = CreateSiteWithNavLayout();
        try
        {
            if (draftTarget)
                await File.WriteAllTextAsync(Path.Combine(dir, "content", "top.md"),
                    "---\ntitle: Top\ndraft: true\nid: a869d67a-e4e1-48c5-a08c-75241df633d1\n---\nDraft");
            var source = Path.Combine(dir, "content", "guides", "advanced", "config.md");
            await File.WriteAllTextAsync(source, $"---\ntitle: Config\nparent: {reference}\n---\nChild");
            var result = await CreateBuilder().BuildAsync(dir);
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.RenderedFiles).IsEqualTo(0);
            await Assert.That(result.Errors).HasSingleItem();
            await Assert.That(result.Errors[0]).Contains(source);
            await Assert.That(result.Errors[0]).Contains(reference);
            await Assert.That(Directory.Exists(Path.Combine(dir, "_site"))).IsFalse();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Build_NavTreeMarkers_CorrectOnNestedPage()
    {
        var dir = CreateSiteWithNavLayout();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();

            var configHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "guides", "advanced", "config", "index.html"));

            // config page: itself should be active in the tree
            await Assert.That(configHtml).Contains("NODE_TITLE:Config");
            await Assert.That(configHtml).Contains("NODE_ACTIVE:true");
            await Assert.That(configHtml).Contains("NODE_URL:/guides/advanced/config/");

            // advanced section should be ancestor
            await Assert.That(configHtml).Contains("NODE_TITLE:Advanced");
            await Assert.That(configHtml).Contains("NODE_ANCESTOR:true");

            // guides section should be ancestor
            await Assert.That(configHtml).Contains("NODE_TITLE:Guides");
            await Assert.That(configHtml).Contains("NODE_ANCESTOR:true");

            // top page should NOT be active or ancestor
            await Assert.That(configHtml).Contains("NODE_TITLE:Top");
            await Assert.That(configHtml).Contains("NODE_ACTIVE:false");
            await Assert.That(configHtml).Contains("NODE_ANCESTOR:false");

            // getting started should NOT be active or ancestor
            await Assert.That(configHtml).Contains("NODE_TITLE:Getting Started");
            await Assert.That(configHtml).Contains("NODE_ACTIVE:false");
            await Assert.That(configHtml).Contains("NODE_ANCESTOR:false");

            // Breadcrumbs
            await Assert.That(configHtml).Contains("BRE_ANCESTORS:Docs|/ ");
            await Assert.That(configHtml).Contains("Guides|/guides/ ");
            await Assert.That(configHtml).Contains("Advanced|/guides/advanced/ ");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Build_NavTreeMarkers_CorrectOnTopPage()
    {
        var dir = CreateSiteWithNavLayout();

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();

            var topHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "top", "index.html"));

            // top page: itself should be active
            await Assert.That(topHtml).Contains("NODE_TITLE:Top");
            await Assert.That(topHtml).Contains("NODE_ACTIVE:true");

            // guides section should NOT be ancestor (top page is at /top/, not under /guides/)
            await Assert.That(topHtml).Contains("NODE_TITLE:Guides");
            await Assert.That(topHtml).Contains("NODE_ANCESTOR:false");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Build_ApiCollectionWithRootOverride_NavTreeMarkersAreAncestorForSectionPages(bool rootItemFirst)
    {
        var dir = CreateSiteWithApiCollection(rootItemFirst);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir);

            await Assert.That(result.Success).IsTrue();

            var serviceHtml = await File.ReadAllTextAsync(
                Path.Combine(dir, "_site", "api", "Kiln", "Services", "SiteBuilder", "index.html"));

            // On the SiteBuilder page:
            // SiteBuilder should be active
            await Assert.That(serviceHtml).Contains("NODE_TITLE:SiteBuilder");
            await Assert.That(serviceHtml).Contains("NODE_ACTIVE:true");
            await Assert.That(serviceHtml).Contains("NODE_URL:/api/Kiln/Services/SiteBuilder/");

            // Kiln and Services section nodes must have proper URLs and is_ancestor == true
            await Assert.That(serviceHtml).Contains("NODE_TITLE:Kiln");
            await Assert.That(serviceHtml).Contains("NODE_URL:/api/Kiln/");
            await Assert.That(serviceHtml).Contains("NODE_ANCESTOR:true");

            await Assert.That(serviceHtml).Contains("NODE_TITLE:Services");
            await Assert.That(serviceHtml).Contains("NODE_URL:/api/Kiln/Services/");
            await Assert.That(serviceHtml).Contains("NODE_ANCESTOR:true");

            // Models section node should NOT be an ancestor
            await Assert.That(serviceHtml).Contains("NODE_TITLE:Models");
            await Assert.That(serviceHtml).Contains("NODE_URL:/api/Kiln/Models/");
            await Assert.That(serviceHtml).Contains("NODE_ANCESTOR:false");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string CreateSiteWithNavLayout()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-nav-{Guid.NewGuid():N}");
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
            title: Top
            ---
            top content
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

        // Layout that renders navtree + breadcrumbs using recursion
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            """
            <html>
            {{ for node in navtree.docs }}
            NODE_TITLE:{{ node.title }}
            NODE_URL:{{ node.url }}
            NODE_ACTIVE:{{ node.is_active }}
            NODE_ANCESTOR:{{ node.is_ancestor }}
            {{ for child in node.children }}
            NODE_TITLE:{{ child.title }}
            NODE_URL:{{ child.url }}
            NODE_ACTIVE:{{ child.is_active }}
            NODE_ANCESTOR:{{ child.is_ancestor }}
            {{ for grandchild in child.children }}
            NODE_TITLE:{{ grandchild.title }}
            NODE_URL:{{ grandchild.url }}
            NODE_ACTIVE:{{ grandchild.is_active }}
            NODE_ANCESTOR:{{ grandchild.is_ancestor }}
            {{ end }}
            {{ end }}
            {{ end }}
            BRE_ANCESTORS:{{ for a in page.ancestors }}{{ a.title }}|{{ a.url }} {{ end }}
            {{ page.content }}
            </html>
            """);
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html>Not Found</html>");

        return dir;
    }

    private static string CreateSiteWithApiCollection(bool rootItemFirst)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-nav-api-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "api", "Kiln", "Services"));
        Directory.CreateDirectory(Path.Combine(dir, "content", "api", "Kiln", "Models"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            """
            title: API Test Site
            baseUrl: http://localhost:5555
            collections:
              api:
                directory: content/api
                permalink: /api/:slug/
            """);

        var rootWeight = rootItemFirst ? 0 : 50;
        const int serviceWeight = 10;
        const int modelWeight = 20;

        File.WriteAllText(Path.Combine(dir, "content", "api", "index.md"),
            $$"""
            ---
            title: API Overview
            url: /api/
            weight: {{rootWeight}}
            ---
            API Root
            """);

        File.WriteAllText(Path.Combine(dir, "content", "api", "Kiln", "Services", "SiteBuilder.md"),
            $$"""
            ---
            title: SiteBuilder
            weight: {{serviceWeight}}
            ---
            SiteBuilder doc
            """);

        File.WriteAllText(Path.Combine(dir, "content", "api", "Kiln", "Models", "BuildResult.md"),
            $$"""
            ---
            title: BuildResult
            weight: {{modelWeight}}
            ---
            BuildResult doc
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            """
            <html>
            {{ for node in navtree.api }}
            NODE_TITLE:{{ node.title }}
            NODE_URL:{{ node.url }}
            NODE_ACTIVE:{{ node.is_active }}
            NODE_ANCESTOR:{{ node.is_ancestor }}
            {{ for child in node.children }}
            NODE_TITLE:{{ child.title }}
            NODE_URL:{{ child.url }}
            NODE_ACTIVE:{{ child.is_active }}
            NODE_ANCESTOR:{{ child.is_ancestor }}
            {{ for grandchild in child.children }}
            NODE_TITLE:{{ grandchild.title }}
            NODE_URL:{{ grandchild.url }}
            NODE_ACTIVE:{{ grandchild.is_active }}
            NODE_ANCESTOR:{{ grandchild.is_ancestor }}
            {{ end }}
            {{ end }}
            {{ end }}
            {{ page.content }}
            </html>
            """);

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
