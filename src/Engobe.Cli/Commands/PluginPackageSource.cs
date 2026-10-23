namespace Engobe.Cli.Commands;

using Engobe.Services;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

internal sealed record PluginPackageSource(INuGetPluginClient Client, string LockValue)
{
    public static PluginPackageSource Resolve(
        string? source, string projectPath, INuGetPluginClient defaultClient, bool fromLock = false)
    {
        if ((!fromLock && source is null) || (fromLock && source == "nuget"))
            return new PluginPackageSource(defaultClient, "nuget");

        if (string.IsNullOrWhiteSpace(source))
            throw new InvalidOperationException($"Plugin package {nameof(source)} cannot be empty.");

        string resolved;
        string lockValue;
        if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            if (uri.Scheme is not ("https" or "http")
                || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidOperationException($"Use an HTTP(S) NuGet v3 {nameof(source)} URL without credentials, query parameters or fragments.");

            resolved = uri.AbsoluteUri;
            lockValue = resolved;
        }
        else
        {
            var path = uri?.IsFile == true ? uri.LocalPath : source;
            resolved = Path.GetFullPath(path, fromLock ? projectPath : Environment.CurrentDirectory);
            if (!Directory.Exists(resolved))
                throw new InvalidOperationException($"The selected local plugin package {nameof(source)} directory does not exist.");
            lockValue = Path.GetRelativePath(projectPath, resolved).Replace('\\', '/');
            if (lockValue == "nuget")
                lockValue = "./nuget";
        }

        var settings = Settings.LoadDefaultSettings(projectPath);
        var configuredSource = new PackageSourceProvider(settings).LoadPackageSources()
            .FirstOrDefault(candidate => string.Equals(candidate.Source, resolved, StringComparison.OrdinalIgnoreCase));
        var packageSource = configuredSource ?? new PackageSource(resolved);
        return new PluginPackageSource(
            new NuGetPluginClient(Repository.Factory.GetCoreV3(packageSource)), lockValue);
    }
}
