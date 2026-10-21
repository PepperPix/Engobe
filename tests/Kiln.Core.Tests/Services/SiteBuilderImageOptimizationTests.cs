namespace Kiln.Core.Tests.Services;

using Kiln.Abstractions;
using Kiln.Services;
using SkiaSharp;

public class SiteBuilderImageOptimizationTests
{
    private const int SourceWidth = 800;
    private const int SourceHeight = 600;
    private const int TestMaxWidth = 400;
    private const int TestQuality = 70;
    private static readonly byte[] TestPng = CreateTestPng();

    [Test]
    [Arguments("/assets/")]
    [Arguments("https://cdn.example.com/v2/media/")]
    public async Task BuildAsync_Production_ReferencedSiteStaticImage_IsDownscaledAndChanged(string assetPrefix)
    {
        var dir = CreateSite(assetPrefix: assetPrefix);

        try
        {
            var originalBytes = await File.ReadAllBytesAsync(Path.Combine(dir, "static", "img", "referenced.png"));

            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();

            var outputPath = Path.Combine(dir, "_site", "assets", "img", "referenced.png");
            await Assert.That(File.Exists(outputPath)).IsTrue();

            var outputBytes = await File.ReadAllBytesAsync(outputPath);
            await Assert.That(outputBytes.SequenceEqual(originalBytes)).IsFalse();

            using var decoded = SKBitmap.Decode(outputBytes);
            await Assert.That(decoded.Width).IsEqualTo(TestMaxWidth);
            var html = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello", "index.html"));
            await Assert.That(html).Contains(assetPrefix + "img/referenced.png");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments("/assets/")]
    [Arguments("https://cdn.example.com/v2/media/")]
    public async Task BuildAsync_Production_UnreferencedSiteStaticImage_RemainsByteIdentical(string assetPrefix)
    {
        var dir = CreateSite(assetPrefix: assetPrefix);

        try
        {
            var originalBytes = await File.ReadAllBytesAsync(Path.Combine(dir, "static", "img", "unreferenced.png"));

            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();

            var outputPath = Path.Combine(dir, "_site", "assets", "img", "unreferenced.png");
            var outputBytes = await File.ReadAllBytesAsync(outputPath);
            await Assert.That(outputBytes.SequenceEqual(originalBytes)).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments("/assets/")]
    [Arguments("https://cdn.example.com/v2/media/")]
    public async Task BuildAsync_Production_ThemeStaticImage_RemainsByteIdentical(string assetPrefix)
    {
        var dir = CreateSite(assetPrefix: assetPrefix);

        try
        {
            var originalBytes = await File.ReadAllBytesAsync(Path.Combine(dir, "themes", "default", "static", "img", "theme.png"));

            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();

            var outputPath = Path.Combine(dir, "_site", "assets", "img", "theme.png");
            var outputBytes = await File.ReadAllBytesAsync(outputPath);
            await Assert.That(outputBytes.SequenceEqual(originalBytes)).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments("/assets/")]
    [Arguments("https://cdn.example.com/v2/media/")]
    public async Task BuildAsync_Production_PageBundleImage_IsOptimized(string assetPrefix)
    {
        var dir = CreateSite(assetPrefix: assetPrefix);

        try
        {
            var originalBytes = await File.ReadAllBytesAsync(Path.Combine(dir, "content", "posts", "bundle-post", "photo.png"));

            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();

            var outputPath = Path.Combine(dir, "_site", "assets", "content", "posts", "bundle-post", "photo.png");
            var outputBytes = await File.ReadAllBytesAsync(outputPath);
            await Assert.That(outputBytes.SequenceEqual(originalBytes)).IsFalse();

            using var decoded = SKBitmap.Decode(outputBytes);
            await Assert.That(decoded.Width).IsEqualTo(TestMaxWidth);
            var html = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "bundle-post", "index.html"));
            await Assert.That(html).Contains(assetPrefix + "content/posts/bundle-post/photo.png");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments("/assets/")]
    [Arguments("https://cdn.example.com/v2/media/")]
    public async Task BuildAsync_Production_ImageOptimizationFalseFrontmatter_SkipsOptimizationForThatItem(string assetPrefix)
    {
        var dir = CreateSite(assetPrefix: assetPrefix);

        try
        {
            var originalBytes = await File.ReadAllBytesAsync(Path.Combine(dir, "content", "posts", "opted-out", "opted-out.png"));

            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();

            var outputPath = Path.Combine(dir, "_site", "assets", "content", "posts", "opted-out", "opted-out.png");
            var outputBytes = await File.ReadAllBytesAsync(outputPath);
            await Assert.That(outputBytes.SequenceEqual(originalBytes)).IsTrue();
            var html = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "opted-out", "index.html"));
            await Assert.That(html).Contains(assetPrefix + "content/posts/opted-out/opted-out.png");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments("/assets/")]
    [Arguments("https://cdn.example.com/v2/media/")]
    public async Task BuildAsync_Production_ExcludeGlob_SkipsMatchingImageEvenWhenReferenced(string assetPrefix)
    {
        var dir = CreateSite(exclude: ["static/img/excluded.png"], assetPrefix: assetPrefix);

        try
        {
            var originalBytes = await File.ReadAllBytesAsync(Path.Combine(dir, "static", "img", "excluded.png"));

            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();

            var outputPath = Path.Combine(dir, "_site", "assets", "img", "excluded.png");
            var outputBytes = await File.ReadAllBytesAsync(outputPath);
            await Assert.That(outputBytes.SequenceEqual(originalBytes)).IsTrue();
            var html = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello", "index.html"));
            await Assert.That(html).Contains(assetPrefix + "img/excluded.png");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments("/assets/")]
    [Arguments("https://cdn.example.com/v2/media/")]
    public async Task BuildAsync_Production_SecondBuild_UsesCache_DoesNotReOptimize(string assetPrefix)
    {
        var dir = CreateSite(assetPrefix: assetPrefix);

        try
        {
            var countingOptimizer = new CountingImageOptimizer(new SkiaSharpImageOptimizer());
            var builder = CreateBuilder(countingOptimizer);

            var firstResult = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
            await Assert.That(firstResult.Success).IsTrue();
            var firstCallCount = countingOptimizer.OptimizeCallCount;
            await Assert.That(firstCallCount).IsGreaterThan(0);

            var secondResult = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);
            await Assert.That(secondResult.Success).IsTrue();

            await Assert.That(countingOptimizer.OptimizeCallCount).IsEqualTo(firstCallCount);
            await Assert.That(Directory.Exists(Path.Combine(dir, ".kiln", "image-cache"))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    [Arguments("/assets/")]
    [Arguments("https://cdn.example.com/v2/media/")]
    public async Task BuildAsync_Production_WebpEnabled_ChangesExtensionAndRewritesHtmlReference(string assetPrefix)
    {
        var dir = CreateSite(webp: true, assetPrefix: assetPrefix);

        try
        {
            var builder = CreateBuilder();
            var result = await builder.BuildAsync(dir, false, BuildEnvironment.Production, CancellationToken.None);

            await Assert.That(result.Success).IsTrue();

            var webpOutputPath = Path.Combine(dir, "_site", "assets", "img", "referenced.webp");
            var pngOutputPath = Path.Combine(dir, "_site", "assets", "img", "referenced.png");
            await Assert.That(File.Exists(webpOutputPath)).IsTrue();
            await Assert.That(File.Exists(pngOutputPath)).IsFalse();

            var postHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "hello", "index.html"));
            await Assert.That(postHtml).Contains(assetPrefix + "img/referenced.webp");
            await Assert.That(postHtml).DoesNotContain("/assets/img/referenced.png");
            var bundleOutput = Path.Combine(dir, "_site", "assets", "content", "posts", "bundle-post", "photo.webp");
            using var decodedBundle = SKBitmap.Decode(await File.ReadAllBytesAsync(bundleOutput));
            await Assert.That(decodedBundle.Width).IsEqualTo(TestMaxWidth);
            var bundleHtml = await File.ReadAllTextAsync(Path.Combine(dir, "_site", "blog", "bundle-post", "index.html"));
            await Assert.That(bundleHtml).Contains(assetPrefix + "content/posts/bundle-post/photo.webp");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static string CreateSite(bool webp = false, IReadOnlyList<string>? exclude = null, string assetPrefix = "/assets/")
    {
        var dir = Path.Combine(Path.GetTempPath(), $"kiln-image-opt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts", "bundle-post"));
        Directory.CreateDirectory(Path.Combine(dir, "content", "posts", "opted-out"));
        Directory.CreateDirectory(Path.Combine(dir, "static", "img"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "layouts"));
        Directory.CreateDirectory(Path.Combine(dir, "themes", "default", "static", "img"));

        var excludeYaml = exclude is { Count: > 0 }
            ? "  exclude:\n" + string.Join('\n', exclude.Select(static e => $"    - \"{e}\""))
            : "  exclude: []";

        File.WriteAllText(Path.Combine(dir, "site.yaml"),
            $"""
            title: Test Site
            baseUrl: http://localhost:5555
            assetPrefix: {assetPrefix}
            collections:
              posts:
                directory: content/posts
                permalink: /blog/:slug/
            build:
              fingerprint: false
              linkCheck: false
            images:
              enabled: true
              maxWidth: {TestMaxWidth}
              quality: {TestQuality}
              webp: {webp.ToString().ToLowerInvariant()}
            {excludeYaml}
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "hello.md"),
            """
            ---
            title: Hello
            date: 2026-01-01
            ---
            ![Referenced](/assets/img/referenced.png)

            ![Excluded](/assets/img/excluded.png)
            """);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "bundle-post", "index.md"),
            """
            ---
            title: Bundle Post
            date: 2026-01-01
            ---
            ![Bundle Photo](photo.png)
            """);
        File.WriteAllBytes(Path.Combine(dir, "content", "posts", "bundle-post", "photo.png"), TestPng);

        File.WriteAllText(Path.Combine(dir, "content", "posts", "opted-out", "index.md"),
            """
            ---
            title: Opted Out
            date: 2026-01-01
            imageOptimization: false
            ---
            ![Opted-out Image](opted-out.png)
            """);
        File.WriteAllBytes(Path.Combine(dir, "content", "posts", "opted-out", "opted-out.png"), TestPng);

        File.WriteAllBytes(Path.Combine(dir, "static", "img", "referenced.png"), TestPng);
        File.WriteAllBytes(Path.Combine(dir, "static", "img", "unreferenced.png"), TestPng);
        File.WriteAllBytes(Path.Combine(dir, "static", "img", "excluded.png"), TestPng);
        File.WriteAllBytes(Path.Combine(dir, "themes", "default", "static", "img", "theme.png"), TestPng);

        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "default.html"),
            "<html><body>{{ page.content }}</body></html>");
        File.WriteAllText(Path.Combine(dir, "themes", "default", "layouts", "404.html"),
            "<html><body>Not Found</body></html>");

        return dir;
    }

    private static byte[] CreateTestPng()
    {
        using var bitmap = new SKBitmap(SourceWidth, SourceHeight);
        var pixels = new SKColor[SourceWidth * SourceHeight];
        for (var y = 0; y < SourceHeight; y++)
        {
            for (var x = 0; x < SourceWidth; x++)
            {
                pixels[y * SourceWidth + x] = new SKColor((byte)(x % 256), (byte)(y % 256), (byte)((x + y) % 256));
            }
            bitmap.Pixels = pixels;
        }
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static ISiteBuilder CreateBuilder(IImageOptimizer? optimizer = null)
    {
        var markdownProcessor = new MarkdownProcessor();
        var contentReader = new ContentReader(markdownProcessor);
        var templateRenderer = new TemplateRenderer();
        var permalinkGenerator = new PermalinkGenerator();
        var configLoader = new SiteConfigLoader();
        var pluginLoader = new PluginLoader();
        IAssetMinifier[] assetMinifiers = [new NuglifyAssetMinifier(), new NoOpAssetMinifier()];
        return new SiteBuilder(
            contentReader,
            templateRenderer,
            permalinkGenerator,
            configLoader,
            pluginLoader,
            assetMinifiers,
            optimizer ?? new SkiaSharpImageOptimizer(),
            new AssetReferenceIndexBuilder());
    }

    private sealed class CountingImageOptimizer(IImageOptimizer inner) : IImageOptimizer
    {
        public int OptimizeCallCount { get; private set; }

        public bool CanOptimize(string extension) => inner.CanOptimize(extension);

        public ImageOptimizationOutput Optimize(byte[] sourceBytes, string sourceExtension, ImageOptimizationSettings settings)
        {
            OptimizeCallCount++;
            return inner.Optimize(sourceBytes, sourceExtension, settings);
        }
    }
}
