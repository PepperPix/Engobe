namespace Engobe.Cli.Completion;

internal interface IDynamicCompletionProvider
{
    Task<IReadOnlyList<string>?> GetSuggestionsAsync(
        IReadOnlyList<string> commandPath,
        string prefix,
        CancellationToken cancellationToken);
}
