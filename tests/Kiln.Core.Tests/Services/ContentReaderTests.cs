namespace Kiln.Core.Tests.Services;

using Kiln.Models;
using Kiln.Services;

public class ContentReaderTests
{
    private readonly ContentReader _reader = new(new MarkdownProcessor());

    [Test]
    [Arguments(null)]
    [Arguments("guides/Overview.md")]
    [Arguments("a869d67a-e4e1-48c5-a08c-75241df633d1")]
    public async Task ReadCollection_PreservesOptionalParent(string? parent)
    {
        var parentLine = parent is null ? "" : $"parent: {parent}\n";
        var dir = CreateTempContent("child.md", $"---\ntitle: Child\n{parentLine}---\nBody");
        try
        {
            var item = _reader.ReadCollection(MakeCollection("posts", dir), dir).Single();
            await Assert.That(item.Parent).IsEqualTo(parent);
            await Assert.That(item.Extra.ContainsKey("parent")).IsFalse();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task ReadCollection_ReturnsEmptyForNonexistentDirectory()
    {
        var collection = MakeCollection("posts", "/nonexistent/path");
        var result = _reader.ReadCollection(collection, "/nonexistent");

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task ReadCollection_ParsesFrontMatterAndContent()
    {
        var tempDir = CreateTempContent(
            "test.md",
            """
            ---
            title: Test Post
            date: 2026-06-17
            tags:
              - dotnet
              - kiln
            ---

            Hello **world**!
            """);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            var item = result[0];
            await Assert.That(item.Title).IsEqualTo("Test Post");
            await Assert.That(item.Date).IsEqualTo(new DateTime(2026, 6, 17));
            await Assert.That(item.Taxonomies.ContainsKey("tags")).IsTrue();
            await Assert.That(item.HtmlContent).Contains("<strong>world</strong>");
            await Assert.That(item.Slug).IsEqualTo("test");
            await Assert.That(item.Collection.Name).IsEqualTo("posts");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_ReadsCustomTaxonomyGenerically()
    {
        var tempDir = CreateTempContent(
            "test.md",
            """
            ---
            title: Test Post
            tags:
              - dotnet
            series:
              - foo
              - bar
            ---

            content
            """);

        try
        {
            var collection = MakeCollection("posts", tempDir, taxonomies: ["tags", "series"]);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            var item = result[0];
            await Assert.That(item.Taxonomies.ContainsKey("series")).IsTrue();
            var series = (List<string>)item.Taxonomies["series"];
            await Assert.That(series).IsEquivalentTo(["foo", "bar"]);

            // Custom taxonomy values must not also leak into Extra.
            await Assert.That(item.Extra.ContainsKey("series")).IsFalse();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_SkipsFilesWithoutFrontMatter()
    {
        var tempDir = CreateTempContent("no-frontmatter.md", "Just plain markdown.");

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);
            await Assert.That(result).IsEmpty();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_MapsNoIndexFlag()
    {
        var tempDir = CreateTempContent(
            "hidden.md",
            """
            ---
            title: Hidden Post
            noIndex: true
            ---

            hidden content
            """);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            await Assert.That(result[0].NoIndex).IsTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_DefaultsNoIndexToFalseWhenUnset()
    {
        var tempDir = CreateTempContent(
            "visible.md",
            """
            ---
            title: Visible Post
            ---

            visible content
            """);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            await Assert.That(result[0].NoIndex).IsFalse();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_SortsDateDesc()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);        await File.WriteAllTextAsync(Path.Combine(tempDir, "old.md"),
            """
            ---
            title: Old
            date: 2024-01-01
            ---
            content
            """).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "new.md"),
            """
            ---
            title: New
            date: 2026-01-01
            ---
            content
            """).ConfigureAwait(false);

        try
        {
            var collection = MakeCollection("posts", tempDir, sort: "date desc");
            var result = _reader.ReadCollection(collection, tempDir);

            const int expectedCount = 2;
            await Assert.That(result.Count).IsEqualTo(expectedCount);
            await Assert.That(result[0].Title).IsEqualTo("New");
            await Assert.That(result[1].Title).IsEqualTo("Old");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_DetectsPageBundle()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        var bundleDir = Path.Combine(tempDir, "my-post");
        Directory.CreateDirectory(bundleDir);

        await File.WriteAllTextAsync(Path.Combine(bundleDir, "index.md"),
            """
            ---
            title: Bundle Post
            date: 2026-06-18
            ---

            Content with ![hero](hero.txt)
            """).ConfigureAwait(false);

        await File.WriteAllTextAsync(Path.Combine(bundleDir, "hero.txt"), "asset").ConfigureAwait(false);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            var item = result[0];
            await Assert.That(item.Title).IsEqualTo("Bundle Post");
            await Assert.That(item.Slug).IsEqualTo("my-post");
            await Assert.That(item.AssetDirectory).IsNotNull();
            await Assert.That(item.AssetDirectory).IsEqualTo(bundleDir);
            await Assert.That(item.HtmlContent).Contains("/assets/content/posts/my-post/hero.txt");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_SubdirWithoutIndexMd_RecursedAsSection()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        var subDir = Path.Combine(tempDir, "not-a-bundle");
        Directory.CreateDirectory(subDir);

        await File.WriteAllTextAsync(Path.Combine(subDir, "post.md"),
            """
            ---
            title: Not a bundle
            ---
            content
            """).ConfigureAwait(false);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            // Subdir without index.md is recursed as a section
            await Assert.That(result).HasSingleItem();
            await Assert.That(result[0].Title).IsEqualTo("Not a bundle");
            await Assert.That(result[0].SectionPath).IsEqualTo("not-a-bundle");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadSingleFile_ParsesStandaloneFile()
    {
        var tempDir = CreateTempContent(
            "index.md",
            """
            ---
            title: Home
            ---
            Welcome home.
            """);

        try
        {
            var collection = MakeCollection("home", tempDir);
            var item = _reader.ReadSingleFile(Path.Combine(tempDir, "index.md"), collection);

            await Assert.That(item.Title).IsEqualTo("Home");
            await Assert.That(item.Slug).IsEqualTo("index");
            await Assert.That(item.Collection.Name).IsEqualTo("home");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadSingleFile_MissingFile_ThrowsFileNotFound()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var collection = MakeCollection("home", tempDir);
            await Assert.That(() => _reader.ReadSingleFile(Path.Combine(tempDir, "missing.md"), collection))
                .ThrowsExactly<FileNotFoundException>();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_FlatItem_RegressionSectionPathEmpty()
    {
        var tempDir = CreateTempContent(
            "hello.md",
            """
            ---
            title: Hello
            ---
            content
            """);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            await Assert.That(result[0].Slug).IsEqualTo("hello");
            await Assert.That(result[0].SectionPath).IsEqualTo("");
            await Assert.That(result[0].RelativePath).IsEqualTo("hello.md");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_FlatBundle_RegressionSectionPathEmpty()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        var bundleDir = Path.Combine(tempDir, "my-post");
        Directory.CreateDirectory(bundleDir);

        await File.WriteAllTextAsync(Path.Combine(bundleDir, "index.md"),
            """
            ---
            title: Bundle Post
            ---
            content
            """).ConfigureAwait(false);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            await Assert.That(result[0].Slug).IsEqualTo("my-post");
            await Assert.That(result[0].SectionPath).IsEqualTo("");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_NestedFile_HasSectionPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        var guidesDir = Path.Combine(tempDir, "guides");
        Directory.CreateDirectory(guidesDir);

        await File.WriteAllTextAsync(Path.Combine(guidesDir, "install.md"),
            """
            ---
            title: Install Guide
            ---
            content
            """).ConfigureAwait(false);

        try
        {
            var collection = MakeCollection("docs", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            await Assert.That(result[0].Slug).IsEqualTo("install");
            await Assert.That(result[0].SectionPath).IsEqualTo("guides");
            await Assert.That(result[0].RelativePath).EndsWith("guides/install.md");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_NestedBundle_HasSectionPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        var bundleDir = Path.Combine(tempDir, "guides", "install");
        Directory.CreateDirectory(bundleDir);

        await File.WriteAllTextAsync(Path.Combine(bundleDir, "index.md"),
            """
            ---
            title: Install Guide
            ---
            content
            """).ConfigureAwait(false);

        try
        {
            var collection = MakeCollection("docs", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            await Assert.That(result[0].Slug).IsEqualTo("install");
            await Assert.That(result[0].SectionPath).IsEqualTo("guides");
            await Assert.That(result[0].RelativePath).EndsWith("guides/install/index.md");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_DeepSection_HasFullSectionPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        var deepDir = Path.Combine(tempDir, "guides", "advanced");
        Directory.CreateDirectory(deepDir);

        await File.WriteAllTextAsync(Path.Combine(deepDir, "config.md"),
            """
            ---
            title: Config Guide
            ---
            content
            """).ConfigureAwait(false);

        try
        {
            var collection = MakeCollection("docs", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            await Assert.That(result[0].Slug).IsEqualTo("config");
            await Assert.That(result[0].SectionPath).IsEqualTo("guides/advanced");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_LeafBundle_NotRecursed()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        var bundleDir = Path.Combine(tempDir, "post");
        Directory.CreateDirectory(bundleDir);

        await File.WriteAllTextAsync(Path.Combine(bundleDir, "index.md"),
            """
            ---
            title: Post
            ---
            content
            """).ConfigureAwait(false);

        await File.WriteAllTextAsync(Path.Combine(bundleDir, "extra.md"),
            """
            ---
            title: Extra
            ---
            not an item
            """).ConfigureAwait(false);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            await Assert.That(result[0].Slug).IsEqualTo("post");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_LeafBundle_NestedMarkdownDoesNotChangeBundleOrSections()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        var bundleDir = Path.Combine(tempDir, "outer");
        var nestedBundleDir = Path.Combine(bundleDir, "child");
        var notesDir = Path.Combine(bundleDir, "notes");
        var manualDir = Path.Combine(bundleDir, "images", "manual");
        var sectionDir = Path.Combine(tempDir, "guides", "advanced", "install");

        await WriteMarkdownAsync(Path.Combine(bundleDir, "index.md"), "title: Outer").ConfigureAwait(false);
        await WriteMarkdownAsync(Path.Combine(nestedBundleDir, "index.md"), "title: Nested").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(nestedBundleDir, "photo.png"), "image").ConfigureAwait(false);
        await WriteMarkdownAsync(Path.Combine(notesDir, "article.md"), "title: Article").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(notesDir, "data.zip"), "archive").ConfigureAwait(false);
        await WriteMarkdownAsync(Path.Combine(manualDir, "README.MD"), "title: Readme").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(manualDir, "diagram.svg"), "image").ConfigureAwait(false);
        await WriteMarkdownAsync(Path.Combine(sectionDir, "index.md"), "title: Install\nslug: install-guide").ConfigureAwait(false);

        try
        {
            var result = _reader.ReadCollection(MakeCollection("posts", tempDir), tempDir);

            await Assert.That(result.Count).IsEqualTo(2);
            var outer = result.Single(static item => item.Slug == "outer");
            await Assert.That(outer.SectionPath).IsEqualTo("");
            var install = result.Single(static item => item.Slug == "install-guide");
            await Assert.That(install.SectionPath).IsEqualTo(Path.Combine("guides", "advanced").Replace(Path.DirectorySeparatorChar, '/'));
            await Assert.That(result.Any(static item => item.Slug == "child" || item.Slug == "article" || item.Slug == "README")).IsFalse();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_PageBundleReferences_ResolveOnlyAllowedLocalAssets()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        var bundleDir = Path.Combine(tempDir, "post");
        var hiddenDir = Path.Combine(bundleDir, ".hidden");
        var linkedTargetDir = Path.Combine(tempDir, "linked-target");
        Directory.CreateDirectory(hiddenDir);
        Directory.CreateDirectory(linkedTargetDir);

        await File.WriteAllTextAsync(Path.Combine(bundleDir, "index.md"),
            """
            ---
            title: Bundle
            ---
            ![Normalized](./images/screens/../screens/x.jpg?v=2#detail)
            ![Nested](child/photo.png)
            ![Missing](images/missing.jpg)
            ![Markdown](child/index.md)
            ![Hidden](.hidden/photo.png)
            ![Linked file](linked-file.png)
            ![Linked directory](linked-directory/inside.png)
            ![Escape](../outside.png)

            [Download](./downloads/setup.zip?download=1#file)
            [README](images/manual/README.MD)
            [Diagram](images/manual/diagram.svg)
            [Child asset](child/photo.png)
            [Anchor](#details)
            [External](https://example.com/file.zip)
            [Root relative](/assets/shared/file.zip)
            """).ConfigureAwait(false);
        await WriteBundleFileAsync(bundleDir, "images/screens/x.jpg").ConfigureAwait(false);
        await WriteBundleFileAsync(bundleDir, "downloads/setup.zip").ConfigureAwait(false);
        await WriteBundleFileAsync(bundleDir, "child/index.md", "not a page").ConfigureAwait(false);
        await WriteBundleFileAsync(bundleDir, "child/photo.png").ConfigureAwait(false);
        await WriteBundleFileAsync(bundleDir, "images/manual/README.MD", "not a page").ConfigureAwait(false);
        await WriteBundleFileAsync(bundleDir, "images/manual/diagram.svg").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(hiddenDir, "photo.png"), "hidden").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "outside.png"), "outside").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(linkedTargetDir, "inside.png"), "linked").ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(tempDir, "linked-file.png"), "linked file").ConfigureAwait(false);
        File.CreateSymbolicLink(Path.Combine(bundleDir, "linked-file.png"), Path.Combine(tempDir, "linked-file.png"));
        Directory.CreateSymbolicLink(Path.Combine(bundleDir, "linked-directory"), linkedTargetDir);

        try
        {
            var warnings = new System.Collections.ObjectModel.Collection<string>();
            var item = _reader.ReadCollection(MakeCollection("posts", tempDir), tempDir, warnings: warnings).Single();
            var html = item.HtmlContent;
            const string assetBase = "/assets/content/posts/post/";

            await Assert.That(html).Contains($"src=\"{assetBase}images/screens/x.jpg?v=2#detail\"");
            await Assert.That(html).Contains($"src=\"{assetBase}child/photo.png\"");
            await Assert.That(html).Contains($"src=\"{assetBase}images/missing.jpg\"");
            await Assert.That(html).Contains($"href=\"{assetBase}downloads/setup.zip?download=1#file\"");
            await Assert.That(html).Contains($"href=\"{assetBase}child/photo.png\"");
            await Assert.That(html).Contains("src=\"child/index.md\"");
            await Assert.That(html).Contains("href=\"images/manual/README.MD\"");
            await Assert.That(html).Contains($"href=\"{assetBase}images/manual/diagram.svg\"");
            await Assert.That(html).Contains("src=\".hidden/photo.png\"");
            await Assert.That(html).Contains("src=\"linked-file.png\"");
            await Assert.That(html).Contains("src=\"linked-directory/inside.png\"");
            await Assert.That(html).Contains("src=\"../outside.png\"");
            await Assert.That(html).Contains("href=\"#details\"");
            await Assert.That(html).Contains("href=\"https://example.com/file.zip\"");
            await Assert.That(html).Contains("href=\"/assets/shared/file.zip\"");
            await Assert.That(warnings.Any(static warning => warning.Contains("child/index.md", StringComparison.Ordinal))).IsTrue();
            await Assert.That(warnings.Any(static warning => warning.Contains("images/manual/README.MD", StringComparison.Ordinal))).IsTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_SectionWithoutIndexMd_IsRecursed()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        var guidesDir = Path.Combine(tempDir, "guides");
        Directory.CreateDirectory(guidesDir);

        await File.WriteAllTextAsync(Path.Combine(guidesDir, "getting-started.md"),
            """
            ---
            title: Getting Started
            ---
            content
            """).ConfigureAwait(false);

        await File.WriteAllTextAsync(Path.Combine(guidesDir, "faq.md"),
            """
            ---
            title: FAQ
            ---
            content
            """).ConfigureAwait(false);

        try
        {
            var collection = MakeCollection("docs", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            const int expectedCount = 2;
            await Assert.That(result).Count().IsEqualTo(expectedCount);
            await Assert.That(result.All(i => i.SectionPath == "guides")).IsTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_Teaser_UsesDescriptionWhenSet()
    {
        var tempDir = CreateTempContent(
            "test.md",
            """
            ---
            title: Test Post
            description: A hand-written teaser.
            ---

            Body content that would otherwise become the teaser.
            """);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            await Assert.That(result[0].Teaser).IsEqualTo("A hand-written teaser.");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_Teaser_UsesContentBeforeMoreMarkerWhenNoDescription()
    {
        var tempDir = CreateTempContent(
            "test.md",
            """
            ---
            title: Test Post
            ---

            Intro **paragraph** before the marker.
            <!--more-->
            Rest of the post that should not be part of the teaser.
            """);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            await Assert.That(result[0].Teaser).IsEqualTo("Intro paragraph before the marker.");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_Teaser_MoreMarkerNeverLeaksIntoHtmlContent()
    {
        var tempDir = CreateTempContent(
            "test.md",
            """
            ---
            title: Test Post
            ---

            Intro before the marker.
            <!--more-->
            Rest of the post.
            """);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            await Assert.That(result[0].HtmlContent).DoesNotContain("<!--more-->");
            await Assert.That(result[0].HtmlContent).Contains("Rest of the post.");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_Teaser_AutoTruncatesToWordLimitWhenNoDescriptionOrMarker()
    {
        var words = Enumerable.Range(1, 60).Select(i => $"word{i}").ToArray();
        var body = string.Join(' ', words);
        var tempDir = CreateTempContent(
            "test.md",
            $"""
            ---
            title: Test Post
            ---

            {body}
            """);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            var expected = string.Join(' ', words.Take(55)) + "…";
            await Assert.That(result[0].Teaser).IsEqualTo(expected);
            await Assert.That(result[0].Teaser).EndsWith("…");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_Teaser_ShortAutoBodyIsNotTruncatedAndHasNoEllipsis()
    {
        var tempDir = CreateTempContent(
            "test.md",
            """
            ---
            title: Test Post
            ---

            Just a short body with **few** words.
            """);

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            await Assert.That(result).HasSingleItem();
            await Assert.That(result[0].Teaser).IsEqualTo("Just a short body with few words.");
            await Assert.That(result[0].Teaser).DoesNotEndWith("…");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task ReadCollection_WithoutSort_PreservesDirectoryFileOrder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var fileNames = new[] { "post-05.md", "post-01.md", "post-13.md", "post-02.md" };
        foreach (var fileName in fileNames)
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, fileName),
                $"""
                ---
                title: {Path.GetFileNameWithoutExtension(fileName)}
                ---
                content
                """).ConfigureAwait(false);
        }

        try
        {
            var collection = MakeCollection("posts", tempDir);
            var result = _reader.ReadCollection(collection, tempDir);

            var expectedOrder = Directory.GetFiles(tempDir, "*.md", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .ToList();

            await Assert.That(result.Count).IsEqualTo(expectedOrder.Count);
            for (var i = 0; i < result.Count; i++)
            {
                await Assert.That(Path.GetFileName(result[i].SourcePath)).IsEqualTo(expectedOrder[i]);
            }
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static string CreateTempContent(string fileName, string content)

    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), content);
        return dir;
    }

    private static async Task WriteMarkdownAsync(string path, string frontMatter)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, $"---\n{frontMatter}\n---\ncontent").ConfigureAwait(false);
    }

    private static async Task WriteBundleFileAsync(string bundleDirectory, string relativePath, string content = "asset")
    {
        var path = Path.Combine(bundleDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content).ConfigureAwait(false);
    }

    private static ContentGroup MakeCollection(string name, string directory, string sort = "none", string[]? taxonomies = null, int teaserWords = 55) =>
        new() { Name = name, Directory = directory, Sort = sort, Taxonomies = [.. taxonomies ?? ["tags", "categories"]], TeaserWords = teaserWords };
}
