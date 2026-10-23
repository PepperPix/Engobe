namespace Engobe.Services;

using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using AngleSharp.Html.Parser;
using Engobe.Abstractions;
using Engobe.Models;

internal static class AssetPipelineFinalizer
{
    private const int HashLength = 8;
    private const int ActiveVisit = 1;
    private const int CompleteVisit = 2;
    private static readonly int CanonicalPrefixLength = AssetUrlResolver.CanonicalPrefix.Length;
    private static readonly string[] FingerprintExtensions =
        [".css", ".js", ".svg", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".woff", ".woff2", ".ttf", ".otf"];

    internal static async Task RunAsync(AssetPipelineRunContext run, CancellationToken cancellationToken)
    {
        var outputDir = run.OutputDir;
        var resolver = run.Resolver;
        var manifest = run.Manifest;
        var options = run.Options;
        var minifier = run.Minifier;
        var warnings = run.Warnings;
        var errors = run.Errors;
        var assetsDirectory = Path.Combine(outputDir, "assets");
        if (Directory.Exists(assetsDirectory))
        {
            await MinifyAssetsAsync(assetsDirectory, options, minifier, warnings, cancellationToken).ConfigureAwait(false);
            if (options.Fingerprint && FindCycle(assetsDirectory, resolver) is { } cycle)
            {
                errors.Add($"CSS fingerprint dependency cycle: {cycle}");
                return;
            }

            if (options.Fingerprint)
                FingerprintNonCssAssets(assetsDirectory, manifest);
            await FinalizeCssAsync(assetsDirectory, resolver, manifest, options.Fingerprint, cancellationToken).ConfigureAwait(false);
        }

        RewriteOutputReferences(outputDir, resolver, manifest, absoluteFeedAssets: true);

        if (options.MinifyHtml && minifier.CanMinify(AssetType.Html))
            await MinifyHtmlAsync(outputDir, options, minifier, warnings, cancellationToken).ConfigureAwait(false);

        if (options.LinkCheck)
            await RunLinkCheckAsync(run, cancellationToken).ConfigureAwait(false);
    }

    internal static void ProjectOnly(
        string outputDir,
        AssetUrlResolver resolver,
        Dictionary<string, string> manifest)
    {
        var assetsDirectory = Path.Combine(outputDir, "assets");
        if (Directory.Exists(assetsDirectory))
        {
            foreach (var cssFile in Directory.EnumerateFiles(assetsDirectory, "*.css", SearchOption.AllDirectories))
            {
                var key = GetAssetKey(assetsDirectory, cssFile);
                var content = File.ReadAllText(cssFile);
                var rewritten = CssAssetReferenceRewriter.Rewrite(content, key, resolver, manifest);
                if (!string.Equals(content, rewritten, StringComparison.Ordinal))
                    File.WriteAllText(cssFile, rewritten, new UTF8Encoding(false));
            }
        }

        RewriteOutputReferences(outputDir, resolver, manifest, absoluteFeedAssets: true);
    }

    private static async Task MinifyAssetsAsync(
        string assetsDirectory,
        BuildOptions options,
        IAssetMinifier minifier,
        Collection<string> warnings,
        CancellationToken cancellationToken)
    {
        foreach (var file in Directory.EnumerateFiles(assetsDirectory, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var type = Path.GetExtension(file).ToUpperInvariant() switch
            {
                ".CSS" when options.MinifyCss => AssetType.Css,
                ".JS" when options.MinifyJs => AssetType.Js,
                ".SVG" when options.MinifySvg => AssetType.Svg,
                _ => (AssetType?)null
            };
            if (type is null || !minifier.CanMinify(type.Value))
                continue;

            await MinifyFileAsync(file, type.Value, minifier, warnings, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task MinifyHtmlAsync(
        string outputDir,
        BuildOptions options,
        IAssetMinifier minifier,
        Collection<string> warnings,
        CancellationToken cancellationToken)
    {
        var htmlMinifier = options.HtmlAggressive && minifier is NuglifyAssetMinifier
            ? new NuglifyAssetMinifier(htmlAggressive: true)
            : minifier;
        foreach (var file in Directory.EnumerateFiles(outputDir, "*.html", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await MinifyFileAsync(file, AssetType.Html, htmlMinifier, warnings, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task MinifyFileAsync(
        string file,
        AssetType type,
        IAssetMinifier minifier,
        Collection<string> warnings,
        CancellationToken cancellationToken)
    {
        var original = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
        try
        {
            var minified = minifier.Minify(original, type);
            if (!string.IsNullOrEmpty(minified) && !string.Equals(original, minified, StringComparison.Ordinal))
                await File.WriteAllTextAsync(file, minified, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            warnings.Add($"Minification failed for '{file}': {exception.Message} — using original.");
        }
    }

    private static string? FindCycle(string assetsDirectory, AssetUrlResolver resolver)
    {
        var cssFiles = Directory.EnumerateFiles(assetsDirectory, "*.css", SearchOption.AllDirectories)
            .ToDictionary(file => GetAssetKey(assetsDirectory, file), StringComparer.Ordinal);
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var chain = new List<string>();

        foreach (var key in cssFiles.Keys)
        {
            var cycle = Visit(key);
            if (cycle is not null)
                return cycle;
        }
        return null;

        string? Visit(string key)
        {
            if (state.TryGetValue(key, out var currentState))
            {
                if (currentState == ActiveVisit)
                {
                    var start = chain.FindIndex(path => path.Equals(key, StringComparison.Ordinal));
                    return string.Join(" -> ", chain.Skip(Math.Max(0, start)).Append(key).Select(static path => path[CanonicalPrefixLength..]));
                }
                if (currentState == CompleteVisit)
                    return null;
            }

            state[key] = ActiveVisit;
            chain.Add(key);
            var css = File.ReadAllText(Path.Combine(assetsDirectory, key[CanonicalPrefixLength..].Replace('/', Path.DirectorySeparatorChar)));
            foreach (var reference in CssAssetReferenceRewriter.Extract(css))
            {
                if (!resolver.TryResolveCssKey(reference, key, out var target) || !cssFiles.ContainsKey("/assets/" + target))
                    continue;
                var cycle = Visit("/assets/" + target);
                if (cycle is not null)
                    return cycle;
            }
            chain.RemoveAt(chain.Count - 1);
            state[key] = CompleteVisit;
            return null;
        }
    }

    private static async Task FinalizeCssAsync(
        string assetsDirectory,
        AssetUrlResolver resolver,
        Dictionary<string, string> manifest,
        bool fingerprint,
        CancellationToken cancellationToken)
    {
        var cssFiles = Directory.EnumerateFiles(assetsDirectory, "*.css", SearchOption.AllDirectories)
            .ToDictionary(file => GetAssetKey(assetsDirectory, file), StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in cssFiles.Keys)
            await FinalizeCssFileAsync(key).ConfigureAwait(false);

        async Task FinalizeCssFileAsync(string key)
        {
            if (!visited.Add(key))
                return;
            cancellationToken.ThrowIfCancellationRequested();
            var file = cssFiles[key];
            var content = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            foreach (var reference in CssAssetReferenceRewriter.Extract(content))
            {
                if (!resolver.TryResolveCssKey(reference, key, out var dependency))
                    continue;
                var dependencyKey = "/assets/" + dependency;
                if (cssFiles.ContainsKey(dependencyKey))
                    await FinalizeCssFileAsync(dependencyKey).ConfigureAwait(false);
            }

            content = CssAssetReferenceRewriter.Rewrite(content, key, resolver, manifest);
            await File.WriteAllTextAsync(file, content, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            if (!fingerprint)
                return;

            var hash = Hash8(await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false));
            var target = AddHashToFileName(file, hash);
            if (File.Exists(target))
                File.Delete(target);
            File.Move(file, target);
            manifest[key] = BuildManifestTarget(assetsDirectory, target);
            cssFiles[key] = target;
        }
    }

    private static void FingerprintNonCssAssets(string assetsDirectory, Dictionary<string, string> manifest)
    {
        foreach (var file in Directory.EnumerateFiles(assetsDirectory, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetExtension(file).Equals(".css", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static file => file, StringComparer.OrdinalIgnoreCase))
        {
            if (!FingerprintExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                continue;
            var key = GetAssetKey(assetsDirectory, file);
            var target = AddHashToFileName(file, Hash8(File.ReadAllBytes(file)));
            if (File.Exists(target))
                File.Delete(target);
            File.Move(file, target);
            manifest[key] = BuildManifestTarget(assetsDirectory, target);
        }
    }

    private static void RewriteOutputReferences(
        string outputDir,
        AssetUrlResolver resolver,
        Dictionary<string, string> manifest,
        bool absoluteFeedAssets)
    {
        foreach (var htmlFile in Directory.EnumerateFiles(outputDir, "*.html", SearchOption.AllDirectories))
        {
            var html = File.ReadAllText(htmlFile);
            var rewritten = HtmlAssetReferenceRewriter.RewriteDocument(html, resolver, manifest);
            if (!string.Equals(html, rewritten, StringComparison.Ordinal))
                File.WriteAllText(htmlFile, rewritten, new UTF8Encoding(false));
        }

        foreach (var xmlFile in Directory.EnumerateFiles(outputDir, "*.xml", SearchOption.AllDirectories))
            RewriteAtomContent(xmlFile, resolver, manifest, absoluteFeedAssets);
    }

    private static void RewriteAtomContent(
        string xmlFile,
        AssetUrlResolver resolver,
        Dictionary<string, string> manifest,
        bool absoluteFeedAssets)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(xmlFile, LoadOptions.PreserveWhitespace);
        }
        catch (System.Xml.XmlException)
        {
            return;
        }

        var changed = false;
        foreach (var element in document.Descendants().Where(static element =>
                     element.Name.LocalName == "content"
                     && string.Equals((string?)element.Attribute("type"), "html", StringComparison.OrdinalIgnoreCase)))
        {
            var original = element.Value;
            var rewritten = HtmlAssetReferenceRewriter.RewriteFragment(original, resolver, manifest, absoluteFeedAssets);
            if (string.Equals(original, rewritten, StringComparison.Ordinal))
                continue;
            element.Value = rewritten;
            changed = true;
        }
        if (changed)
            document.Save(xmlFile, SaveOptions.DisableFormatting);
    }

    private static async Task RunLinkCheckAsync(
        AssetPipelineRunContext run,
        CancellationToken cancellationToken)
    {
        var outputDir = run.OutputDir;
        var resolver = run.Resolver;
        var knownPaths = Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories)
            .Select(file => ToWebPath(outputDir, file))
            .ToHashSet(StringComparer.Ordinal);
        var dead = new List<(string Source, string Reference)>();
        var deferred = new SortedSet<string>(StringComparer.Ordinal);
        var parser = new HtmlParser();

        foreach (var htmlFile in Directory.EnumerateFiles(outputDir, "*.html", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = await parser.ParseDocumentAsync(await File.ReadAllTextAsync(htmlFile, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
            foreach (var element in document.All)
            {
                foreach (var attribute in new[] { "href", "src", "poster" })
                {
                    if (element.GetAttribute(attribute) is { } reference)
                        Check(reference, ToWebPath(outputDir, htmlFile));
                }
                if (element.GetAttribute("srcset") is { } srcset)
                    foreach (var reference in ExtractSrcsetUrls(srcset))
                        Check(reference, ToWebPath(outputDir, htmlFile));
            }
        }

        var assetsDirectory = Path.Combine(outputDir, "assets");
        if (Directory.Exists(assetsDirectory))
        {
            foreach (var cssFile in Directory.EnumerateFiles(assetsDirectory, "*.css", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceKey = "/assets/" + Path.GetRelativePath(assetsDirectory, cssFile).Replace(Path.DirectorySeparatorChar, '/');
                foreach (var reference in CssAssetReferenceRewriter.Extract(await File.ReadAllTextAsync(cssFile, cancellationToken).ConfigureAwait(false)))
                {
                    if (resolver.TryResolveCssKey(reference, sourceKey, out var key))
                    {
                        Check(reference, ToWebPath(outputDir, cssFile), "/assets/" + key);
                        continue;
                    }
                    Check(reference, ToWebPath(outputDir, cssFile));
                }
            }
        }

        foreach (var xmlFile in Directory.EnumerateFiles(outputDir, "*.xml", SearchOption.AllDirectories))
        {
            XDocument document;
            try
            {
                document = XDocument.Load(xmlFile);
            }
            catch (System.Xml.XmlException)
            {
                continue;
            }
            foreach (var content in document.Descendants().Where(static element =>
                         element.Name.LocalName == "content"
                         && string.Equals((string?)element.Attribute("type"), "html", StringComparison.OrdinalIgnoreCase)))
            {
                var html = await parser.ParseDocumentAsync(content.Value, cancellationToken).ConfigureAwait(false);
                foreach (var element in html.All)
                {
                    foreach (var attribute in new[] { "href", "src", "poster" })
                        if (element.GetAttribute(attribute) is { } reference)
                            Check(reference, ToWebPath(outputDir, xmlFile));
                    if (element.GetAttribute("srcset") is { } srcset)
                        foreach (var reference in ExtractSrcsetUrls(srcset))
                            Check(reference, ToWebPath(outputDir, xmlFile));
                }
            }
        }

        foreach (var (source, reference) in dead)
            run.Errors.Add($"Dead link in '{source}': '{reference}'");
        if (deferred.Count > 0)
            run.Warnings.Add($"Pagefind entrypoint existence checks deferred until search indexing: {string.Join(", ", deferred)}. Run search indexing before publishing.");

        void Check(string reference, string source, string? localReference = null)
        {
            if (TryMapLocalReference(localReference ?? reference, resolver, out var resolved) && !TargetExists(resolved, knownPaths))
            {
                if (run.SearchEnabled && IsDeferredPagefindEntrypoint(reference, resolved, resolver))
                {
                    deferred.Add(resolved);
                    return;
                }
                dead.Add((source, reference));
            }
        }
    }

    private static bool IsDeferredPagefindEntrypoint(string reference, string resolved, AssetUrlResolver resolver)
    {
        if (resolved is not ("/pagefind/pagefind.js" or "/pagefind/pagefind-ui.css"))
            return false;

        var value = reference.Trim();
        if (!value.StartsWith("/", StringComparison.Ordinal) && Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (!uri.GetLeftPart(UriPartial.Authority).Equals(resolver.Origin, StringComparison.OrdinalIgnoreCase))
                return false;
            value = uri.AbsolutePath;
        }
        var suffix = value.IndexOfAny(['?', '#']);
        if (suffix >= 0)
            value = value[..suffix];
        return value.Equals(resolver.SiteBasePath + resolved, StringComparison.Ordinal);
    }

    private static bool TryMapLocalReference(string reference, AssetUrlResolver resolver, out string resolvedPath)
    {
        if (resolver.TryGetKey(reference, out var assetKey))
        {
            resolvedPath = "/assets/" + assetKey;
            return true;
        }

        var value = reference.Trim();
        if (IsNonCheckableReference(value))
        {
            resolvedPath = string.Empty;
            return false;
        }

        if (!value.StartsWith("/", StringComparison.Ordinal)
            && Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            if (!absolute.GetLeftPart(UriPartial.Authority).Equals(resolver.Origin, StringComparison.OrdinalIgnoreCase))
            {
                resolvedPath = string.Empty;
                return false;
            }
            value = absolute.AbsolutePath;
        }

        if (!value.StartsWith("/", StringComparison.Ordinal))
        {
            resolvedPath = string.Empty;
            return false;
        }
        var suffix = value.IndexOfAny(['?', '#']);
        if (suffix >= 0)
            value = value[..suffix];
        if (!string.IsNullOrEmpty(resolver.SiteBasePath)
            && (value.Equals(resolver.SiteBasePath, StringComparison.Ordinal)
                || value.StartsWith(resolver.SiteBasePath + "/", StringComparison.Ordinal)))
            value = value[resolver.SiteBasePath.Length..];
        resolvedPath = Uri.UnescapeDataString(value);
        return true;
    }

    private static bool IsNonCheckableReference(string value)
    {
        if (value.StartsWith("//", StringComparison.Ordinal) || value.StartsWith('#') || value.StartsWith('?'))
            return true;
        foreach (var scheme in new[] { "data:", "mailto:", "tel:" })
            if (value.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static bool TargetExists(string path, HashSet<string> knownPaths)
    {
        if (knownPaths.Contains(path))
            return true;
        if (knownPaths.Contains(path.TrimEnd('/') + "/index.html"))
            return true;
        return !Path.HasExtension(path) && knownPaths.Contains(path.TrimEnd('/') + "/index.html");
    }

    private static IEnumerable<string> ExtractSrcsetUrls(string srcset)
    {
        var index = 0;
        while (index < srcset.Length)
        {
            while (index < srcset.Length && (char.IsWhiteSpace(srcset[index]) || srcset[index] == ','))
                index++;
            var start = index;
            while (index < srcset.Length && !char.IsWhiteSpace(srcset[index]))
                index++;
            var url = srcset[start..index].TrimEnd(',');
            if (url.Length > 0)
                yield return url;
            while (index < srcset.Length && srcset[index] != ',')
                index++;
            if (index < srcset.Length)
                index++;
        }
    }

    private static string GetAssetKey(string assetsDirectory, string file) =>
        "/assets/" + Path.GetRelativePath(assetsDirectory, file).Replace(Path.DirectorySeparatorChar, '/');

    private static string BuildManifestTarget(string assetsDirectory, string file) => GetAssetKey(assetsDirectory, file);

    private static string AddHashToFileName(string file, string hash)
    {
        var extension = Path.GetExtension(file);
        return Path.Combine(Path.GetDirectoryName(file)!, $"{Path.GetFileNameWithoutExtension(file)}.{hash}{extension}");
    }

    private static string Hash8(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes))[..HashLength];

    private static string ToWebPath(string outputDir, string file) =>
        "/" + Path.GetRelativePath(outputDir, file).Replace(Path.DirectorySeparatorChar, '/');
}
