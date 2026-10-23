namespace Engobe.Cli.Completion;

using Engobe.Cli.Infrastructure;
using Engobe.Services;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

internal static class DotnetSuggestCompletionHandler
{
    public static bool IsCompletionRequest(string[] args)
        => args.Length > 0 && SuggestDirective.TryParse(args[0], out _);

    public static Task<int> HandleAsync(
        string[] args,
        Action<IConfigurator> configureCommands,
        CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || !SuggestDirective.TryParse(args[0], out var directive))
            throw new ArgumentException("The arguments do not contain a dotnet-suggest request.", nameof(args));

        return HandleValidRequestAsync(args, directive, configureCommands, cancellationToken);
    }

    private static async Task<int> HandleValidRequestAsync(
        string[] args,
        SuggestDirective directive,
        Action<IConfigurator> configureCommands,
        CancellationToken cancellationToken)
    {
        var commandLine = args.Length > 1 ? RemoveOuterQuotes(args[1]) : string.Empty;
        var capture = await CaptureCommandModelAsync(configureCommands).ConfigureAwait(false);
        var candidates = await CommandCompletionEngine.GetSuggestionsAsync(
            capture,
            commandLine,
            directive.CursorPosition,
            new PluginNameCompletionProvider(new PluginLockFile()),
            cancellationToken).ConfigureAwait(false);

        foreach (var candidate in candidates)
            await Console.Out.WriteLineAsync(candidate).ConfigureAwait(false);

        return 0;
    }

    private static async Task<Spectre.Console.Cli.Help.ICommandModel> CaptureCommandModelAsync(
        Action<IConfigurator> configureCommands)
    {
        var helpProvider = new CommandModelCapture();
        var app = new CommandApp(new TypeRegistrar(new ServiceCollection()));
        var quietConsole = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(TextWriter.Null),
        });

        app.Configure(config =>
        {
            config.SetHelpProvider(helpProvider);
            config.ConfigureConsole(quietConsole);
            configureCommands(config);
        });

        var exitCode = await app.RunAsync(["--help"]).ConfigureAwait(false);
        if (exitCode != 0 || helpProvider.Model is null)
            throw new InvalidOperationException("Unable to build the command model for dotnet-suggest.");

        return helpProvider.Model;
    }

    private static string RemoveOuterQuotes(string value)
        => value.Length >= 2 && value[0] == '"' && value[^1] == '"'
            ? value[1..^1]
            : value;
}
