namespace Engobe.Cli.Completion;

using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

internal sealed class CommandModelCapture : IHelpProvider
{
    public ICommandModel? Model { get; private set; }

    public IEnumerable<IRenderable> Write(ICommandModel model, ICommandInfo? command)
    {
        Model = model;
        return [];
    }
}
