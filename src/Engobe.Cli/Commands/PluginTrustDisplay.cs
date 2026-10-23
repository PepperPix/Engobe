namespace Engobe.Cli.Commands;

using Engobe.Models;

internal static class PluginTrustDisplay
{
    public static string Label(PluginTrustLevel trust)
        => trust == PluginTrustLevel.FirstParty ? "first-party" : "community";

    public static string Markup(PluginTrustLevel trust)
        => trust == PluginTrustLevel.FirstParty ? "[green]first-party[/]" : "[yellow]community[/]";
}
