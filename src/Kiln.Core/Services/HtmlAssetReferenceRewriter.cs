namespace Kiln.Services;

using AngleSharp;
using AngleSharp.Html.Parser;

internal static class HtmlAssetReferenceRewriter
{
    private static readonly string[] Attributes = ["src", "href", "poster"];

    internal static string RewriteDocument(
        string html,
        AssetUrlResolver resolver,
        IReadOnlyDictionary<string, string>? manifest = null,
        bool absoluteForLocal = false) =>
        Rewrite(html, resolver, manifest, absoluteForLocal, isFragment: false);

    internal static string RewriteFragment(
        string html,
        AssetUrlResolver resolver,
        IReadOnlyDictionary<string, string>? manifest = null,
        bool absoluteForLocal = false) =>
        Rewrite(html, resolver, manifest, absoluteForLocal, isFragment: true);

    private static string Rewrite(
        string html,
        AssetUrlResolver resolver,
        IReadOnlyDictionary<string, string>? manifest,
        bool absoluteForLocal,
        bool isFragment)
    {
        var parser = new HtmlParser();
        var document = parser.ParseDocument(isFragment ? string.Empty : html);
        var fragmentContainer = isFragment
            ? document.Body ?? throw new InvalidOperationException("AngleSharp did not create a document body.")
            : null;
        if (fragmentContainer is not null)
            fragmentContainer.InnerHtml = html;

        var changed = false;
        foreach (var element in document.All)
        {
            foreach (var attribute in Attributes)
            {
                var value = element.GetAttribute(attribute);
                if (value is null)
                    continue;
                var projected = resolver.Project(value, manifest, absoluteForLocal);
                if (!string.Equals(value, projected, StringComparison.Ordinal))
                {
                    element.SetAttribute(attribute, projected);
                    changed = true;
                }
            }

            var srcset = element.GetAttribute("srcset");
            if (srcset is not null)
            {
                var projected = RewriteSrcset(srcset, resolver, manifest, absoluteForLocal);
                if (!string.Equals(srcset, projected, StringComparison.Ordinal))
                {
                    element.SetAttribute("srcset", projected);
                    changed = true;
                }
            }
        }

        return changed ? fragmentContainer?.InnerHtml ?? document.ToHtml() : html;
    }

    internal static string RewriteSrcset(
        string srcset,
        AssetUrlResolver resolver,
        IReadOnlyDictionary<string, string>? manifest = null,
        bool absoluteForLocal = false)
    {
        var output = new System.Text.StringBuilder(srcset.Length);
        var index = 0;
        while (index < srcset.Length)
        {
            var leadingStart = index;
            while (index < srcset.Length && (char.IsWhiteSpace(srcset[index]) || srcset[index] == ','))
                index++;
            output.Append(srcset.AsSpan(leadingStart, index - leadingStart));
            if (index >= srcset.Length)
                break;

            var urlStart = index;
            while (index < srcset.Length && !char.IsWhiteSpace(srcset[index]))
                index++;
            var urlEnd = index;
            while (urlEnd > urlStart && srcset[urlEnd - 1] == ',')
                urlEnd--;
            var hasTrailingComma = urlEnd < index;
            var candidateUrl = srcset[urlStart..urlEnd];
            output.Append(resolver.Project(candidateUrl, manifest, absoluteForLocal));
            output.Append(srcset.AsSpan(urlEnd, index - urlEnd));

            if (hasTrailingComma)
                continue;

            var descriptorStart = index;
            var parenthesisDepth = 0;
            while (index < srcset.Length)
            {
                if (srcset[index] == '(')
                    parenthesisDepth++;
                else if (srcset[index] == ')')
                    parenthesisDepth = Math.Max(0, parenthesisDepth - 1);
                else if (srcset[index] == ',' && parenthesisDepth == 0)
                {
                    index++;
                    break;
                }
                index++;
            }
            output.Append(srcset.AsSpan(descriptorStart, index - descriptorStart));
        }

        return output.ToString();
    }
}
