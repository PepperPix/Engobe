namespace Kiln.Services;

using System.Security.Cryptography;
using System.Text.Json;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

internal static class NuGetPluginPackageReader
{
    public static async Task<IReadOnlyList<NuGetVersion>?> GetVersionsAsync(SourceRepository repository, string packageId, CancellationToken ct)
    {
        if (!repository.PackageSource.IsHttp)
            return null;
        var http = await repository.GetResourceAsync<HttpSourceResource>(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The NuGet source does not provide HTTP support.");
        var baseAddress = await GetPackageBaseAddressAsync(http.HttpSource, repository.PackageSource.Source, ct).ConfigureAwait(false);
        if (baseAddress is null)
            return null;
        var id = packageId.ToLowerInvariant();
        var uri = new Uri(baseAddress, $"{id}/index.json");
        return await ReadWithOfflineCacheAsync<IReadOnlyList<NuGetVersion>>(
            http.HttpSource, uri.AbsoluteUri, $"kiln_versions_{id}",
            async result =>
            {
                if (result.Status == HttpSourceResultStatus.NotFound)
                    return [];
                if (result.Stream is null)
                    throw new InvalidOperationException($"Source '{uri}' returned an empty package version response.");
                using var document = await JsonDocument.ParseAsync(result.Stream, cancellationToken: ct).ConfigureAwait(false);
                return document.RootElement.GetProperty("versions").EnumerateArray()
                    .Select(item => NuGetVersion.TryParse(item.GetString(), out var version)
                        ? version
                        : throw new InvalidOperationException($"Source '{uri}' returned an invalid package version.")).ToArray();
            },
            ct).ConfigureAwait(false);
    }

    public static async Task<string?> DownloadAndVerifyAsync(
        SourceRepository repository,
        string packageId,
        NuGetVersion version,
        Stream packageStream,
        CancellationToken ct)
    {
        var source = repository.PackageSource.Source;
        var identity = $"{packageId} {version.ToNormalizedString()}";
        var warning = $"Source '{source}' provides no SHA-512 hash for package '{identity}'; installation proceeded without package hash verification.";

        string? expectedHash;
        try
        {
            if (!repository.PackageSource.IsHttp)
            {
                // Local sources are reachable content, not the HTTP offline cache.
                await CopyFromSourceAsync(repository, packageId, version, packageStream, ct).ConfigureAwait(false);
                return warning;
            }

            var http = await repository.GetResourceAsync<HttpSourceResource>(ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Source '{source}' does not provide HTTP support.");
            var baseAddress = await GetPackageBaseAddressAsync(http.HttpSource, source, ct).ConfigureAwait(false);
            if (baseAddress is null)
            {
                await CopyFromSourceAsync(repository, packageId, version, packageStream, ct).ConfigureAwait(false);
                return warning;
            }
            var id = packageId.ToLowerInvariant();
            var normalizedVersion = version.ToNormalizedString().ToLowerInvariant();
            var packageUri = new Uri(baseAddress, $"{id}/{normalizedVersion}/{id}.{normalizedVersion}.nupkg");
            // Package versions are immutable. An older cached sidecar is also usable offline.
            using var cache = new SourceCacheContext();
            cache.MaxAge = DateTimeOffset.MinValue;
            var packageRequest = new HttpSourceCachedRequest(
                packageUri.AbsoluteUri,
                $"kiln_nupkg_{id}.{normalizedVersion}",
                HttpSourceCacheContext.Create(cache, 0))
            {
                IgnoreNotFounds = true,
                MaxTries = 1,
            };
            var found = await http.HttpSource.GetAsync(
                packageRequest,
                async result =>
                {
                    if (result.Status == HttpSourceResultStatus.NotFound || result.Stream is null)
                        return false;
                    await result.Stream.CopyToAsync(packageStream, ct).ConfigureAwait(false);
                    return true;
                },
                NullLogger.Instance,
                ct).ConfigureAwait(false);
            if (!found || packageStream.Length == 0)
                throw new InvalidOperationException($"Package '{identity}' was not found on source '{source}'.");

            var request = new HttpSourceCachedRequest(
                $"{packageUri.AbsoluteUri}.sha512",
                $"kiln_sha512_{id}.{normalizedVersion}",
                HttpSourceCacheContext.Create(cache, 0))
            {
                IgnoreNotFounds = true,
                MaxTries = 1,
            };
            expectedHash = await http.HttpSource.GetAsync(
                request,
                async result =>
                {
                    if (result.Status == HttpSourceResultStatus.NotFound)
                        return null;
                    using var reader = new StreamReader(result.Stream
                        ?? throw new InvalidOperationException($"Source '{source}' returned an empty SHA-512 response for package '{identity}'."));
                    return await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                },
                NullLogger.Instance,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or FatalProtocolException)
        {
            throw new PluginPackageVerificationException(
                $"Package '{identity}' from source '{source}' cannot be verified offline: matching cached package bytes and a SHA-512 hash are required.",
                ex);
        }

        if (expectedHash is null)
            return warning;

        byte[] expected;
        try
        {
            expected = Convert.FromBase64String(expectedHash.Trim());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Invalid SHA-512 hash for package '{identity}' from source '{source}'.", ex);
        }

        packageStream.Position = 0;
        var actual = await SHA512.HashDataAsync(packageStream, ct).ConfigureAwait(false);
        packageStream.Position = 0;
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            throw new InvalidOperationException($"SHA-512 hash mismatch for package '{identity}' from source '{source}'.");

        return null;
    }

    private static Task<Uri?> GetPackageBaseAddressAsync(HttpSource http, string source, CancellationToken ct)
    {
        return ReadWithOfflineCacheAsync<Uri?>(
            http, source, "kiln_service_index",
            async result =>
            {
                if (result.Stream is null)
                    throw new InvalidOperationException($"Source '{source}' returned an empty NuGet service index.");
                using var document = await JsonDocument.ParseAsync(result.Stream, cancellationToken: ct).ConfigureAwait(false);
                if (!document.RootElement.TryGetProperty("resources", out var resources) || resources.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException($"Source '{source}' returned a NuGet service index without resources.");
                foreach (var resource in resources.EnumerateArray())
                {
                    if (!resource.TryGetProperty("@type", out var type) || !IsPackageBaseAddress(type))
                        continue;
                    if (!resource.TryGetProperty("@id", out var address)
                        || !Uri.TryCreate(address.GetString(), UriKind.Absolute, out var uri))
                        throw new InvalidOperationException($"Source '{source}' returned an invalid PackageBaseAddress.");
                    return uri;
                }
                return null;
            },
            ct);
    }

    private static async Task<T> ReadWithOfflineCacheAsync<T>(
        HttpSource http, string uri, string cacheKey, Func<HttpSourceResult, Task<T>> read, CancellationToken ct)
    {
        using var cache = new SourceCacheContext();
        try
        {
            return await ReadAsync(cache).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or FatalProtocolException)
        {
            // NuGet's service-index provider does not expose a context for stale offline reads.
            using var offlineCache = new SourceCacheContext();
            offlineCache.MaxAge = DateTimeOffset.MinValue;
            return await ReadAsync(offlineCache).ConfigureAwait(false);
        }

        Task<T> ReadAsync(SourceCacheContext context)
        {
            var request = new HttpSourceCachedRequest(uri, cacheKey, HttpSourceCacheContext.Create(context, 0))
            {
                IgnoreNotFounds = true,
                MaxTries = 1,
            };
            return http.GetAsync(request, read, NullLogger.Instance, ct);
        }
    }

    private static bool IsPackageBaseAddress(JsonElement type) => type.ValueKind switch
    {
        JsonValueKind.String => ServiceTypes.PackageBaseAddress.Contains(type.GetString(), StringComparer.Ordinal),
        JsonValueKind.Array => type.EnumerateArray().Any(IsPackageBaseAddress),
        _ => false,
    };

    private static async Task CopyFromSourceAsync(
        SourceRepository repository, string packageId, NuGetVersion version, Stream packageStream, CancellationToken ct)
    {
        var finder = await repository.GetResourceAsync<FindPackageByIdResource>(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The NuGet repository does not provide package lookup support.");
        using var cache = new SourceCacheContext();
        cache.MaxAge = DateTimeOffset.MinValue;
        var found = await finder.CopyNupkgToStreamAsync(
            packageId, version, packageStream, cache, NullLogger.Instance, ct).ConfigureAwait(false);
        if (!found || packageStream.Length == 0)
            throw new InvalidOperationException($"Package '{packageId}' {nameof(version)} '{version.ToNormalizedString()}' was not found on the configured NuGet source.");
    }
}
