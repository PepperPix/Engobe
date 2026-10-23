namespace Engobe.Cli.Completion;

using Engobe.Services;

internal sealed class PluginNameCompletionProvider(IPluginLockFile lockFile) : IDynamicCompletionProvider
{
    public async Task<IReadOnlyList<string>?> GetSuggestionsAsync(
        IReadOnlyList<string> commandPath,
        string prefix,
        CancellationToken cancellationToken)
    {
        if (commandPath.Count != 2
            || !string.Equals(commandPath[0], "plugin", StringComparison.OrdinalIgnoreCase)
            || !IsPluginNameCommand(commandPath[1]))
        {
            return null;
        }

        var projectPath = Environment.CurrentDirectory;
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in await GetLockedPluginNamesAsync(projectPath, cancellationToken).ConfigureAwait(false))
            names.Add(name);

        var pluginsPath = Path.Combine(projectPath, "plugins");
        if (Directory.Exists(pluginsPath))
        {
            foreach (var directory in Directory.EnumerateDirectories(pluginsPath))
            {
                var name = Path.GetFileName(directory);
                if (PluginNames.IsSafeDirectoryName(name))
                    names.Add(name);
            }
        }

        return names
            .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IEnumerable<string>> GetLockedPluginNamesAsync(string projectPath, CancellationToken cancellationToken)
    {
        var entries = await lockFile.ReadAsync(projectPath, cancellationToken).ConfigureAwait(false);
        return entries.Keys;
    }

    private static bool IsPluginNameCommand(string commandName)
        => string.Equals(commandName, "remove", StringComparison.OrdinalIgnoreCase)
            || string.Equals(commandName, "update", StringComparison.OrdinalIgnoreCase);
}
