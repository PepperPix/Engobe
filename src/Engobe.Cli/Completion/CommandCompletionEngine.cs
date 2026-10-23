namespace Engobe.Cli.Completion;

using Spectre.Console.Cli.Help;

internal static class CommandCompletionEngine
{
    public static async Task<IReadOnlyList<string>> GetSuggestionsAsync(
        ICommandModel model,
        string commandLine,
        int cursorPosition,
        IDynamicCompletionProvider dynamicProvider,
        CancellationToken cancellationToken)
    {
        var line = commandLine[..Math.Clamp(cursorPosition, 0, commandLine.Length)];
        var words = SplitWords(line);
        var hasTrailingSpace = line.Length > 0 && char.IsWhiteSpace(line[^1]);
        var completeWordCount = hasTrailingSpace ? words.Count : Math.Max(0, words.Count - 1);
        var container = (ICommandContainer)model;
        var commandPath = new List<string>();

        for (var index = 1; index < completeWordCount; index++)
        {
            var word = words[index];
            if (word.StartsWith("-", StringComparison.Ordinal))
                break;

            var command = container.Commands.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, word, StringComparison.OrdinalIgnoreCase));
            if (command is null)
                return [];

            container = command;
            commandPath.Add(command.Name);
        }

        var prefix = hasTrailingSpace || words.Count == 0 ? string.Empty : words[^1];
        if (prefix.StartsWith("-", StringComparison.Ordinal))
            return GetOptionSuggestions(container, prefix);

        var commandSuggestions = container.Commands
            .Where(command => !command.IsHidden
                && !command.IsDefaultCommand
                && command.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(command => command.Name)
            .ToArray();

        if (commandSuggestions.Length > 0)
            return commandSuggestions;

        if (container is not ICommandInfo commandInfo
            || !commandInfo.Parameters.OfType<ICommandArgument>().Any())
        {
            return [];
        }

        return await dynamicProvider
            .GetSuggestionsAsync(commandPath, prefix, cancellationToken)
            .ConfigureAwait(false) ?? [];
    }

    private static string[] GetOptionSuggestions(ICommandContainer container, string prefix)
    {
        if (container is not ICommandInfo commandInfo)
            return [];

        return commandInfo.Parameters
            .OfType<ICommandOption>()
            .Where(option => !option.IsHidden)
            .SelectMany(option => option.LongNames)
            .Select(name => $"--{name}")
            .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static List<string> SplitWords(string line)
    {
        var words = new List<string>();
        var current = new System.Text.StringBuilder();
        var quote = '\0';

        foreach (var character in line)
        {
            if (quote != '\0')
            {
                if (character == quote)
                    quote = '\0';
                else
                    current.Append(character);

                continue;
            }

            if (character is '"' or '\'')
            {
                quote = character;
            }
            else if (char.IsWhiteSpace(character))
            {
                if (current.Length > 0)
                {
                    words.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(character);
            }
        }

        if (current.Length > 0)
            words.Add(current.ToString());

        return words;
    }
}
