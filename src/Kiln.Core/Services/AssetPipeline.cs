namespace Kiln.Services;

internal static class AssetPipeline
{
    internal static Task RunAsync(
        AssetPipelineRunContext run,
        CancellationToken cancellationToken) =>
        AssetPipelineFinalizer.RunAsync(run, cancellationToken);

    internal static void ProjectOnly(
        string outputDir,
        AssetUrlResolver resolver,
        Dictionary<string, string> manifest) =>
        AssetPipelineFinalizer.ProjectOnly(outputDir, resolver, manifest);

    internal static string ToWebPath(string outputDir, string fullPath)
    {
        var relative = Path.GetRelativePath(outputDir, fullPath)
            .Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return $"{Path.AltDirectorySeparatorChar}{relative}";
    }
}
