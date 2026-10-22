namespace Kiln.Core.Tests.Services;

using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Xml.Linq;
using AngleSharp.Html.Parser;
using Kiln.Abstractions;
using Kiln.Services;

public class AssetPipelineIntegrationTests
{
    private const string SiteCssRelativePath = "themes/default/static/css/site.css";
    private const string SiteCssAssetPath = "/assets/css/site.css";
    private const string DeadLinkPath = "/does-not-exist/";
    private static readonly Regex FingerprintedCssRegex = new("^site\\.[0-9a-f]{8}\\.css$", RegexOptions.Compiled);

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task BuildAsync_Production_PreservesDocumentTypeWhenFingerprinting(bool minifyHtml)
    {
        var dir = CreateSite();
        try
        {
            var layoutPath = Path.Combine(dir, "themes", "default", "layouts", "default.html");
            await File.WriteAllTextAsync(layoutPath, "<!DOCTYPE html>\n" + await File.ReadAllTextAsync(layoutPath));
            var configPath = Path.Combine(dir, "site.yaml");
            await File.WriteAllTextAsync(configPath, (await File.ReadAllTextAsync(configPath))
                .Replace("build:\n", $"build:\n  minify:\n    html: {minifyHtml.ToString().ToLowerInvariant()}\n", StringComparison.Ordinal));
            var result = await CreateBuilder([new NuglifyAssetMinifier()])
                .BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
            await Assert.That(result.Errors).IsEmpty();
            await Assert.That(result.Success).IsTrue();
            var html = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));
            var document = await new HtmlParser().ParseDocumentAsync(html);
            await Assert.That(document.Doctype).IsNotNull();
            await Assert.That(document.Doctype.Name).IsEqualTo("html");
            var cssName = GetSingleFingerprintedCssFileName(Path.Combine(dir, "_site", "assets", "css"));
            await Assert.That(document.QuerySelector("link")!.GetAttribute("href")).IsEqualTo("/assets/css/" + cssName);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments("https://cdn.example.com/v2/media/")]
    [Arguments("https://cdn.example.com/assets/")]
    [Arguments("http://cdn.example.com:8080/v2/media/")]
    public async Task BuildAsync_Development_ProjectsAuthoredCdnUrlsButPreservesForeignBoundaries(string prefix)
    {
        var dir = CreateReferenceFixture(prefix, "https://example.org/docs/");
        try
        {
            var contentPath = Path.Combine(dir, "content", "posts", "hello-world.md");
            var foreignPath = prefix.TrimEnd('/') + "-other/missing.svg";
            var foreignHost = prefix.Replace("cdn.example.com", "cdn.example.com.evil", StringComparison.Ordinal) + "missing.svg";
            var foreignPort = new UriBuilder(prefix) { Port = 444 }.Uri.AbsoluteUri + "missing.svg";
            await File.WriteAllTextAsync(contentPath, (await File.ReadAllTextAsync(contentPath))
                .Replace("/assets/", prefix, StringComparison.Ordinal)
                + $"\n<img src=\"{foreignPath}\"><img src=\"{foreignHost}\"><img src=\"{foreignPort}\">");
            var cssPath = Path.Combine(dir, "themes", "default", "static", "css", "site.css");
            await File.WriteAllTextAsync(cssPath,
                $"@import '{prefix}css/base.css' screen; .image {{ background: url('{prefix}images/picture.svg?v=2#hero'); }}"
                + $" .foreign {{ background: url('{foreignPath}'); }}");

            var result = await CreateBuilder([new NuglifyAssetMinifier()])
                .BuildAsync(dir, false, BuildEnvironment.Development, CancellationToken.None);
            await Assert.That(result.Errors).IsEmpty();
            await Assert.That(result.Success).IsTrue();
            var html = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));
            var document = await new HtmlParser().ParseDocumentAsync(html);
            await Assert.That(document.QuerySelector("img")!.GetAttribute("src")).IsEqualTo("/docs/assets/images/picture.svg");
            await Assert.That(document.QuerySelector("img")!.GetAttribute("srcset"))
                .IsEqualTo("/docs/assets/images/picture.svg 1x, https://external.example/picture.svg 2x, data:image/svg+xml,%3Csvg%3E 3x");
            await Assert.That(document.QuerySelector("video")!.GetAttribute("poster")).IsEqualTo("/docs/assets/images/picture.svg");
            await Assert.That(html).Contains(foreignPath);
            await Assert.That(html).Contains(foreignHost);
            await Assert.That(html).Contains(foreignPort);
            await Assert.That(document.QuerySelector("script")!.TextContent).Contains(prefix + "images/picture.svg");

            var css = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "assets", "css", "site.css"));
            await Assert.That(css).Contains("@import '/docs/assets/css/base.css' screen");
            await Assert.That(css).Contains("/docs/assets/images/picture.svg?v=2#hero");
            await Assert.That(css).Contains(foreignPath);
            var feed = XDocument.Load(Path.Combine(dir, "_site", "blog", "feed.xml"));
            var feedHtml = feed.Descendants().Single(element => element.Name.LocalName == "content").Value;
            var feedDocument = await new HtmlParser().ParseDocumentAsync(feedHtml);
            await Assert.That(feedDocument.QuerySelector("img")!.GetAttribute("src"))
                .IsEqualTo("https://example.org/docs/assets/images/picture.svg");
            await Assert.That(feedDocument.QuerySelector("video")!.GetAttribute("poster"))
                .IsEqualTo("https://example.org/docs/assets/images/picture.svg");
            await Assert.That(feedDocument.QuerySelector("a")!.GetAttribute("href"))
                .IsEqualTo("https://example.org/docs/assets/images/picture.svg?variant=small&mode=detail#preview");
            await Assert.That(new SiteConfigLoader().Load(dir).AssetPrefix).IsEqualTo(prefix);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Production_MinifiesFingerprintsAndRewritesReferences()
    {
        var dir = CreateSite();

        try
        {
            var sourceCssPath = Path.Combine(dir, SiteCssRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var sourceCssLength = new FileInfo(sourceCssPath).Length;

            var builder = CreateBuilder([new NuglifyAssetMinifier()]);
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();

            var cssOutputDir = Path.Combine(dir, "_site", "assets", "css");
            var hashedCssFiles = Directory.GetFiles(cssOutputDir, "site.*.css");
            await Assert.That(hashedCssFiles).Count().IsEqualTo(1);

            var hashedCssFileName = Path.GetFileName(hashedCssFiles[0]);
            await Assert.That(FingerprintedCssRegex.IsMatch(hashedCssFileName)).IsTrue();
            await Assert.That(File.Exists(Path.Combine(cssOutputDir, "site.css"))).IsFalse();

            var minifiedCssLength = new FileInfo(hashedCssFiles[0]).Length;
            await Assert.That(minifiedCssLength).IsLessThan(sourceCssLength);

            var postHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));
            await Assert.That(postHtml).Contains($"/assets/css/{hashedCssFileName}");
            await Assert.That(postHtml).DoesNotContain(SiteCssAssetPath);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Production_FailsOnDeadInternalLink()
    {
        var dir = CreateSite(includeDeadLink: true);

        try
        {
            var builder = CreateBuilder([new NuglifyAssetMinifier()]);
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Any(static error =>
                    error.Contains(DeadLinkPath, StringComparison.OrdinalIgnoreCase)))
                .IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Development_LeavesAssetsUnchangedAndIgnoresDeadLinks()
    {
        var dir = CreateSite(includeDeadLink: true);

        try
        {
            var sourceCssPath = Path.Combine(dir, SiteCssRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var sourceCss = await File.ReadAllTextAsync(sourceCssPath);

            var builder = CreateBuilder([new NuglifyAssetMinifier()]);
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Development, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();

            var cssOutputDir = Path.Combine(dir, "_site", "assets", "css");
            var plainCssPath = Path.Combine(cssOutputDir, "site.css");
            await Assert.That(File.Exists(plainCssPath)).IsTrue();
            await Assert.That(Directory.GetFiles(cssOutputDir, "site.*.css")).Count().IsEqualTo(0);

            var outputCss = await File.ReadAllTextAsync(plainCssPath);
            await Assert.That(outputCss).IsEqualTo(sourceCss);

            var postHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));
            await Assert.That(postHtml).Contains(SiteCssAssetPath);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Production_FingerprintIsDeterministic()
    {
        var dir = CreateSite();

        try
        {
            var builder = CreateBuilder([new NuglifyAssetMinifier()]);

            var firstResult = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
            await Assert.That(firstResult.Success).IsTrue();
            var firstHashFileName = GetSingleFingerprintedCssFileName(Path.Combine(dir, "_site", "assets", "css"));

            var secondResult = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
            await Assert.That(secondResult.Success).IsTrue();
            var secondHashFileName = GetSingleFingerprintedCssFileName(Path.Combine(dir, "_site", "assets", "css"));

            await Assert.That(secondHashFileName).IsEqualTo(firstHashFileName);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Production_UnknownMinifierIdReturnsError()
    {
        var dir = CreateSite(minifierId: "doesnotexist");

        try
        {
            var builder = CreateBuilder([new NuglifyAssetMinifier()]);
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Errors.Any(static error =>
                    error.Contains("Unknown asset minifier id 'doesnotexist'", StringComparison.OrdinalIgnoreCase)))
                .IsTrue();
            await Assert.That(result.Errors.Any(static error =>
                    error.Contains("'nuglify'", StringComparison.OrdinalIgnoreCase)))
                .IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Production_FingerprintDisabled_KeepsPlainCssFile()
    {
        var dir = CreateSite(fingerprint: false);

        try
        {
            var sourceCssPath = Path.Combine(dir, SiteCssRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var sourceCssLength = new FileInfo(sourceCssPath).Length;

            var builder = CreateBuilder([new NuglifyAssetMinifier()]);
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();

            var cssOutputDir = Path.Combine(dir, "_site", "assets", "css");
            var plainCssPath = Path.Combine(cssOutputDir, "site.css");
            await Assert.That(File.Exists(plainCssPath)).IsTrue();

            var allCssFileNames = Directory.EnumerateFiles(cssOutputDir, "*.css")
                .Select(Path.GetFileName)
                .Where(static name => name is not null)
                .Cast<string>()
                .ToArray();
            await Assert.That(allCssFileNames.Any(FingerprintedCssRegex.IsMatch)).IsFalse();

            var minifiedCssLength = new FileInfo(plainCssPath).Length;
            await Assert.That(minifiedCssLength).IsLessThan(sourceCssLength);

            var postHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));
            await Assert.That(postHtml).Contains(SiteCssAssetPath);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task BuildAsync_Production_LinkCheckDisabled_AllowsDeadInternalLink()
    {
        var dir = CreateSite(includeDeadLink: true, linkCheck: false);

        try
        {
            var builder = CreateBuilder([new NuglifyAssetMinifier()]);
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Errors).Count().IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

        [Test]
        public async Task BuildAsync_Production_CdnFinalizesCssHtmlAndAtomReferences()
        {
            var dir = CreateReferenceFixture("https://cdn.example.com/v2/media/", "https://example.org/docs/");

            try
            {
                var result = await CreateBuilder([new NuglifyAssetMinifier()])
                    .BuildAsync(dir, false, BuildEnvironment.Production, progress: null, new Uri("https://example.org/docs/"), CancellationToken.None);

                await Assert.That(result.Success).IsTrue();
                var assetsDirectory = Path.Combine(dir, "_site", "assets");
                await Assert.That(Directory.Exists(Path.Combine(dir, "_site", "https:", "cdn.example.com"))).IsFalse();

                var siteCssFile = Directory.GetFiles(Path.Combine(assetsDirectory, "css"), "site.*.css").Single();
                var baseCssFile = Directory.GetFiles(Path.Combine(assetsDirectory, "css"), "base.*.css").Single();
                var fontFile = Directory.GetFiles(Path.Combine(assetsDirectory, "fonts"), "main.*.woff2").Single();
                var imageFileName = Path.GetFileName(Directory.GetFiles(Path.Combine(assetsDirectory, "images"), "picture.*.svg").Single());
                await Assert.That(FileNameHashMatches(siteCssFile)).IsTrue();
                await Assert.That(FileNameHashMatches(baseCssFile)).IsTrue();
                await Assert.That(FileNameHashMatches(fontFile)).IsTrue();

                var siteCss = await File.ReadAllTextAsync(siteCssFile);
                var baseCss = await File.ReadAllTextAsync(baseCssFile);
                await Assert.That(siteCss).Contains($"https://cdn.example.com/v2/media/css/{Path.GetFileName(baseCssFile)}");
                await Assert.That(siteCss).Contains("screen");
                await Assert.That(siteCss).Contains("print");
                await Assert.That(siteCss).Contains("supports(display: grid)");
                await Assert.That(baseCss).Contains($"https://cdn.example.com/v2/media/fonts/{Path.GetFileName(fontFile)}?v=2#font");

                var html = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));
                await Assert.That(html).Contains($"https://cdn.example.com/v2/media/images/{imageFileName}");
                await Assert.That(html).Contains($"https://cdn.example.com/v2/media/images/{imageFileName} 1x");
                await Assert.That(html).Contains("https://external.example/picture.svg 2x");
                await Assert.That(html).Contains("data:image/svg+xml,%3Csvg%3E 3x");
                await Assert.That(html).Contains($"poster=https://cdn.example.com/v2/media/images/{imageFileName}");
                await Assert.That(html).Contains("href=\"https://cdn.example.com/v2/media/images/picture.");
                await Assert.That(html).Contains("?variant=small&amp;mode=detail#preview");
                await Assert.That(html).Contains("const example=\"/assets/images/picture.svg\"");
                await Assert.That(html).Contains("url('/assets/images/picture.svg')");

                var feedPath = Path.Combine(dir, "_site", "blog", "feed.xml");
                var feed = XDocument.Load(feedPath);
                var feedHtml = feed.Descendants().Single(element => element.Name.LocalName == "content").Value;
                await Assert.That(feedHtml).Contains($"https://cdn.example.com/v2/media/images/{imageFileName}");
                await Assert.That(feedHtml).Contains("srcset");
                await Assert.That(feedHtml).Contains("?variant=small&amp;mode=detail#preview");
                await Assert.That(feedHtml).Contains("https://external.example/picture.svg 2x");
                await Assert.That(feed.Descendants().Single(element => element.Name.LocalName == "link"
                        && (string?)element.Attribute("rel") == "self")
                    .Attribute("href")!.Value).Contains("https://example.org/docs/blog/feed.xml");
                await Assert.That(feed.Descendants().Single(element => element.Name.LocalName == "link"
                        && (string?)element.Attribute("rel") == "alternate")
                    .Attribute("href")!.Value).IsEqualTo("https://example.org/docs/blog/");
                await Assert.That(feed.Descendants().Single(element => element.Name.LocalName == "entry")
                    .Elements().Single(element => element.Name.LocalName == "link")
                    .Attribute("href")!.Value).IsEqualTo("https://example.org/docs/blog/hello-world/");
                await Assert.That(Directory.EnumerateFiles(assetsDirectory, "*", SearchOption.AllDirectories)
                    .All(file => Path.GetRelativePath(Path.Combine(dir, "_site"), file).StartsWith("assets", StringComparison.Ordinal)))
                    .IsTrue();

                var secondBuild = await CreateBuilder([new NuglifyAssetMinifier()])
                    .BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
                await Assert.That(secondBuild.Success).IsTrue();
                await Assert.That(Path.GetFileName(Directory.GetFiles(Path.Combine(assetsDirectory, "css"), "site.*.css").Single()))
                    .IsEqualTo(Path.GetFileName(siteCssFile));
                await Assert.That(Path.GetFileName(Directory.GetFiles(Path.Combine(assetsDirectory, "css"), "base.*.css").Single()))
                    .IsEqualTo(Path.GetFileName(baseCssFile));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public async Task BuildAsync_Production_ProjectsShortcodeAndPluginSlotAssets()
        {
            var dir = CreateReferenceFixture("https://cdn.example.com/v2/media/", "https://example.org/docs/");

            try
            {
                var pluginDirectory = Path.Combine(dir, "plugins", "demo");
                Directory.CreateDirectory(Path.Combine(pluginDirectory, "shortcodes"));
                Directory.CreateDirectory(Path.Combine(pluginDirectory, "slots"));
                Directory.CreateDirectory(Path.Combine(pluginDirectory, "static", "js"));
                await File.WriteAllTextAsync(
                    Path.Combine(pluginDirectory, "plugin.yaml"),
                    "name: demo\nversion: 1.0.0\nslots:\n  - body_end\nshortcodes:\n  - badge\n");
                await File.WriteAllTextAsync(
                    Path.Combine(pluginDirectory, "shortcodes", "badge.html"),
                    "<img src=\"{{ plugin_asset_url 'js/plugin.js' }}\">");
                await File.WriteAllTextAsync(
                    Path.Combine(pluginDirectory, "slots", "body_end.html"),
                    "<a href=\"{{ plugin_asset_url 'demo' 'js/plugin.js' }}\">Plugin asset</a>");
                await File.WriteAllTextAsync(Path.Combine(pluginDirectory, "static", "js", "plugin.js"), "window.pluginAsset = true;");

                var postLayout = Path.Combine(dir, "themes", "default", "layouts", "post.html");
                var layout = await File.ReadAllTextAsync(postLayout);
                await File.WriteAllTextAsync(postLayout, layout.Replace("</body>", "{{ slot 'body_end' }}</body>", StringComparison.Ordinal));
                var defaultLayout = Path.Combine(dir, "themes", "default", "layouts", "default.html");
                var defaultLayoutContent = await File.ReadAllTextAsync(defaultLayout);
                await File.WriteAllTextAsync(
                    defaultLayout,
                    defaultLayoutContent.Replace("</body>", "{{ slot 'body_end' }}</body>", StringComparison.Ordinal));
                var configPath = Path.Combine(dir, "site.yaml");
                var config = await File.ReadAllTextAsync(configPath);
                await File.WriteAllTextAsync(
                    configPath,
                    config.Replace("    feed: true", "    feed: true\n    plugins:\n      demo:\n        enabled: true", StringComparison.Ordinal));
                var postPath = Path.Combine(dir, "content", "posts", "hello-world.md");
                await File.AppendAllTextAsync(postPath, "\n\n{% badge %}\n");

                var result = await CreateBuilder([new NuglifyAssetMinifier()])
                    .BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

                await Assert.That(string.Join("; ", result.Errors)).IsEmpty();
                await Assert.That(result.Success).IsTrue();
                var pluginFile = Directory.GetFiles(Path.Combine(dir, "_site", "assets", "plugins", "demo", "js"), "plugin.*.js").Single();
                var pluginName = Path.GetFileName(pluginFile);
                var html = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));
                await Assert.That(html).Contains($"src=https://cdn.example.com/v2/media/plugins/demo/js/{pluginName}");
                await Assert.That(html).Contains($"href=https://cdn.example.com/v2/media/plugins/demo/js/{pluginName}");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public async Task BuildAsync_Production_CssCyclesFailOnlyWhenFingerprinting()
        {
            var dir = CreateReferenceFixture("/media/", "http://localhost:5555/");

            try
            {
                await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "static", "css", "base.css"),
                    "@import 'site.css'; .base { color: red; }");
                var builder = CreateBuilder([new NuglifyAssetMinifier()]);
                var configPath = Path.Combine(dir, "site.yaml");
                var yaml = await File.ReadAllTextAsync(configPath);
                await File.WriteAllTextAsync(configPath, yaml.Replace("  linkCheck: true", "  linkCheck: false", StringComparison.Ordinal));

                var fingerprinted = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
                await Assert.That(fingerprinted.Success).IsFalse();
                var diagnostic = string.Join("; ", fingerprinted.Errors);
                await Assert.That(
                    diagnostic.Contains("css/site.css -> css/base.css -> css/site.css", StringComparison.Ordinal)
                    || diagnostic.Contains("css/base.css -> css/site.css -> css/base.css", StringComparison.Ordinal)).IsTrue();

                await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "static", "css", "base.css"), "@import 'base.css';");
                var selfCycle = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
                await Assert.That(selfCycle.Success).IsFalse();
                await Assert.That(string.Join("; ", selfCycle.Errors)).Contains("css/base.css -> css/base.css");

                yaml = await File.ReadAllTextAsync(configPath);
                await File.WriteAllTextAsync(configPath,
                    yaml.Replace("  fingerprint: true", "  fingerprint: false", StringComparison.Ordinal));
                var unhashed = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
                await Assert.That(unhashed.Success).IsTrue();
                await Assert.That(File.Exists(Path.Combine(dir, "_site", "assets", "css", "site.css"))).IsTrue();
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public async Task BuildAsync_Production_CdnCombinesWebpFingerprintAndAbsoluteFeedAssets()
        {
            var dir = CreateSite();

            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "static", "img"));
                await File.WriteAllBytesAsync(Path.Combine(dir, "static", "img", "referenced.png"), "source image"u8.ToArray());
                await File.WriteAllTextAsync(
                    Path.Combine(dir, "content", "posts", "hello-world.md"),
                    """
                    ---
                    title: Hello World
                    date: 2026-01-01
                    ---
                    ![Referenced](/assets/img/referenced.png)
                    """);
                var configPath = Path.Combine(dir, "site.yaml");
                var yaml = await File.ReadAllTextAsync(configPath);
                yaml = yaml
                    .Replace("baseUrl: http://localhost:5555", "baseUrl: https://example.org/docs/", StringComparison.Ordinal)
                    .Replace("    permalink: /blog/:slug/", "    permalink: /blog/:slug/\n    feed: true", StringComparison.Ordinal)
                    .Replace("build:\n", "assetPrefix: https://cdn.example.com/v2/media/\nbuild:\n", StringComparison.Ordinal)
                    + "\nimages:\n  enabled: true\n  webp: true\n";
                await File.WriteAllTextAsync(configPath, yaml);

                var result = await CreateBuilder(
                        [new NuglifyAssetMinifier()],
                        new WebpTestImageOptimizer())
                    .BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

                await Assert.That(result.Success).IsTrue();
                var webpFile = Directory.GetFiles(Path.Combine(dir, "_site", "assets", "img"), "referenced.*.webp").Single();
                await Assert.That(FileNameHashMatches(webpFile)).IsTrue();
                var postHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));
                await Assert.That(postHtml).Contains($"https://cdn.example.com/v2/media/img/{Path.GetFileName(webpFile)}");
                var feed = XDocument.Load(Path.Combine(dir, "_site", "blog", "feed.xml"));
                var feedHtml = feed.Descendants().Single(element => element.Name.LocalName == "content").Value;
                await Assert.That(feedHtml).Contains($"https://cdn.example.com/v2/media/img/{Path.GetFileName(webpFile)}");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public async Task BuildAsync_Production_LocalPrefixAndBaseUrlOverride_ProjectsOnlyLocalAssetBase()
        {
            var dir = CreateReferenceFixture("/media/", "http://localhost:5555/");

            try
            {
                var result = await CreateBuilder([new NuglifyAssetMinifier()])
                    .BuildAsync(dir, false, BuildEnvironment.Production, progress: null, new Uri("https://example.org/docs/"), CancellationToken.None);

                await Assert.That(result.Success).IsTrue();
                var html = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));
                await Assert.That(html).Contains("/docs/media/css/site.");
                await Assert.That(html).Contains("/docs/media/images/picture.");
                await Assert.That(html).DoesNotContain("cdn.example.com");

                var feed = XDocument.Load(Path.Combine(dir, "_site", "blog", "feed.xml"));
                var feedHtml = feed.Descendants().Single(element => element.Name.LocalName == "content").Value;
                await Assert.That(feedHtml).Contains("https://example.org/docs/media/images/picture.");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public async Task BuildAsync_Development_CdnFallbackProjectsCssAndFeedLocally()
        {
            var dir = CreateReferenceFixture("https://cdn.example.com/v2/media/", "https://example.org/docs/");

            try
            {
                var originalConfig = await File.ReadAllTextAsync(Path.Combine(dir, "site.yaml"));
                var result = await CreateBuilder([new NuglifyAssetMinifier()])
                    .BuildAsync(dir, false, BuildEnvironment.Development, CancellationToken.None);

                await Assert.That(result.Success).IsTrue();
                await Assert.That(await File.ReadAllTextAsync(Path.Combine(dir, "site.yaml"))).IsEqualTo(originalConfig);
                var css = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "assets", "css", "site.css"));
                await Assert.That(css).Contains("/docs/assets/css/base.css");
                await Assert.That(css).DoesNotContain("cdn.example.com");
                await Assert.That(File.Exists(Path.Combine(dir, "_site", "assets", "css", "site.css"))).IsTrue();
                await Assert.That(File.Exists(Path.Combine(dir, "_site", "assets", "css", "site." + new string('0', 8) + ".css"))).IsFalse();

                var feed = XDocument.Load(Path.Combine(dir, "_site", "blog", "feed.xml"));
                var imageFileName = Path.GetFileName(Directory.GetFiles(Path.Combine(dir, "_site", "assets", "images"), "picture.svg").Single());
                await Assert.That(feed.Descendants().Single(element => element.Name.LocalName == "content").Value)
                    .Contains($"https://example.org/docs/assets/images/{imageFileName}");

                var production = await CreateBuilder([new NuglifyAssetMinifier()])
                    .BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
                await Assert.That(production.Success).IsTrue();
                var productionHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));
                await Assert.That(productionHtml).Contains("https://cdn.example.com/v2/media/css/site.");

                var secondDevelopment = await CreateBuilder([new NuglifyAssetMinifier()])
                    .BuildAsync(dir, false, BuildEnvironment.Development, CancellationToken.None);
                await Assert.That(secondDevelopment.Success).IsTrue();
                var secondDevelopmentHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello-world", "index.html"));
                await Assert.That(secondDevelopmentHtml).Contains("/docs/assets/css/site.css");
                await Assert.That(secondDevelopmentHtml).DoesNotContain("cdn.example.com");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public async Task BuildAsync_Development_RetainsCustomLocalPrefixInCssAndFeed()
        {
            var dir = CreateReferenceFixture("/media/", "https://example.org/docs/");

            try
            {
                var result = await CreateBuilder([new NuglifyAssetMinifier()])
                    .BuildAsync(dir, false, BuildEnvironment.Development, CancellationToken.None);

                await Assert.That(result.Success).IsTrue();
                var css = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "assets", "css", "site.css"));
                await Assert.That(css).Contains("/docs/media/css/base.css");
                await Assert.That(css).Contains("supports(display: grid)");
                var baseCss = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "assets", "css", "base.css"));
                await Assert.That(baseCss).Contains("/docs/media/fonts/main.woff2?v=2#font");
                var feed = XDocument.Load(Path.Combine(dir, "_site", "blog", "feed.xml"));
                var feedHtml = feed.Descendants().Single(element => element.Name.LocalName == "content").Value;
                await Assert.That(feedHtml).Contains("https://example.org/docs/media/images/picture.svg");
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        [Arguments("/media/", false, false)]
        [Arguments("/media/", true, false)]
        [Arguments("/media/", false, true)]
        [Arguments("/media/", true, true)]
        [Arguments("https://cdn.example.com/v2/media/", false, false)]
        [Arguments("https://cdn.example.com/v2/media/", true, false)]
        [Arguments("https://cdn.example.com/v2/media/", false, true)]
        [Arguments("https://cdn.example.com/v2/media/", true, true)]
        public async Task BuildAsync_OfflineLinkCheckRequiresExactPathCase(string prefix, bool wrongCase, bool fingerprint)
        {
            var dir = CreateReferenceFixture(prefix, "https://example.org/docs/");
            try
            {
                var configPath = Path.Combine(dir, "site.yaml");
                await File.WriteAllTextAsync(configPath, (await File.ReadAllTextAsync(configPath))
                    .Replace("fingerprint: true", $"fingerprint: {fingerprint.ToString().ToLowerInvariant()}", StringComparison.Ordinal));
                var imageName = wrongCase ? "Picture.svg" : "picture.svg";
                var fontName = wrongCase ? "Main.woff2" : "main.woff2";
                var slug = wrongCase ? "Hello-world" : "hello-world";
                await File.WriteAllTextAsync(Path.Combine(dir, "content", "posts", "hello-world.md"),
                    $"""
                    ---
                    title: Hello World
                    date: 2026-01-01
                    ---
                    <img src="/assets/images/{imageName}?v=2#preview">
                    <a href="/docs/blog/{slug}/">Post</a>
                    """);
                await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "static", "css", "base.css"),
                    $".base {{ src: url('../fonts/{fontName}?v=2#font'); }}");
                var importName = wrongCase ? "Base.css" : "base.css";
                await File.WriteAllTextAsync(Path.Combine(dir, "themes", "default", "static", "css", "site.css"),
                    $"@import '{importName}';");

                var result = await CreateBuilder([new NuglifyAssetMinifier()])
                    .BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

                await Assert.That(result.Success).IsEqualTo(!wrongCase);
                if (wrongCase)
                {
                    await Assert.That(result.Errors.Any(static error =>
                        error.Contains("/blog/hello-world/index.html", StringComparison.Ordinal)
                        && error.Contains("Picture.svg?v=2#preview", StringComparison.Ordinal))).IsTrue();
                    await Assert.That(result.Errors.Any(static error =>
                        error.Contains("/assets/css/base", StringComparison.Ordinal)
                        && error.Contains("Main.woff2?v=2#font", StringComparison.Ordinal))).IsTrue();
                    await Assert.That(result.Errors.Any(static error =>
                        error.Contains("/blog/feed.xml", StringComparison.Ordinal)
                        && error.Contains("Picture.svg?v=2#preview", StringComparison.Ordinal))).IsTrue();
                    await Assert.That(result.Errors.Any(static error =>
                        error.Contains("/assets/css/site", StringComparison.Ordinal)
                        && error.Contains("Base.css", StringComparison.Ordinal))).IsTrue();
                    await Assert.That(string.Join("\n", result.Errors)).Contains("/docs/blog/Hello-world/");
                }
                else
                {
                    await Assert.That(result.Errors).IsEmpty();
                }
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public async Task BuildAsync_OfflineLinkCheckReportsMissingCssHtmlAndFeedTargetsWithoutNetwork()
        {
            var dir = CreateReferenceFixture("https://cdn.example.com/v2/media/", "https://example.org/docs/");

            try
            {
                await File.WriteAllTextAsync(
                    Path.Combine(dir, "themes", "default", "static", "css", "base.css"),
                    ".missing { src: url('../fonts/missing.woff2?v=2#font'); }");
                await File.WriteAllTextAsync(
                    Path.Combine(dir, "content", "posts", "hello-world.md"),
                    """
                    ---
                    title: Hello World
                    date: 2026-01-01
                    ---
                    <img srcset="/assets/images/missing.svg 1x, https://cdn.example.com.evil.test/v2/media/foreign.svg 2x">
                    <video poster="/assets/videos/missing.mp4"></video>
                    """);

                var builder = CreateBuilder([new NuglifyAssetMinifier()]);
                var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

                await Assert.That(result.Success).IsFalse();
                var diagnostics = string.Join("\n", result.Errors);
                await Assert.That(diagnostics).Contains("/assets/css/base.");
                await Assert.That(diagnostics).Contains("https://cdn.example.com/v2/media/fonts/missing.woff2?v=2#font");
                await Assert.That(diagnostics).Contains("https://cdn.example.com/v2/media/images/missing.svg");
                await Assert.That(diagnostics).Contains("https://cdn.example.com/v2/media/videos/missing.mp4");
                await Assert.That(diagnostics).DoesNotContain("cdn.example.com.evil.test");

                var configPath = Path.Combine(dir, "site.yaml");
                var yaml = await File.ReadAllTextAsync(configPath);
                await File.WriteAllTextAsync(configPath, yaml.Replace("  linkCheck: true", "  linkCheck: false", StringComparison.Ordinal));
                var disabled = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
                await Assert.That(disabled.Success).IsTrue();
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public async Task BuildAsync_RejectsLocalAssetNamespacePageCollisionsBeforeReplacingOutput()
        {
            var collidingSite = CreateSite();
            var similarSite = CreateSite();
            var sameOriginSite = CreateSite();
            var foreignCdnSite = CreateSite();
            var liveReloadSite = CreateSite();
            var liveReloadNeighborSite = CreateSite();
            var pagefindSite = CreateSite();
            var developmentFallbackSite = CreateSite();

            try
            {
                await SetAssetPrefixAsync(collidingSite, "/blog/");
                var oldOutput = Path.Combine(collidingSite, "_site");
                Directory.CreateDirectory(oldOutput);
                await File.WriteAllTextAsync(Path.Combine(oldOutput, "old-marker.txt"), "preserve");

                var builder = CreateBuilder([new NuglifyAssetMinifier()]);
                var collision = await builder.BuildAsync(collidingSite, false, BuildEnvironment.Production, CancellationToken.None);
                await Assert.That(collision.Success).IsFalse();
                await Assert.That(string.Join("; ", collision.Errors)).Contains("collides with generated page");
                await Assert.That(await File.ReadAllTextAsync(Path.Combine(oldOutput, "old-marker.txt"))).IsEqualTo("preserve");

                await SetAssetPrefixAsync(similarSite, "/blogroll/");
                var similar = await builder.BuildAsync(similarSite, false, BuildEnvironment.Production, CancellationToken.None);
                await Assert.That(similar.Success).IsTrue();

                await SetAssetPrefixAsync(sameOriginSite, "http://localhost:5555/blog/");
                var sameOrigin = await builder.BuildAsync(sameOriginSite, false, BuildEnvironment.Production, CancellationToken.None);
                await Assert.That(sameOrigin.Success).IsFalse();
                await Assert.That(string.Join("; ", sameOrigin.Errors)).Contains("collides with generated page");

                await SetAssetPrefixAsync(foreignCdnSite, "https://cdn.example.com/blog/");
                var foreignCdn = await builder.BuildAsync(foreignCdnSite, false, BuildEnvironment.Production, CancellationToken.None);
                await Assert.That(foreignCdn.Success).IsTrue();
                var foreignCdnIndex = await File.ReadAllTextAsync(Path.Combine(foreignCdnSite, "_site", "blog", "index.html"));
                await Assert.That(foreignCdnIndex).Contains("/blog/hello-world/");
                await Assert.That(foreignCdnIndex).DoesNotContain("https://cdn.example.com/blog/hello-world");

                await SetAssetPrefixAsync(liveReloadSite, "/__kiln/livereload/");
                var liveReloadCollision = await builder.BuildAsync(liveReloadSite, false, BuildEnvironment.Production, CancellationToken.None);
                await Assert.That(liveReloadCollision.Success).IsFalse();
                await Assert.That(string.Join("; ", liveReloadCollision.Errors)).Contains("overlaps reserved route");

                await SetAssetPrefixAsync(liveReloadNeighborSite, "/__kiln/livereloading/");
                var liveReloadNeighbor = await builder.BuildAsync(liveReloadNeighborSite, false, BuildEnvironment.Production, CancellationToken.None);
                await Assert.That(liveReloadNeighbor.Success).IsTrue();

                await SetAssetPrefixAsync(pagefindSite, "/pagefind/", enableSearch: true);
                var pagefindCollision = await builder.BuildAsync(pagefindSite, false, BuildEnvironment.Production, CancellationToken.None);
                await Assert.That(pagefindCollision.Success).IsFalse();
                await Assert.That(string.Join("; ", pagefindCollision.Errors)).Contains("overlaps reserved route");

                var fallbackConfig = Path.Combine(developmentFallbackSite, "site.yaml");
                var fallbackYaml = await File.ReadAllTextAsync(fallbackConfig);
                fallbackYaml = fallbackYaml
                    .Replace("    permalink: /blog/:slug/", "    permalink: /assets/:slug/", StringComparison.Ordinal)
                    .Replace("build:\n", "assetPrefix: https://cdn.example.com/v2/media/\nbuild:\n", StringComparison.Ordinal);
                await File.WriteAllTextAsync(fallbackConfig, fallbackYaml);
                var fallbackOutput = Path.Combine(developmentFallbackSite, "_site");
                Directory.CreateDirectory(fallbackOutput);
                await File.WriteAllTextAsync(Path.Combine(fallbackOutput, "old-marker.txt"), "preserve");
                var developmentCollision = await builder.BuildAsync(
                    developmentFallbackSite,
                    false,
                    BuildEnvironment.Development,
                    CancellationToken.None);
                await Assert.That(developmentCollision.Success).IsFalse();
                await Assert.That(string.Join("; ", developmentCollision.Errors)).Contains("collides with generated page");
                await Assert.That(await File.ReadAllTextAsync(Path.Combine(fallbackOutput, "old-marker.txt"))).IsEqualTo("preserve");
            }
            finally
            {
                Directory.Delete(collidingSite, true);
                Directory.Delete(similarSite, true);
                Directory.Delete(sameOriginSite, true);
                Directory.Delete(foreignCdnSite, true);
                Directory.Delete(liveReloadSite, true);
                Directory.Delete(liveReloadNeighborSite, true);
                Directory.Delete(pagefindSite, true);
                Directory.Delete(developmentFallbackSite, true);
            }
        }

    [Test]
    [Arguments("/media/")]
    [Arguments("/media/review/")]
    public async Task BuildAsync_RejectsVirtualTaxonomyAssetCollisionsBeforeWritingOutput(string assetPrefix)
    {
        var dir = CreateSite();
        try
        {
            var configPath = Path.Combine(dir, "site.yaml");
            await File.WriteAllTextAsync(configPath, (await File.ReadAllTextAsync(configPath))
                .Replace("baseUrl: http://localhost:5555", "baseUrl: https://example.org/docs/", StringComparison.Ordinal)
                .Replace("    permalink: /blog/:slug/", "    permalink: /blog/:slug/\n    taxonomies:\n      - topics", StringComparison.Ordinal)
                + $"\nassetPrefix: {assetPrefix}\ntaxonomies:\n  topics:\n    permalink: /media/:slug/\n");
            var contentPath = Path.Combine(dir, "content", "posts", "hello-world.md");
            await File.WriteAllTextAsync(contentPath, (await File.ReadAllTextAsync(contentPath))
                .Replace("title: Hello World", "title: Hello World\ntopics:\n  - review", StringComparison.Ordinal));
            var outputDir = Path.Combine(dir, "_site");
            Directory.CreateDirectory(outputDir);
            var markerPath = Path.Combine(outputDir, "old-marker.txt");
            await File.WriteAllTextAsync(markerPath, "preserve");

            var result = await CreateBuilder([new NuglifyAssetMinifier()])
                .BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsFalse();
            await Assert.That(string.Join("; ", result.Errors)).Contains("collides with generated page '/docs/media/review/'");
            await Assert.That(await File.ReadAllTextAsync(markerPath)).IsEqualTo("preserve");
            await Assert.That(Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories)).IsEquivalentTo([markerPath]);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    internal static string CreateSite(
        bool includeDeadLink = false,
        bool fingerprint = true,
        bool linkCheck = true,
        string minifierId = "nuglify")
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-asset-pipeline-it-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "partials"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "static", "css"));

        var siteYaml = new StringBuilder()
            .AppendLine("title: Test Site")
            .AppendLine("baseUrl: http://localhost:5555")
            .AppendLine("theme: default")
            .AppendLine("assets:")
            .AppendLine($"  minifier: {minifierId}")
            .AppendLine("build:")
            .AppendLine($"  fingerprint: {fingerprint.ToString().ToLowerInvariant()}")
            .AppendLine($"  linkCheck: {linkCheck.ToString().ToLowerInvariant()}")
            .AppendLine("collections:")
            .AppendLine("  posts:")
            .AppendLine("    directory: content/posts")
            .AppendLine("    permalink: /blog/:slug/")
            .ToString()
            .ReplaceLineEndings("\n");

        File.WriteAllText(Path.Combine(dir, "site.yaml"), siteYaml);

        var layoutDeadLink = includeDeadLink
            ? $"<a href=\"{DeadLinkPath}\">broken</a>"
            : string.Empty;

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello-world.md"),
            """
            ---
            title: Hello World
            date: 2026-01-01
            ---
            Post content.
            """);

                const string defaultLayout =
                        """
                        <html>
                            <head>
                                <link rel="stylesheet" href="{{ asset_url 'css/site.css' }}">
                            </head>
                            <body>
                                __DEAD_LINK__
                                {{ page.content }}
                            </body>
                        </html>
                        """;
                File.WriteAllText(
                        Path.Combine(dir, "themes", "default", "layouts", "default.html"),
                        defaultLayout.Replace("__DEAD_LINK__", layoutDeadLink, StringComparison.Ordinal));

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "post.html"),
            """
            <html>
              <head>
                <link rel="stylesheet" href="{{ asset_url 'css/site.css' }}">
              </head>
              <body>
                {{ page.content }}
              </body>
            </html>
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "posts-index.html"),
            """
            <html>
              <head>
                <link rel="stylesheet" href="{{ asset_url 'css/site.css' }}">
              </head>
              <body>
                {{ for item in paginator.items }}<a href="{{ item.url }}">{{ item.title }}</a>{{ end }}
              </body>
            </html>
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            """
            <html>
              <head>
                <link rel="stylesheet" href="{{ asset_url 'css/site.css' }}">
              </head>
              <body>Not found.</body>
            </html>
            """);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "static", "css", "site.css"),
            """
            /* Integration test CSS with intentional whitespace for minification checks */
            body {
                margin: 0;
                padding: 0;
                color:   #ffffff;
                background-color: #101010;
            }

            .content {
                max-width: 960px;
                margin: 0 auto;
            }
            """);

        return dir;
    }

    [Test]
    [Arguments("\n", "/media/")]
    [Arguments("\r\n", "/media/")]
    [Arguments("\n", "https://cdn.example.com/v2/media/")]
    [Arguments("\r\n", "https://cdn.example.com/v2/media/")]
    public async Task ReferenceFixture_ConfiguresPrefixAndFeedWithEitherLineEnding(string lineEnding, string prefix)
    {
        var dir = CreateReferenceFixture(prefix, "https://example.org/docs/", lineEnding);
        try
        {
            var config = new SiteConfigLoader().Load(dir);
            await Assert.That(config.AssetPrefix).IsEqualTo(prefix);
            await Assert.That(config.Collections["posts"].Feed).IsTrue();
            await Assert.That(config.BasePath).IsEqualTo("/docs");

            var configPath = Path.Combine(dir, "site.yaml");
            await File.WriteAllTextAsync(configPath, (await File.ReadAllTextAsync(configPath))
                .ReplaceLineEndings(lineEnding)
                .Replace($"assetPrefix: {prefix}{lineEnding}", string.Empty, StringComparison.Ordinal));
            await SetAssetPrefixAsync(dir, prefix, enableSearch: true);
            config = new SiteConfigLoader().Load(dir);
            await Assert.That(config.AssetPrefix).IsEqualTo(prefix);
            await Assert.That(config.Search.Enabled).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CreateReferenceFixture(string assetPrefix, string baseUrl, string lineEnding = "\n")
    {
        var dir = CreateSite();
        var configPath = Path.Combine(dir, "site.yaml");
        File.WriteAllText(configPath, File.ReadAllText(configPath).ReplaceLineEndings(lineEnding));
        var yaml = File.ReadAllText(configPath)
            .ReplaceLineEndings("\n")
            .Replace("baseUrl: http://localhost:5555", $"baseUrl: {baseUrl}", StringComparison.Ordinal)
            .Replace("    permalink: /blog/:slug/", "    permalink: /blog/:slug/\n    feed: true", StringComparison.Ordinal)
            .Replace("build:\n", $"assetPrefix: {assetPrefix}\nbuild:\n", StringComparison.Ordinal);
        File.WriteAllText(configPath, yaml);

        var staticDirectory = Path.Combine(dir, "themes", "default", "static");
        Directory.CreateDirectory(Path.Combine(staticDirectory, "css"));
        Directory.CreateDirectory(Path.Combine(staticDirectory, "fonts"));
        Directory.CreateDirectory(Path.Combine(staticDirectory, "images"));
        File.WriteAllText(Path.Combine(staticDirectory, "css", "site.css"),
            "@import 'base.css' screen; @import url(\"base.css\") print; @import url(base.css) supports(display: grid); .site { color: black; }");
        File.WriteAllText(Path.Combine(staticDirectory, "css", "base.css"),
            ".base { src: url('../fonts/main.woff2?v=2#font'); src2: url(../fonts/main.woff2?v=2#font); }");
        File.WriteAllText(Path.Combine(staticDirectory, "fonts", "main.woff2"), "font bytes");
        File.WriteAllText(Path.Combine(staticDirectory, "images", "picture.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>");
        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello-world.md"),
            """
            ---
            title: Hello World
            date: 2026-01-01
            ---
            <img src="/assets/images/picture.svg" srcset="/assets/images/picture.svg 1x, https://external.example/picture.svg 2x, data:image/svg+xml,%3Csvg%3E 3x">
            <a href="/assets/images/picture.svg?variant=small&amp;mode=detail#preview">image</a>
            <video poster="/assets/images/picture.svg"></video>
            <script>const example = '/assets/images/picture.svg';</script>
            <style>.example { background: url('/assets/images/picture.svg'); }</style>
            """);
        return dir;
    }

    private static async Task SetAssetPrefixAsync(string projectPath, string prefix, bool enableSearch = false)
    {
        var configPath = Path.Combine(projectPath, "site.yaml");
        var yaml = (await File.ReadAllTextAsync(configPath)).ReplaceLineEndings("\n");
        var search = enableSearch ? "search:\n  enabled: true\n" : string.Empty;
        await File.WriteAllTextAsync(
            configPath,
            yaml.Replace("build:\n", $"assetPrefix: {prefix}\n{search}build:\n", StringComparison.Ordinal));
    }

    private static bool FileNameHashMatches(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes))[..8];
        return Path.GetFileNameWithoutExtension(path).EndsWith("." + hash, StringComparison.Ordinal);
    }

    internal static ISiteBuilder CreateBuilder(IEnumerable<IAssetMinifier> minifiers, IImageOptimizer? imageOptimizer = null)
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
            minifiers,
            imageOptimizer ?? new SkiaSharpImageOptimizer(),
            new AssetReferenceIndexBuilder());
    }

    private sealed class WebpTestImageOptimizer : IImageOptimizer
    {
        public bool CanOptimize(string extension) => extension.Equals(".png", StringComparison.OrdinalIgnoreCase);

        public ImageOptimizationOutput Optimize(byte[] sourceBytes, string sourceExtension, ImageOptimizationSettings settings)
        {
            var bytes = new byte[sourceBytes.Length + 4];
            sourceBytes.CopyTo(bytes, 0);
            "webp"u8.CopyTo(bytes.AsSpan(sourceBytes.Length));
            return new ImageOptimizationOutput(bytes, settings.ConvertToWebp ? ".webp" : sourceExtension);
        }
    }

    internal static string GetSingleFingerprintedCssFileName(string cssOutputDir)
    {
        var fileNames = Directory.EnumerateFiles(cssOutputDir, "*.css")
            .Select(Path.GetFileName)
            .Where(static name => name is not null)
            .Cast<string>()
            .Where(static name => FingerprintedCssRegex.IsMatch(name))
            .ToArray();

        if (fileNames.Length != 1)
            throw new InvalidOperationException("Expected exactly one fingerprinted CSS file.");

        return fileNames[0];
    }
}