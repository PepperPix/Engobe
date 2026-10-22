namespace Kiln.Services;

internal sealed class PageBundleAssetCatalog
{
    private readonly IReadOnlyList<string> _relativePaths;
    private readonly HashSet<string> _relativePathSet;

    public PageBundleAssetCatalog(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);

        if (!Directory.Exists(RootDirectory))
            throw new DirectoryNotFoundException($"Page bundle directory not found: {RootDirectory}");

        var paths = new List<string>();
        AddDirectoryFiles(RootDirectory, RootDirectory, paths);
        _relativePaths = Array.AsReadOnly([.. paths]);
        _relativePathSet = new HashSet<string>(
            paths,
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal);
    }

    public string RootDirectory { get; }

    public IReadOnlyList<string> RelativePaths => _relativePaths;

    public bool ContainsAsset(string relativePath)
    {
        return TryGetFullPath(relativePath, out _) && _relativePathSet.Contains(NormalizeSeparators(relativePath));
    }

    public bool IsExcludedMarkdownTarget(string relativePath)
    {
        if (!Path.GetExtension(relativePath).Equals(".md", StringComparison.OrdinalIgnoreCase)
            || !TryGetFullPath(relativePath, out var fullPath)
            || IsHiddenOrSymbolicLink(relativePath))
        {
            return false;
        }

        return File.Exists(fullPath);
    }

    public bool IsHiddenOrSymbolicLink(string relativePath)
    {
        var pathSegments = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (Array.Exists(pathSegments, static segment => segment.StartsWith(".", StringComparison.Ordinal)))
        {
            return true;
        }

        if (!TryGetFullPath(relativePath, out var fullPath))
            return true;

        var currentPath = RootDirectory;
        var relativeSegments = Path.GetRelativePath(RootDirectory, fullPath)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in relativeSegments)
        {
            currentPath = Path.Combine(currentPath, segment);
            if (new FileInfo(currentPath).LinkTarget is not null || new DirectoryInfo(currentPath).LinkTarget is not null)
                return true;

            if (!File.Exists(currentPath) && !Directory.Exists(currentPath))
                return false;

            if ((File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
                return true;
        }

        return false;
    }

    private bool TryGetFullPath(string relativePath, out string fullPath)
    {
        fullPath = Path.GetFullPath(Path.Combine(RootDirectory, NormalizeSeparators(relativePath)));
        var rootPrefix = RootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootPrefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            && !string.Equals(fullPath, RootDirectory, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static string NormalizeSeparators(string relativePath) =>
        relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

    private static void AddDirectoryFiles(string rootDirectory, string currentDirectory, List<string> paths)
    {
        foreach (var entryPath in Directory.EnumerateFileSystemEntries(currentDirectory))
        {
            var name = Path.GetFileName(entryPath);
            if (name.StartsWith(".", StringComparison.Ordinal))
                continue;

            var attributes = File.GetAttributes(entryPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                continue;

            if ((attributes & FileAttributes.Directory) != 0)
            {
                AddDirectoryFiles(rootDirectory, entryPath, paths);
                continue;
            }

            if (Path.GetExtension(entryPath).Equals(".md", StringComparison.OrdinalIgnoreCase))
                continue;

            paths.Add(Path.GetRelativePath(rootDirectory, entryPath));
        }
    }
}
