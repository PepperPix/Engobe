namespace Engobe.Core.Tests.Configuration;

using Engobe.Models;
using Engobe.Services;

public class SiteConfigLoaderTests
{
    private readonly SiteConfigLoader _loader = new();

    [Test]
    public async Task Load_ValidYaml_ParsesAllFields()
    {
        var dir = await CreateTempSiteAsync("""
            title: My Site
            description: A test site
            baseUrl: https://example.com
            language: de
            theme: mytheme
            assetPrefix: /static/
            outputDir: dist
            themesDir: layouts

            collections:
              posts:
                directory: content/blog
                permalink: /blog/:slug/
                sort: date desc
                feed: true
                paginate: 5
                taxonomies:
                  - tags
                layout: post
              pages:
                directory: content/pages
                permalink: /:slug/
                sort: weight asc

            taxonomies:
              tags:
                permalink: /t/:slug/
                paginate: 15
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Title).IsEqualTo("My Site");
            await Assert.That(config.Description).IsEqualTo("A test site");
            await Assert.That(config.BaseUrl.ToString()).IsEqualTo("https://example.com/");
            await Assert.That(config.Language).IsEqualTo("de");
            await Assert.That(config.Theme).IsEqualTo("mytheme");
            await Assert.That(config.AssetPrefix).IsEqualTo("/static/");
            await Assert.That(config.OutputDir).IsEqualTo("dist");
            await Assert.That(config.ThemesDir).IsEqualTo("layouts");

            await Assert.That(config.Collections).Count().IsEqualTo(2);
            await Assert.That(config.Collections.ContainsKey("posts")).IsTrue();
            await Assert.That(config.Collections["posts"].Directory).IsEqualTo(Path.Combine("content", "blog"));
            await Assert.That(config.Collections["posts"].Permalink).IsEqualTo("/blog/:slug/");
            await Assert.That(config.Collections["posts"].Sort).IsEqualTo("date desc");
            await Assert.That(config.Collections["posts"].Feed).IsTrue();
            await Assert.That(config.Collections["posts"].Paginate).IsEqualTo(5);
            await Assert.That(config.Collections["posts"].Layout).IsEqualTo("post");

            await Assert.That(config.Taxonomies).Count().IsEqualTo(1);
            await Assert.That(config.Taxonomies["tags"].Permalink).IsEqualTo("/t/:slug/");
            await Assert.That(config.Taxonomies["tags"].Paginate).IsEqualTo(15);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_MissingTitle_ThrowsWithClearMessage()
    {
        var dir = await CreateTempSiteAsync("""
            baseUrl: https://example.com
            """);

        try
        {
            await Assert.That(() => _loader.Load(dir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("missing required field: title");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_MissingBaseUrl_ThrowsWithClearMessage()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            """);

        try
        {
            await Assert.That(() => _loader.Load(dir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("missing required field: baseUrl");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_NoSiteYaml_ThrowsFileNotFound()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"engobe-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        try
        {
            await Assert.That(() => _loader.Load(dir))
                .ThrowsExactly<FileNotFoundException>();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_MinimalYaml_UsesDefaults()
    {
        var dir = await CreateTempSiteAsync("""
            title: Minimal
            baseUrl: http://localhost:5555
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Language).IsEqualTo("en");
            await Assert.That(config.Theme).IsEqualTo("default");
            await Assert.That(config.AssetPrefix).IsEqualTo("/assets/");
            await Assert.That(config.OutputDir).IsEqualTo("_site");
            await Assert.That(config.ThemesDir).IsEqualTo("themes");
            await Assert.That(config.Collections).IsEmpty();
            await Assert.That(config.Taxonomies).IsEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments(@"/media\..\..\outside/")]
    [Arguments("/media%5c..%5c..%5coutside/")]
    [Arguments("/media%5C..%5C..%5Coutside/")]
    [Arguments(@"/media\images/")]
    [Arguments(@"https://cdn.example.org/media\..\..\outside/")]
    [Arguments("https://cdn.example.org/media%5c..%5c..%5coutside/")]
    [Arguments("https://cdn.example.org/media%5Cimages/")]
    [Arguments(@"https://cdn.example.org\media")]
    public async Task Load_AssetPrefixWithBackslashes_ThrowsBeforeOutput(string prefix)
    {
        var dir = await CreateTempSiteAsync($"title: Test\nbaseUrl: https://example.org/\nassetPrefix: '{prefix}'\n");
        try
        {
            await Assert.That(() => _loader.Load(dir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("assetPrefix");
            await Assert.That(Directory.Exists(Path.Combine(dir, "_site"))).IsFalse();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_AssetPrefixValidatesPublicUrlBase()
    {
            var validPrefixes = new[]
            {
                ("assetPrefix: /media", "/media/"),
                ("assetPrefix: https://cdn.example.org/v2/media", "https://cdn.example.org/v2/media/"),
                ("assetPrefix: https://cdn.example.org/assets/", "https://cdn.example.org/assets/")
            };

            foreach (var (setting, expected) in validPrefixes)
            {
                var dir = await CreateTempSiteAsync($"title: Test\nbaseUrl: https://example.org/docs/\n{setting}\n");
                try
                {
                    await Assert.That(_loader.Load(dir).AssetPrefix).IsEqualTo(expected);
                }
                finally
                {
                    Directory.Delete(dir, true);
                }
            }

            var aliasDir = await CreateTempSiteAsync("title: Test\nbaseUrl: https://example.org/\nasset_prefix: /legacy/\n");
            try
            {
                await Assert.That(_loader.Load(aliasDir).AssetPrefix).IsEqualTo("/assets/");
            }
            finally
            {
                Directory.Delete(aliasDir, true);
            }

            var invalidPrefixes = new[]
            {
                "cdn.example.org/assets",
                "//cdn.example.org/assets/",
                "file:///private/",
                "/../assets/",
                "https://cdn.example.org/assets/?token=1",
                "https://cdn.example.org/assets/#fragment",
                "https://user:secret@cdn.example.org/assets/",
                "ftp://cdn.example.org/assets/",
                "https://cdn.example.org/a/%2e%2e/assets/",
                "https://cdn.example.org/a/../assets/",
                "/a/%2e%2e/assets/",
                "https://user:pass@cdn.example.org/assets/",
                "/"
            };
            foreach (var prefix in invalidPrefixes)
            {
                var dir = await CreateTempSiteAsync($"title: Test\nbaseUrl: https://example.org/\nassetPrefix: '{prefix}'\n");
                try
                {
                    await Assert.That(() => _loader.Load(dir))
                        .ThrowsExactly<InvalidOperationException>()
                        .WithMessageContaining("assetPrefix");
                }
                finally
                {
                    Directory.Delete(dir, true);
                }
            }
    }

    [Test]
    public async Task WithBaseUrl_PreservesConfiguredAssetPrefix()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: https://example.org/
            assetPrefix: https://cdn.example.org/v2/media/
            """);

        try
        {
            var config = _loader.Load(dir);
            var overridden = config.WithBaseUrl(new Uri("https://example.org/docs/"));

            await Assert.That(config.AssetPrefix).IsEqualTo("https://cdn.example.org/v2/media/");
            await Assert.That(overridden.AssetPrefix).IsEqualTo(config.AssetPrefix);
            await Assert.That(overridden.BasePath).IsEqualTo("/docs");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_CollectionWithoutExplicitDirectory_UsesNativeSeparator()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost

            collections:
              posts:
                permalink: /:slug/
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Collections["posts"].Directory).IsEqualTo(Path.Combine("content", "posts"));
            await Assert.That(config.Collections["posts"].Paginate).IsNull();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments("false", true, 0)]
    [Arguments("0", false, 0)]
    [Arguments("7", false, 7)]
    public async Task Load_CollectionPaginate_FalseAndIntegers(string paginate, bool expectedNull, int expected)
    {
        var dir = await CreateTempSiteAsync($"""
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                paginate: {paginate}
            """);

        try
        {
            var config = _loader.Load(dir);

            if (expectedNull)
                await Assert.That(config.Collections["posts"].Paginate).IsNull();
            else
                await Assert.That(config.Collections["posts"].Paginate).IsEqualTo(expected);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_SiteYml_AlsoWorks()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"engobe-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "site.yml"), """
            title: YML Test
            baseUrl: http://localhost
            """);

        try
        {
            var config = _loader.Load(dir);
            await Assert.That(config.Title).IsEqualTo("YML Test");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_HomePageAndCollectionSet_Throws()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                directory: content/posts
            home:
              page: content/index.md
              collection: posts
            """);

        try
        {
            await Assert.That(() => _loader.Load(dir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("home");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_HomeBlockEmpty_Throws()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            home: {}
            """);

        try
        {
            await Assert.That(() => _loader.Load(dir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("home");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_HomeCollectionUnknown_Throws()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                directory: content/posts
            home:
              collection: pages
            """);

        try
        {
            await Assert.That(() => _loader.Load(dir))
                .ThrowsExactly<InvalidOperationException>()
                .WithMessageContaining("home.collection");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_HomePage_MapsToConfiguration()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            home:
              page: content/index.md
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Home).IsNotNull();
            await Assert.That(config.Home!.Page).IsEqualTo("content/index.md");
            await Assert.That(config.Home.Collection).IsNull();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_HomeCollection_MapsToConfiguration()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                directory: content/posts
            home:
              collection: posts
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Home).IsNotNull();
            await Assert.That(config.Home!.Collection).IsEqualTo("posts");
            await Assert.That(config.Home.Page).IsNull();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_SearchSection_ParsesAllFields()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            search:
              enabled: true
              extended: true
              binaryPath: /usr/local/bin/pagefind
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Search.Enabled).IsTrue();
            await Assert.That(config.Search.Extended).IsTrue();
            await Assert.That(config.Search.BinaryPath).IsEqualTo("/usr/local/bin/pagefind");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_SearchBinaryPathRelative_ResolvesAgainstProjectDirectory()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            search:
              enabled: true
              binaryPath: tools/pagefind
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Search.BinaryPath).IsEqualTo(Path.Combine(dir, "tools", "pagefind"));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_SearchBinaryPathAbsolute_IsUnchanged()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "somewhere", "pagefind");
        var dir = await CreateTempSiteAsync($"""
            title: Test
            baseUrl: http://localhost
            search:
              binaryPath: '{absolute}'
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Search.BinaryPath).IsEqualTo(absolute);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments("binaryPath: ''")]
    [Arguments("binaryPath: '   '")]
    [Arguments("enabled: true")]
    public async Task Load_SearchBinaryPathBlankOrMissing_IsNull(string searchLine)
    {
        var dir = await CreateTempSiteAsync($"""
            title: Test
            baseUrl: http://localhost
            search:
              {searchLine}
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Search.BinaryPath).IsNull();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_SearchSectionAbsent_UsesDefaults()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Search.Enabled).IsFalse();
            await Assert.That(config.Search.Extended).IsFalse();
            await Assert.That(config.Search.BinaryPath).IsNull();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_SearchEnabledOnly_OtherFieldsDefault()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            search:
              enabled: true
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Search.Enabled).IsTrue();
            await Assert.That(config.Search.Extended).IsFalse();
            await Assert.That(config.Search.BinaryPath).IsNull();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_ImagesSection_ParsesAllFields()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            images:
              enabled: false
              maxWidth: 1200
              quality: 70
              webp: true
              exclude:
                - "static/hero-*.png"
                - "static/logos/**"
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Images.Enabled).IsFalse();
            await Assert.That(config.Images.MaxWidth).IsEqualTo(1200);
            await Assert.That(config.Images.Quality).IsEqualTo(70);
            await Assert.That(config.Images.Webp).IsTrue();
            await Assert.That(config.Images.Exclude).IsEquivalentTo(["static/hero-*.png", "static/logos/**"]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_ImagesSectionAbsent_UsesDefaults()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Images.Enabled).IsTrue();
            await Assert.That(config.Images.MaxWidth).IsEqualTo(2000);
            await Assert.That(config.Images.Quality).IsEqualTo(82);
            await Assert.That(config.Images.Webp).IsFalse();
            await Assert.That(config.Images.Exclude).IsEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_CollectionTeaserWords_OverridesDefault()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                teaserWords: 30
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Collections["posts"].TeaserWords).IsEqualTo(30);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task Load_CollectionTeaserWordsAbsent_UsesDefault()
    {
        var dir = await CreateTempSiteAsync("""
            title: Test
            baseUrl: http://localhost
            collections:
              posts:
                permalink: /:slug/
            """);

        try
        {
            var config = _loader.Load(dir);

            await Assert.That(config.Collections["posts"].TeaserWords).IsEqualTo(ContentGroup.DefaultTeaserWords);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static async Task<string> CreateTempSiteAsync(string yamlContent)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"engobe-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "site.yaml"), yamlContent);
        return dir;
    }
}
