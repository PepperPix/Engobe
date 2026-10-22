namespace Kiln.Services;

internal static class CssAssetReferenceRewriter
{
    private const int CommentDelimiterLength = 2;
    private const int EscapedCharacterLength = 2;
    private const int ImportKeywordLength = 7;
    private const int UrlKeywordLength = 3;

    internal static IEnumerable<string> Extract(string css)
    {
        var references = Parse(css);
        return references.Select(reference => reference.Value).ToArray();
    }

    internal static string Rewrite(
        string css,
        string stylesheetKey,
        AssetUrlResolver resolver,
        IReadOnlyDictionary<string, string>? manifest = null,
        bool absoluteForLocal = false)
    {
        var references = Parse(css);
        if (references.Count == 0)
            return css;

        var output = new System.Text.StringBuilder(css);
        for (var i = references.Count - 1; i >= 0; i--)
        {
            var reference = references[i];
            var projected = resolver.ProjectCss(reference.Value, stylesheetKey, manifest, absoluteForLocal);
            if (!string.Equals(projected, reference.Value, StringComparison.Ordinal))
                output.Remove(reference.Start, reference.Length).Insert(reference.Start, projected);
        }
        return output.ToString();
    }

    private static List<CssReference> Parse(string css)
    {
        var references = new List<CssReference>();
        var index = 0;
        while (index < css.Length)
        {
            if (StartsWith(css, index, "/*"))
            {
                var endComment = css.IndexOf("*/", index + CommentDelimiterLength, StringComparison.Ordinal);
                index = endComment < 0 ? css.Length : endComment + CommentDelimiterLength;
                continue;
            }

            if (StartsWith(css, index, "@import") && IsBoundary(css, index + ImportKeywordLength))
            {
                index += ImportKeywordLength;
                SkipWhitespace(css, ref index);
                if (index >= css.Length)
                    break;
                if (index < css.Length && (css[index] == '"' || css[index] == '\''))
                {
                    references.Add(ReadQuoted(css, ref index));
                    continue;
                }
                if (TryReadUrl(css, ref index, out var importReference))
                {
                    references.Add(importReference);
                    continue;
                }
            }

            if (StartsWith(css, index, "url") && IsBoundary(css, index + UrlKeywordLength))
            {
                var urlIndex = index;
                if (TryReadUrl(css, ref index, out var reference))
                {
                    references.Add(reference);
                    continue;
                }
                index = urlIndex;
            }

            if (css[index] is '"' or '\'')
            {
                SkipQuoted(css, ref index);
                continue;
            }
            index++;
        }
        return references;
    }

    private static bool TryReadUrl(string css, ref int index, out CssReference reference)
    {
        reference = default;
        var start = index;
        index += UrlKeywordLength;
        SkipWhitespace(css, ref index);
        if (index >= css.Length || css[index] != '(')
        {
            index = start;
            return false;
        }

        index++;
        SkipWhitespace(css, ref index);
        if (index >= css.Length)
            return false;

        var quote = css[index] is '"' or '\'' ? css[index++] : '\0';
        var valueStart = index;
        if (quote != '\0')
        {
            while (index < css.Length)
            {
                if (css[index] == '\\' && index + 1 < css.Length)
                {
                    index += EscapedCharacterLength;
                    continue;
                }
                if (css[index++] == quote)
                    break;
            }
            var valueEnd = index - 1;
            reference = new CssReference(valueStart, Math.Max(0, valueEnd - valueStart), css[valueStart..valueEnd]);
            while (index < css.Length && css[index] != ')')
                index++;
        }
        else
        {
            while (index < css.Length && css[index] != ')')
                index++;
            var valueEnd = index;
            while (valueEnd > valueStart && char.IsWhiteSpace(css[valueEnd - 1]))
                valueEnd--;
            reference = new CssReference(valueStart, valueEnd - valueStart, css[valueStart..valueEnd]);
        }
        if (index < css.Length)
            index++;
        return true;
    }

    private static CssReference ReadQuoted(string css, ref int index)
    {
        var quote = css[index++];
        var start = index;
        while (index < css.Length)
        {
            if (css[index] == '\\' && index + 1 < css.Length)
            {
                index += EscapedCharacterLength;
                continue;
            }
            if (css[index++] == quote)
                break;
        }
        var end = index - 1;
        return new CssReference(start, Math.Max(0, end - start), css[start..end]);
    }

    private static void SkipQuoted(string css, ref int index)
    {
        var quote = css[index++];
        while (index < css.Length)
        {
            if (css[index] == '\\' && index + 1 < css.Length)
            {
                index += EscapedCharacterLength;
                continue;
            }
            if (css[index++] == quote)
                return;
        }
    }

    private static void SkipWhitespace(string css, ref int index)
    {
        while (index < css.Length && char.IsWhiteSpace(css[index]))
            index++;
    }

    private static bool StartsWith(string value, int index, string token) =>
        value.AsSpan(index).StartsWith(token, StringComparison.OrdinalIgnoreCase);

    private static bool IsBoundary(string value, int index) =>
        index >= value.Length || !(char.IsLetterOrDigit(value[index]) || value[index] is '_' or '-');

    private readonly record struct CssReference(int Start, int Length, string Value);
}
