namespace Engobe.Cli.Completion;

using System.Globalization;

internal readonly record struct SuggestDirective(int CursorPosition)
{
    private const string Prefix = "[suggest:";

    public static bool TryParse(string? value, out SuggestDirective directive)
    {
        directive = default;
        if (value is null
            || !value.StartsWith(Prefix, StringComparison.Ordinal)
            || value.Length <= Prefix.Length
            || value[^1] != ']')
        {
            return false;
        }

        var positionText = value.AsSpan(Prefix.Length, value.Length - Prefix.Length - 1);
        if (!int.TryParse(positionText, NumberStyles.None, CultureInfo.InvariantCulture, out var position))
            return false;

        directive = new SuggestDirective(position);
        return true;
    }
}
