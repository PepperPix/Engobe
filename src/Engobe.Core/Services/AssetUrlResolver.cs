namespace Engobe.Services;

using Engobe.Models;
using Engobe.Abstractions;

internal sealed class AssetUrlResolver
{
    internal const string CanonicalPrefix = "/assets/";
    private const string CanonicalKeyRoot = "assets/";
    private const string AuthoritySeparator = "://";
    private const string UrlSeparator = "/";
    private readonly Uri _baseUrl;
    private readonly string _configuredPrefix;

    internal AssetUrlResolver(SiteConfiguration site, BuildEnvironment environment)
    {
        _baseUrl = site.BaseUrl;
        _configuredPrefix = NormalizePrefix(site.AssetPrefix);
        var configuredPrefix = _configuredPrefix;
        if (environment == BuildEnvironment.Development && IsHttpAbsolute(configuredPrefix))
            configuredPrefix = CanonicalPrefix;

        EffectiveBase = configuredPrefix.StartsWith("/", StringComparison.Ordinal)
            ? JoinBasePath(site.BasePath, configuredPrefix)
            : configuredPrefix;
    }

    private bool HasOwnedOrigin(Uri uri) =>
        IsAbsolute ? MatchesOrigin(uri, EffectiveBase) : MatchesOrigin(uri, Origin);

    private static bool IsInvalidAbsolutePrefix(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return true;
        if (string.IsNullOrEmpty(uri.Host))
            return true;
        if (!string.IsNullOrEmpty(uri.UserInfo))
            return true;
        if (!string.IsNullOrEmpty(uri.Query))
            return true;
        return !string.IsNullOrEmpty(uri.Fragment);
    }

    private static bool IsNonRelativeCssTarget(string value)
    {
        if (value.Length == 0 || value.StartsWith("//", StringComparison.Ordinal))
            return true;
        if (value.StartsWith('#') || value.StartsWith('?'))
            return true;
        return IsHttpAbsolute(value) || HasExplicitScheme(value);
    }

    private static bool IsHttpAbsolute(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool HasExplicitScheme(string value)
    {
        var colon = value.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0)
            return false;
        var slash = value.IndexOf('/', StringComparison.Ordinal);
        return slash < 0 || colon < slash;
    }

    internal string EffectiveBase { get; }

    internal bool IsAbsolute => IsHttpAbsolute(EffectiveBase);

    internal string SiteBasePath => _baseUrl.AbsolutePath.TrimEnd('/');

    internal string Origin => _baseUrl.GetLeftPart(UriPartial.Authority);

    internal static string NormalizePrefix(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        var value = prefix.Trim();
        if (Uri.UnescapeDataString(value).Contains('\\', StringComparison.Ordinal))
            throw InvalidPrefix(prefix);
        if (value.StartsWith("//", StringComparison.Ordinal))
            throw InvalidPrefix(prefix);

        if (value.StartsWith("/", StringComparison.Ordinal))
        {
            if (value == "/")
                throw InvalidPrefix(prefix);
            if (value.Contains('?', StringComparison.Ordinal) || value.Contains('#', StringComparison.Ordinal))
                throw InvalidPrefix(prefix);
            ValidatePath(value, prefix);
            return value.TrimEnd('/') + "/";
        }

        if (value.Contains('?', StringComparison.Ordinal) || value.Contains('#', StringComparison.Ordinal))
            throw InvalidPrefix(prefix);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || IsInvalidAbsolutePrefix(uri))
            throw InvalidPrefix(prefix);

        var authorityStart = value.IndexOf(AuthoritySeparator, StringComparison.Ordinal) + AuthoritySeparator.Length;
        var rawPathStart = value.IndexOf('/', authorityStart);
        if (rawPathStart >= 0)
            ValidatePath(value[rawPathStart..], prefix);
        ValidatePath(uri.AbsolutePath, prefix);
        var path = uri.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
        var authority = uri.GetLeftPart(UriPartial.Authority);
        return string.IsNullOrEmpty(path) ? $"{authority}/" : $"{authority}/{path.Trim('/')}/";
    }

    internal string FormatPublicAddress(string relativeKey, bool absoluteForLocal = false)
    {
        var key = NormalizeKey(relativeKey);
        var escapedKey = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
        var url = EffectiveBase + escapedKey;
        if (!absoluteForLocal || IsAbsolute)
            return url;

        return $"{_baseUrl.GetLeftPart(UriPartial.Authority)}{url}";
    }

    internal bool TryGetKey(string reference, out string key)
    {
        key = string.Empty;
        if (string.IsNullOrWhiteSpace(reference))
            return false;

        var value = reference.Trim();
        if (!TryGetSuffix(value, out var uri, out var path, out var absolute))
            return false;
        if (absolute)
        {
            if (IsHttpAbsolute(_configuredPrefix)
                && MatchesOrigin(uri!, _configuredPrefix)
                && TryStripPrefix(path, EffectivePath(_configuredPrefix), out var configuredRemainder))
                return TryNormalizeRemainder(configuredRemainder, out key);

            return HasOwnedOrigin(uri!)
                && TryStripPrefix(path, EffectivePath(EffectiveBase), out var effectiveRemainder)
                && TryNormalizeRemainder(effectiveRemainder, out key);
        }
        if ((!IsAbsolute && TryStripPrefix(path, EffectivePath(EffectiveBase), out var remainder))
            || TryStripPrefix(path, CanonicalPrefix, out remainder))
            return TryNormalizeRemainder(remainder, out key);
        return false;
    }

    internal string Project(string reference, IReadOnlyDictionary<string, string>? manifest = null, bool absoluteForLocal = false)
    {
        if (!TryGetKey(reference, out var key))
            return reference;

        key = ResolveManifestKey(key, manifest);

        var suffixStart = FindSuffixStart(reference);
        var suffix = suffixStart < 0 ? string.Empty : reference[suffixStart..];
        return FormatPublicAddress(key, absoluteForLocal) + suffix;
    }

    internal string ProjectCss(
        string reference,
        string stylesheetKey,
        IReadOnlyDictionary<string, string>? manifest = null,
        bool absoluteForLocal = false)
    {
        if (TryGetKey(reference, out _))
            return Project(reference, manifest, absoluteForLocal);

        var value = reference.Trim();
        if (IsNonRelativeCssTarget(value))
            return reference;

        var suffixStart = FindSuffixStart(value);
        var path = suffixStart < 0 ? value : value[..suffixStart];
        var suffix = suffixStart < 0 ? string.Empty : value[suffixStart..];
        if (path.StartsWith("/", StringComparison.Ordinal))
            return reference;

        var normalizedStylesheetKey = NormalizeKey(stylesheetKey);
        if (normalizedStylesheetKey.StartsWith(CanonicalKeyRoot, StringComparison.Ordinal))
            normalizedStylesheetKey = normalizedStylesheetKey[CanonicalKeyRoot.Length..];
        var combined = $"{Path.GetDirectoryName(normalizedStylesheetKey)?.Replace('\\', '/')}/{path}";
        var segments = new List<string>();
        foreach (var segment in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var decoded = Uri.UnescapeDataString(segment);
            if (decoded is "." or "")
                continue;
            if (decoded == "..")
            {
                if (segments.Count == 0)
                    return reference;
                segments.RemoveAt(segments.Count - 1);
                continue;
            }
            if (decoded.Contains('/', StringComparison.Ordinal) || decoded.Contains('\\', StringComparison.Ordinal))
                return reference;
            segments.Add(decoded);
        }
        if (segments.Count == 0)
            return reference;

        var key = string.Join('/', segments);
        key = ResolveManifestKey(key, manifest);
        return FormatPublicAddress(key, absoluteForLocal) + suffix;
    }

    internal bool IsOwnedAbsolute(string reference)
    {
        if (!Uri.TryCreate(reference, UriKind.Absolute, out var uri) || !IsAbsolute)
            return false;
        return MatchesOrigin(uri, EffectiveBase)
            && TryStripPrefix(uri.AbsolutePath, EffectivePath(EffectiveBase), out _);
    }

    internal bool TryResolveCssKey(string reference, string stylesheetKey, out string key)
    {
        var projected = ProjectCss(reference, stylesheetKey);
        return TryGetKey(projected, out key);
    }

    internal static string GetPhysicalPath(string outputDir, string key)
    {
        var normalized = NormalizeKey(key);
        return Path.GetFullPath(Path.Combine(outputDir, "assets", normalized.Replace('/', Path.DirectorySeparatorChar)));
    }

    internal static string NormalizeKey(string relativeKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativeKey);
        var decodedKey = Uri.UnescapeDataString(relativeKey.Trim().TrimStart('/').Replace('\\', '/'));
        var segments = decodedKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || Array.Exists(segments, static segment =>
                segment is "." or ".."
                || segment.Contains('\\', StringComparison.Ordinal)
                || segment.Contains(':', StringComparison.Ordinal)))
            throw new ArgumentException("Asset key must remain within the assets namespace.", nameof(relativeKey));
        return string.Join('/', segments);
    }

    private static string JoinBasePath(string basePath, string prefix)
    {
        if (string.IsNullOrEmpty(basePath))
            return prefix;
        var normalizedBase = basePath.TrimEnd('/');
        if (prefix.Equals(normalizedBase + "/", StringComparison.Ordinal)
            || prefix.StartsWith(normalizedBase + "/", StringComparison.Ordinal))
            return prefix;
        return normalizedBase + prefix;
    }

    private static string EffectivePath(string baseValue)
    {
        if (Uri.TryCreate(baseValue, UriKind.Absolute, out var uri))
            return uri.AbsolutePath;
        return baseValue;
    }

    private static bool TryGetSuffix(string value, out Uri? uri, out string path, out bool absolute)
    {
        uri = null;
        path = string.Empty;
        absolute = !value.StartsWith("/", StringComparison.Ordinal)
            && Uri.TryCreate(value, UriKind.Absolute, out uri);
        if (absolute)
        {
            path = uri!.AbsolutePath;
            return true;
        }
        if (value.StartsWith("//", StringComparison.Ordinal))
            return false;

        var query = value.IndexOfAny(['?', '#']);
        path = query < 0 ? value : value[..query];
        return path.StartsWith("/", StringComparison.Ordinal);
    }

    private static bool MatchesOrigin(Uri uri, string baseValue)
    {
        if (!Uri.TryCreate(baseValue, UriKind.Absolute, out var baseUri))
            return false;
        return uri.Scheme.Equals(baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase)
            && uri.Port == baseUri.Port;
    }

    private static bool TryStripPrefix(string path, string prefix, out string remainder)
    {
        remainder = string.Empty;
        if (string.IsNullOrEmpty(prefix))
            return false;
        var normalizedPrefix = prefix.StartsWith(UrlSeparator, StringComparison.Ordinal) ? prefix : UrlSeparator + prefix;
        normalizedPrefix = normalizedPrefix.TrimEnd('/') + UrlSeparator;
        var normalizedPath = path.StartsWith(UrlSeparator, StringComparison.Ordinal) ? path : UrlSeparator + path;
        if (!normalizedPath.StartsWith(normalizedPrefix, StringComparison.Ordinal))
            return false;
        remainder = normalizedPath[normalizedPrefix.Length..];
        return true;
    }

    private static bool TryNormalizeRemainder(string remainder, out string key)
    {
        key = string.Empty;
        try
        {
            key = NormalizeKey(remainder);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static string ResolveManifestKey(string key, IReadOnlyDictionary<string, string>? manifest)
    {
        if (manifest is null)
            return key;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (visited.Add(key) && manifest.TryGetValue(CanonicalPrefix + key, out var renamed)
               && renamed.StartsWith(CanonicalPrefix, StringComparison.Ordinal))
            key = NormalizeKey(renamed[CanonicalPrefix.Length..]);
        return key;
    }

    private static void ValidatePath(string path, string original)
    {
        var decodedPath = Uri.UnescapeDataString(path);
        foreach (var segment in decodedPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
                throw InvalidPrefix(original);
        }
    }

    private static InvalidOperationException InvalidPrefix(string value) =>
        new($"Invalid assetPrefix '{value}'. Use a root-relative path other than '/' or an absolute HTTP(S) URL without credentials, query, fragment, backslashes, or traversal segments.");

    private static int FindSuffixStart(string value)
    {
        var query = value.IndexOfAny(['?', '#']);
        return query < 0 ? -1 : query;
    }
}
