namespace Engobe.Core.Tests.Services;

using Engobe.Models;
using Engobe.Services;

public class NavigationTreeBuilderTests
{
    private const string ParentId = "a869d67a-e4e1-48c5-a08c-75241df633d1";

    [Test]
    [Arguments("guides/Overview.md")]
    [Arguments(ParentId)]
    [Arguments("A869D67A-E4E1-48C5-A08C-75241DF633D1")]
    public async Task Build_ParentReference_ReplacesStructureAndSortsChildren(string reference)
    {
        var parent = MakeItem("Overview", "/unrelated/", 0, sectionPath: "guides",
            navigation: ("guides/Overview.md", ParentId, null));
        var items = new[]
        {
            MakeItem("Z", "/old/z/", 1, sectionPath: "old", navigation: ("old/z.md", null, reference)),
            MakeItem("B", "/unused/deep/b/", 0, sectionPath: "unused/deep", navigation: ("unused/deep/b.md", null, reference)),
            MakeItem("A", "/old/a/", 0, sectionPath: "old", navigation: ("old/a.md", null, reference)),
            MakeItem("Remaining", "/old/remaining/", 0, sectionPath: "old"),
            parent
        };
        var roots = NavigationTreeBuilder.Build(items)["posts"];
        await Assert.That(roots.Select(node => node.Title)).IsEquivalentTo(["Guides", "Old"]);
        var overview = roots.Single(node => node.Title == "Guides").Children.Single();
        await Assert.That(overview.Children.Select(node => node.Title)).IsEquivalentTo(["A", "B", "Z"]);
        await Assert.That(string.Join(",", overview.Children.Select(node => node.Title))).IsEqualTo("A,B,Z");
        await Assert.That(roots.Single(node => node.Title == "Old").Children.Single().Title).IsEqualTo("Remaining");
    }

    [Test]
    [Arguments("missing.md")]
    [Arguments("overview.md")]
    [Arguments("Overview.MD")]
    [Arguments("/Overview.md")]
    [Arguments("Overview")]
    [Arguments("")]
    [Arguments("not-a-uuid")]
    [Arguments("00000000-0000-0000-0000-000000000001")]
    public async Task Build_InvalidParent_ReportsSourceAndValue(string reference)
    {
        var items = new[]
        {
            MakeItem("Overview", "/Overview/", 0, navigation: ("Overview.md", ParentId, null)),
            MakeItem("Child", "/child/", 0, navigation: ("child.md", null, reference))
        };
        await Assert.That(() => NavigationTreeBuilder.Build(items))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessageContaining("/child.md")
            .And.WithMessageContaining($"'{reference}'");
    }

    [Test]
    [Arguments("Overview.md")]
    [Arguments(ParentId)]
    public async Task Build_CrossCollectionOrUnpublishedParent_IsNotResolved(string reference)
    {
        var child = MakeItem("Child", "/child/", 0, navigation: ("child.md", null, reference));
        var other = MakeItem("Overview", "/overview/", 0, collection: MakeCollection("other"),
            navigation: ("Overview.md", ParentId, null));
        foreach (var items in new[] { new[] { child }, new[] { child, other } })
        {
            await Assert.That(() => NavigationTreeBuilder.Build(items))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("/child.md")
                .And.WithMessageContaining(reference);
        }
    }

    [Test]
    public async Task Build_DuplicateParentId_IsAmbiguous()
    {
        var items = new[]
        {
            MakeItem("One", "/one/", 0, navigation: ("one.md", ParentId, null)),
            MakeItem("Two", "/two/", 0, navigation: ("two.md", ParentId.ToUpperInvariant(), null)),
            MakeItem("Child", "/child/", 0, navigation: ("child.md", null, ParentId))
        };
        await Assert.That(() => NavigationTreeBuilder.Build(items))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessageContaining("/child.md")
            .And.WithMessageContaining(ParentId);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Build_ParentCycle_ReportsSourceAndValue(bool self)
    {
        var first = MakeItem("One", "/one/", 0, navigation: ("one.md", null, self ? "one.md" : ParentId));
        var second = MakeItem("Two", "/two/", 0, navigation: ("two.md", ParentId, "one.md"));
        await Assert.That(() => NavigationTreeBuilder.Build([first, second]))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessageContaining("/one.md")
            .And.WithMessageContaining(first.Parent!)
            .And.WithMessageContaining("cycle");
    }

    [Test]
    public async Task Build_FlatItems_FlatTree()
    {
        var items = new List<ContentItem>
        {
            MakeItem("Hello", "/hello/", 0),
            MakeItem("World", "/world/", 1)
        };

        var result = NavigationTreeBuilder.Build(items);

        await Assert.That(result).ContainsKey("posts");
        var roots = result["posts"];
        const int expectedCount = 2;
        await Assert.That(roots).Count().IsEqualTo(expectedCount);
        await Assert.That(roots[0].Title).IsEqualTo("Hello");
        await Assert.That(roots[0].Url.OriginalString).IsEqualTo("/hello/");
        await Assert.That(roots[1].Title).IsEqualTo("World");
    }

    [Test]
    public async Task Build_NestedItems_CreatesSectionNodes()
    {
        var col = MakeCollection();
        var items = new List<ContentItem>
        {
            MakeItem("Install", "/guides/install/", 0, sectionPath: "guides", collection: col),
            MakeItem("Config", "/guides/advanced/config/", 1, sectionPath: "guides/advanced", collection: col)
        };

        var result = NavigationTreeBuilder.Build(items);

        await Assert.That(result).ContainsKey("posts");
        var roots = result["posts"];
        await Assert.That(roots).Count().IsEqualTo(1);
        await Assert.That(roots[0].Title).IsEqualTo("Guides");
        const int expectedChildCount = 2;
        await Assert.That(roots[0].Children).Count().IsEqualTo(expectedChildCount);

        var configSection = roots[0].Children[0];
        var install = roots[0].Children[1];
        await Assert.That(configSection.Title).IsEqualTo("Advanced");
        const int configChildCount = 1;
        await Assert.That(configSection.Children).Count().IsEqualTo(configChildCount);
        await Assert.That(configSection.Children[0].Title).IsEqualTo("Config");

        await Assert.That(install.Title).IsEqualTo("Install");
        await Assert.That(install.Children).IsEmpty();
    }

    [Test]
    public async Task Build_SiblingsSortedByWeightThenTitle()
    {
        var items = new List<ContentItem>
        {
            MakeItem("Z Item", "/z/", 1),
            MakeItem("A Item", "/a/", 0),
            MakeItem("B Item", "/b/", 0)
        };

        var result = NavigationTreeBuilder.Build(items);
        var roots = result["posts"];

        const int lastIdx = 2;
        await Assert.That(roots[0].Title).IsEqualTo("A Item");
        await Assert.That(roots[1].Title).IsEqualTo("B Item");
        await Assert.That(roots[lastIdx].Title).IsEqualTo("Z Item");
    }

    [Test]
    public async Task Build_SectionTitles_AreHumanized()
    {
        var col = MakeCollection();
        var items = new List<ContentItem>
        {
            MakeItem("A", "/getting-started/a/", 0, sectionPath: "getting-started", collection: col),
            MakeItem("B", "/hello_world/b/", 1, sectionPath: "hello_world", collection: col)
        };

        var result = NavigationTreeBuilder.Build(items);
        var titles = result["posts"].Select(n => n.Title).ToList();

        await Assert.That(titles).Contains("Getting Started");
        await Assert.That(titles).Contains("Hello World");
    }

    [Test]
    public async Task Build_MultipleCollections_SeparateTrees()
    {
        var postsCol = new ContentGroup { Name = "posts" };
        var pagesCol = new ContentGroup { Name = "pages" };
        var items = new List<ContentItem>
        {
            MakeItem("Post1", "/post1/", 0, collection: postsCol),
            MakeItem("Page1", "/page1/", 0, collection: pagesCol)
        };

        var result = NavigationTreeBuilder.Build(items);

        await Assert.That(result).ContainsKey("posts");
        await Assert.That(result).ContainsKey("pages");
        await Assert.That(result["posts"]).Count().IsEqualTo(1);
        await Assert.That(result["pages"]).Count().IsEqualTo(1);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Build_RootItemWithUrlOverride_SectionNodesDeriveCorrectUrlsRegardlessOfOrder(bool rootItemFirst)
    {
        var apiCol = new ContentGroup
        {
            Name = "api",
            Permalink = "/api/:slug/"
        };

        var rootItem = MakeItem(
            "API Overview",
            "/api/",
            weight: 0,
            collection: apiCol,
            permalinkOverride: "/api/");

        var servicesItem = MakeItem(
            "SiteBuilder",
            "/api/Engobe/Services/SiteBuilder/",
            weight: 10,
            sectionPath: "Engobe/Services",
            collection: apiCol);

        var modelsItem = MakeItem(
            "BuildResult",
            "/api/Engobe/Models/BuildResult/",
            weight: 20,
            sectionPath: "Engobe/Models",
            collection: apiCol);

        var items = rootItemFirst
            ? new List<ContentItem> { rootItem, servicesItem, modelsItem }
            : new List<ContentItem> { servicesItem, modelsItem, rootItem };

        var result = NavigationTreeBuilder.Build(items);

        await Assert.That(result).ContainsKey("api");
        var roots = result["api"];
        // In the root list, we have the rootItem and the Engobe section node
        var engobeNode = roots.FirstOrDefault(n => n.Title == "Engobe");
        await Assert.That(engobeNode).IsNotNull();
        await Assert.That(engobeNode!.Url.OriginalString).IsEqualTo("/api/Engobe/");

        var modelsNode = engobeNode.Children.FirstOrDefault(c => c.Title == "Models");
        await Assert.That(modelsNode).IsNotNull();
        await Assert.That(modelsNode!.Url.OriginalString).IsEqualTo("/api/Engobe/Models/");

        var servicesNode = engobeNode.Children.FirstOrDefault(c => c.Title == "Services");
        await Assert.That(servicesNode).IsNotNull();
        await Assert.That(servicesNode!.Url.OriginalString).IsEqualTo("/api/Engobe/Services/");
    }

    [Test]
    public async Task Build_RootItemWithUrlOverride_WithBasePath_SectionNodesIncludeBasePath()
    {
        var apiCol = new ContentGroup
        {
            Name = "api",
            Permalink = "/api/:slug/"
        };

        var rootItem = MakeItem(
            "API Overview",
            "/site/api/",
            weight: 0,
            collection: apiCol,
            permalinkOverride: "/api/");

        var servicesItem = MakeItem(
            "SiteBuilder",
            "/site/api/Engobe/Services/SiteBuilder/",
            weight: 10,
            sectionPath: "Engobe/Services",
            collection: apiCol);

        var items = new List<ContentItem> { rootItem, servicesItem };

        var result = NavigationTreeBuilder.Build(items, basePath: "/site");

        await Assert.That(result).ContainsKey("api");
        var roots = result["api"];
        var engobeNode = roots.FirstOrDefault(n => n.Title == "Engobe");
        await Assert.That(engobeNode).IsNotNull();
        await Assert.That(engobeNode!.Url.OriginalString).IsEqualTo("/site/api/Engobe/");

        var servicesNode = engobeNode.Children.FirstOrDefault(c => c.Title == "Services");
        await Assert.That(servicesNode).IsNotNull();
        await Assert.That(servicesNode!.Url.OriginalString).IsEqualTo("/site/api/Engobe/Services/");
    }

    private static ContentItem MakeItem(
        string title,
        string url,
        int weight,
        string sectionPath = "",
        ContentGroup? collection = null,
        string? permalinkOverride = null,
        (string Path, string? Id, string? Parent)? navigation = null)
    {
        var col = collection ?? MakeCollection();
        var extra = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(permalinkOverride))
        {
            extra["permalink_override"] = permalinkOverride;
        }

        return new ContentItem
        {
            Title = title,
            Id = navigation?.Id,
            Parent = navigation?.Parent,
            Slug = url.Trim('/').Split('/').Last(),
            SectionPath = sectionPath,
            Weight = weight,
            SourcePath = "/" + (navigation?.Path ?? "test.md"),
            RelativePath = navigation?.Path ?? "test.md",
            RawContent = "",
            HtmlContent = "",
            Url = new Uri(url, UriKind.Relative),
            OutputPath = url.Trim('/') + "/index.html",
            Collection = col,
            Extra = extra
        };
    }

    private static ContentGroup MakeCollection(string name = "posts") =>
        new() { Name = name };
}
