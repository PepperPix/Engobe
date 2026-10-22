namespace Kiln.Services;

using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Kiln.Models;
using Scriban;
using Scriban.Runtime;

/// <summary>
/// Default <see cref="ITemplateRenderer"/> implementation that renders the layouts of a theme as Scriban templates.
/// </summary>
/// <remarks>
/// This type is public for compatibility but is not part of the supported API surface; it may change in any release. Depend on <see cref="ITemplateRenderer"/> instead.
/// </remarks>
public sealed class TemplateRenderer : ITemplateRenderer
{
    public string Render(ContentItem item, SharedRenderContext sharedContext, SiteConfiguration site, string themePath, IReadOnlyList<PluginDefinition> plugins)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(sharedContext);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(plugins);

        var layoutName = item.Layout ?? item.Collection.Layout;
        var layoutPath = ResolveLayoutPath(themePath,
            $"{layoutName}.html",
            "default.html");

        return RenderTemplate(layoutPath, ctx =>
            BuildItemScriptObject(item, sharedContext, site, themePath, ctx, plugins));
    }

    public string RenderCollectionIndex(
        ContentGroup collection,
        Paginator paginator,
        SharedRenderContext sharedContext,
        SiteConfiguration site,
        string themePath,
        IReadOnlyList<PluginDefinition> plugins)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(paginator);
        ArgumentNullException.ThrowIfNull(sharedContext);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(plugins);

        var layoutPath = ResolveLayoutPath(themePath,
            $"{collection.Name}-index.html",
            "index.html",
            "default.html");

        return RenderTemplate(layoutPath, ctx =>
        {
            var collectionIndexUri = new Uri(collection.IndexUrl.OriginalString, UriKind.Relative);
            var indexUrl = paginator.Page == 1
                ? SiteConfiguration.ApplyBasePath(site.BasePath, collectionIndexUri)
                : $"{SiteConfiguration.ApplyBasePath(site.BasePath, new Uri(collection.IndexUrl.OriginalString.TrimEnd('/'), UriKind.Relative))}/page/{paginator.Page}/";
            var so = BuildCommonScriptObject(
                sharedContext,
                site,
                themePath,
                ctx,
                indexUrl,
                plugins,
                collection.Plugins);
            so.Add("collection", SharedRenderContext.BuildCollectionObject(collection, site.BasePath));
            so.Add("paginator", BuildPaginatorObject(paginator, site.BasePath));
            return so;
        });
    }

    internal static bool HasCollectionIndexLayout(string themePath, string collectionName)
    {
        var layoutPath = Path.Combine(themePath, "layouts", $"{collectionName}-index.html");
        return File.Exists(layoutPath);
    }

    public string RenderTaxonomyTerm(
        TaxonomyTerm term,
        Paginator paginator,
        SharedRenderContext sharedContext,
        SiteConfiguration site,
        string themePath,
        IReadOnlyList<PluginDefinition> plugins)
    {
        ArgumentNullException.ThrowIfNull(term);
        ArgumentNullException.ThrowIfNull(paginator);
        ArgumentNullException.ThrowIfNull(sharedContext);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(plugins);

        var layoutPath = ResolveLayoutPath(themePath,
            $"taxonomy-{term.Taxonomy.Name}.html",
            "taxonomy.html",
            "default.html");

        return RenderTemplate(layoutPath, ctx =>
        {
            var termUri = new Uri(term.Url.OriginalString, UriKind.Relative);
            var termPageUrl = paginator.Page == 1
                ? SiteConfiguration.ApplyBasePath(site.BasePath, termUri)
                : $"{SiteConfiguration.ApplyBasePath(site.BasePath, new Uri(term.Url.OriginalString.TrimEnd('/'), UriKind.Relative))}/page/{paginator.Page}/";
            var so = BuildCommonScriptObject(sharedContext, site, themePath, ctx, termPageUrl, plugins, null);
            so.Add("taxonomy", new
            {
                name = term.Taxonomy.Name,
                term = term.Name,
                url = SiteConfiguration.ApplyBasePath(site.BasePath, termUri),
                items = term.Items.Select(item => SharedRenderContext.BuildItemSummary(item, site.BasePath)).ToList()
            });
            so.Add("paginator", BuildPaginatorObject(paginator, site.BasePath));
            return so;
        });
    }

    public string RenderTaxonomyOverview(
        TaxonomyDefinition taxonomy,
        IReadOnlyList<TaxonomyTerm> terms,
        SharedRenderContext sharedContext,
        SiteConfiguration site,
        string themePath,
        IReadOnlyList<PluginDefinition> plugins)
    {
        ArgumentNullException.ThrowIfNull(taxonomy);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(sharedContext);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(plugins);

        var overviewUrl = GetTaxonomyOverviewUrl(taxonomy);

        var layoutPath = ResolveLayoutPath(themePath,
            $"taxonomy-{taxonomy.Name}-index.html",
            "taxonomy-index.html",
            "default.html");

        return RenderTemplate(layoutPath, ctx =>
        {
            var overviewUri = new Uri(overviewUrl.OriginalString, UriKind.Relative);
            var so = BuildCommonScriptObject(
                sharedContext,
                site,
                themePath,
                ctx,
                SiteConfiguration.ApplyBasePath(site.BasePath, overviewUri),
                plugins,
                null);
            so.Add("taxonomy", new
            {
                name = taxonomy.Name,
                url = SiteConfiguration.ApplyBasePath(site.BasePath, overviewUri),
                terms = terms.Select(t => new
                {
                    name = t.Name,
                    slug = t.Slug,
                    url = SiteConfiguration.ApplyBasePath(site.BasePath, new Uri(t.Url.OriginalString, UriKind.Relative)),
                    count = t.Count
                }).ToList()
            });
            return so;
        });
    }

    public string RenderNotFound(SharedRenderContext sharedContext, SiteConfiguration site, string themePath, IReadOnlyList<PluginDefinition> plugins)
    {
        ArgumentNullException.ThrowIfNull(sharedContext);
        ArgumentNullException.ThrowIfNull(site);
        ArgumentNullException.ThrowIfNull(plugins);

        var layoutPath = ResolveLayoutPath(themePath, "404.html");

        return RenderTemplate(layoutPath, ctx =>
            BuildCommonScriptObject(sharedContext, site, themePath, ctx, "/404.html", plugins, null));
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private static string RenderTemplate(
        string layoutPath,
        Func<TemplateContext, ScriptObject> buildScriptObject)
    {
        var template = TemplateCache.GetOrParse(layoutPath);

        if (template.HasErrors)
            throw new InvalidOperationException(
                $"Template errors in '{layoutPath}': {string.Join(", ", template.Messages)}");

        var context = TemplateContextFactory.Create();
        var scriptObject = buildScriptObject(context);
        context.PushGlobal(scriptObject);
        return template.Render(context);
    }

    private static ScriptObject BuildItemScriptObject(
        ContentItem item,
        SharedRenderContext shared,
        SiteConfiguration site,
        string themePath,
        TemplateContext context,
        IReadOnlyList<PluginDefinition> plugins)
    {
        var itemUrl = new Uri(item.Url.OriginalString, UriKind.Relative);
        var so = BuildCommonScriptObject(
            shared,
            site,
            themePath,
            context,
            SiteConfiguration.ApplyBasePath(site.BasePath, itemUrl),
            plugins,
            item.Collection.Plugins);

        var pageObj = new ScriptObject();
        pageObj.Add("id", item.Id);
        pageObj.Add("title", item.Title);
        pageObj.Add("date", item.Date);
        pageObj.Add("content", HtmlAssetReferenceRewriter.Rewrite(item.HtmlContent, shared.AssetUrls));
        pageObj.Add("url", SiteConfiguration.ApplyBasePath(site.BasePath, itemUrl));
        pageObj.Add("slug", item.Slug);
        pageObj.Add("description", item.Description);
        pageObj.Add("teaser", item.Teaser);
        pageObj.Add("draft", item.Draft);
        pageObj.Add("no_index", item.NoIndex);
        pageObj.Add("weight", item.Weight);
        pageObj.Add("extra", item.Extra);
        pageObj.Add("tags", item.Taxonomies.GetValueOrDefault("tags"));
        pageObj.Add("categories", item.Taxonomies.GetValueOrDefault("categories"));
        pageObj.Add("taxonomies", item.Taxonomies);
        pageObj.Add("collection", new
        {
            name = item.Collection.Name,
            url = SiteConfiguration.ApplyBasePath(site.BasePath, new Uri(item.Collection.IndexUrl.OriginalString, UriKind.Relative)),
            feed = item.Collection.Feed,
            plugins = item.Collection.Plugins
        });
        pageObj.Add("next", item.Next is null ? null : (object)new
        {
            title = item.Next.Title,
            url = SiteConfiguration.ApplyBasePath(site.BasePath, new Uri(item.Next.Url.OriginalString, UriKind.Relative))
        });
        pageObj.Add("prev", item.Prev is null ? null : (object)new
        {
            title = item.Prev.Title,
            url = SiteConfiguration.ApplyBasePath(site.BasePath, new Uri(item.Prev.Url.OriginalString, UriKind.Relative))
        });

        // Breadcrumb ancestors
        pageObj.Add("ancestors", BuildAncestors(item, shared, site.BasePath));
        var sectionUrl = shared.NavTree.TryGetValue(item.Collection.Name, out var roots)
            ? FindSectionUrl(roots, item.SectionPath)
            : null;
        pageObj.Add("section", string.IsNullOrEmpty(item.SectionPath) ? null : new
        {
            title = NavigationTreeBuilder.Humanize(item.SectionPath.Split('/')[^1]),
            path = item.SectionPath,
            url = sectionUrl?.OriginalString
                ?? NavigationTreeBuilder.BuildSectionUrl(item, item.SectionPath.Split('/'), site.BasePath).OriginalString
        });

        foreach (var (refKey, refItem) in item.ResolvedReferences)
        {
            pageObj.Add(refKey, new
            {
                title = refItem.Title,
                url = SiteConfiguration.ApplyBasePath(site.BasePath, new Uri(refItem.Url.OriginalString, UriKind.Relative)),
                slug = refItem.Slug,
                extra = refItem.Extra
            });
        }

        so.Add("page", pageObj);

        // Co-located asset_url for page bundles
        var effectiveSlug = string.IsNullOrEmpty(item.SectionPath) ? item.Slug : $"{item.SectionPath}/{item.Slug}";
        so.Import("page_asset_url", new Func<string, string>(
            filename => shared.AssetUrls.FormatPublicAddress($"content/{item.Collection.Name}/{effectiveSlug}/{filename.TrimStart('/')}")));

        return so;
    }

    private static Uri? FindSectionUrl(IReadOnlyList<NavigationNode> nodes, string sectionPath)
    {
        foreach (var node in nodes)
        {
            if (node.SectionPath == sectionPath)
                return node.Url;
            if (node.Item is null && FindSectionUrl(node.Children, sectionPath) is { } url)
                return url;
        }
        return null;
    }

    private static List<object> BuildAncestors(ContentItem item, SharedRenderContext shared, string basePath)
    {
        var ancestors = new List<object>
        {
            new
            {
                title = NavigationTreeBuilder.Humanize(item.Collection.Name),
                url = SiteConfiguration.ApplyBasePath(basePath, item.Collection.IndexUrl)
            }
        };
        var chain = new List<NavigationNode>();
        if (shared.NavTree.TryGetValue(item.Collection.Name, out var roots)
            && FindAncestorChain(roots, item, chain))
        {
            ancestors.AddRange(chain.Select(node => (object)new { title = node.Title, url = node.Url.OriginalString }));
        }
        else
        {
            // Standalone rendering can omit the pre-built navigation tree.
            var segments = item.SectionPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
            {
                ancestors.Add(new
                {
                    title = NavigationTreeBuilder.Humanize(segments[i]),
                    url = NavigationTreeBuilder.BuildSectionUrl(item, segments[..(i + 1)], basePath).OriginalString
                });
            }
        }
        return ancestors;
    }

    private static bool FindAncestorChain(
        IReadOnlyList<NavigationNode> nodes, ContentItem item, List<NavigationNode> chain)
    {
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node.Item, item))
                return true;
            chain.Add(node);
            if (FindAncestorChain(node.Children, item, chain))
                return true;
            chain.RemoveAt(chain.Count - 1);
        }
        return false;
    }

    private static ScriptObject BuildCommonScriptObject(
        SharedRenderContext shared,
        SiteConfiguration site,
        string themePath,
        TemplateContext context,
        string? currentUrl,
        IReadOnlyList<PluginDefinition> plugins,
        Dictionary<string, object>? currentCollectionPlugins)
    {
        var so = new ScriptObject();

        so.Add("site", shared.Site);
        so.Add("collections", shared.Collections);
        so.Add("plugins", shared.Plugins);
        so.Add("theme", shared.Theme);
        so.Add("taxonomies", shared.Taxonomies);

        // menus — active flag computed from currentUrl
        var menusObj = new ScriptObject();
        foreach (var (name, menu) in site.Menus)
            menusObj.Add(name, menu.Items.Select(i => BuildMenuItemObject(i, currentUrl, site.BasePath)).ToList());
        so.Add("menus", menusObj);

        // navtree — per-collection navigation tree with is_active/is_ancestor per currentUrl
        var navObj = new ScriptObject();
        foreach (var (name, roots) in shared.NavTree)
            navObj.Add(name, roots.Select(n => ProjectNavNode(n, currentUrl, site.BasePath).Value).ToList());
        so.Add("navtree", navObj);

        // include partial
        var partialsDir = Path.Combine(themePath, "partials");
        so.Import("include", new Func<string, string>(partialName =>
        {
            var partialPath = Path.Combine(partialsDir, $"{partialName}.html");
            if (!File.Exists(partialPath))
                return $"<!-- partial '{partialName}' not found -->";
            return TemplateCache.GetOrParse(partialPath).Render(context);
        }));

        // asset_url
        so.Import("asset_url", new Func<string, string>(path => shared.AssetUrls.FormatPublicAddress(path)));

        var stringFunctions = new ScriptObject();
        stringFunctions.Import("base64_encode", new Func<object?, string>(value =>
        {
            var raw = value is null ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        }));
        so.Add("string", stringFunctions);

        // plugin_asset_url
        so.Import("plugin_asset_url", new Func<string, string, string>(
            (pluginName, path) => shared.AssetUrls.FormatPublicAddress($"plugins/{pluginName}/{path.TrimStart('/')}")));

        // limit helper for concise list widgets: collections.posts.items | limit 5
        so.Import("limit", new Func<object?, int, object>((source, n) =>
        {
            if (n <= 0 || source is null || source is string || source is not System.Collections.IEnumerable sequence)
                return new List<object>();

            var result = new List<object>(n);
            foreach (var value in sequence)
            {
                if (result.Count >= n)
                    break;

                if (value is not null)
                    result.Add(value);
            }

            return result;
        }));

        // slot — renders all plugin partials for the given slot name
        so.Import("slot", new Func<string, string>(slotName =>
        {
            var applicablePlugins = plugins
                .Where(p => p.Slots.Contains(slotName, StringComparer.OrdinalIgnoreCase))
                .Where(p => IsPluginEnabledForCollection(currentCollectionPlugins, Path.GetFileName(p.Directory)))
                .OrderBy(p => GetPluginPriority(site, Path.GetFileName(p.Directory)));

            var sb = new System.Text.StringBuilder();
            foreach (var plugin in applicablePlugins)
            {
                var pluginKey = Path.GetFileName(plugin.Directory);
                // Lookup: theme-override → plugin-default
                var themeOverridePath = Path.Combine(themePath, "plugins", pluginKey, "slots", $"{slotName}.html");
                var pluginDefaultPath = Path.Combine(plugin.Directory, "slots", $"{slotName}.html");

                string? slotFilePath;
                if (File.Exists(themeOverridePath))
                    slotFilePath = themeOverridePath;
                else if (File.Exists(pluginDefaultPath))
                    slotFilePath = pluginDefaultPath;
                else
                    slotFilePath = null;

                if (slotFilePath is null) continue;

                sb.Append(TemplateCache.GetOrParse(slotFilePath).Render(context));
            }
            return sb.ToString();
        }));

        return so;
    }

    private static (object Value, bool ContainsCurrent) ProjectNavNode(NavigationNode node, string? currentUrl, string basePath)
    {
        var children = node.Children.Select(child => ProjectNavNode(child, currentUrl, basePath)).ToList();
        var nodeUrl = SiteConfiguration.ApplyBasePath(basePath, new Uri(node.Url.OriginalString, UriKind.Relative));
        var isActive = currentUrl is not null &&
            string.Equals(nodeUrl, currentUrl, StringComparison.OrdinalIgnoreCase);
        var containsCurrent = children.Exists(child => child.ContainsCurrent);
        var isAncestor = !isActive && currentUrl is not null &&
            (currentUrl.StartsWith(nodeUrl, StringComparison.OrdinalIgnoreCase) || containsCurrent);

        return (new
        {
            title = node.Title,
            url = nodeUrl,
            weight = node.Weight,
            is_active = isActive,
            is_ancestor = isAncestor,
            children = children.Select(child => child.Value).ToList()
        }, isActive || containsCurrent);
    }

    private static bool IsPluginEnabledForCollection(Dictionary<string, object>? collectionPlugins, string pluginKey)
    {
        if (collectionPlugins is null || !collectionPlugins.TryGetValue(pluginKey, out var raw))
            return false;

        var enabledVal = raw switch
        {
            IDictionary<object, object> yamlDict when yamlDict.TryGetValue("enabled", out var v) => v,
            IDictionary<string, object> strDict when strDict.TryGetValue("enabled", out var v) => v,
            _ => null
        };

        if (enabledVal is null) return false;
        if (enabledVal is bool b) return b;
        return string.Equals(enabledVal.ToString(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetPluginPriority(SiteConfiguration site, string pluginKey)
    {
        if (!site.Plugins.TryGetValue(pluginKey, out var raw)) return int.MaxValue;

        var priorityVal = raw switch
        {
            IDictionary<object, object> yamlDict when yamlDict.TryGetValue("priority", out var v) => v,
            IDictionary<string, object> strDict when strDict.TryGetValue("priority", out var v) => v,
            _ => null
        };

        if (priorityVal is int i) return i;
        return int.TryParse(priorityVal?.ToString(), out var p) ? p : int.MaxValue;
    }

    private static object BuildMenuItemObject(MenuItem item, string? currentUrl, string basePath)
    {
        var children = item.Children.Select(c => BuildMenuItemObject(c, currentUrl, basePath)).ToList();
        var active = IsMenuItemActive(item, currentUrl, basePath);
        return new
        {
            title = item.Title,
            url = item.Url is null ? null : SiteConfiguration.ApplyBasePath(basePath, item.Url),
            external = item.External,
            active,
            children
        };
    }

    private static bool IsMenuItemActive(MenuItem item, string? currentUrl, string basePath)
    {
        if (currentUrl is null) return false;
        var resolvedUrl = item.Url is null ? null : SiteConfiguration.ApplyBasePath(basePath, item.Url);
        var selfActive = resolvedUrl is not null &&
            string.Equals(resolvedUrl, currentUrl, StringComparison.OrdinalIgnoreCase);
        if (selfActive) return true;
        foreach (var child in item.Children)
        {
            if (IsMenuItemActive(child, currentUrl, basePath)) return true;
        }
        return false;
    }

    private static object BuildPaginatorObject(Paginator paginator, string basePath)
    {
        return new
        {
            items = paginator.Items.Select(item => SharedRenderContext.BuildItemSummary(item, basePath)).ToList(),
            page = paginator.Page,
            total_pages = paginator.TotalPages,
            total_items = paginator.TotalItems,
            next_url = paginator.NextUrl is null ? null : SiteConfiguration.ApplyBasePath(basePath, new Uri(paginator.NextUrl.OriginalString, UriKind.Relative)),
            prev_url = paginator.PrevUrl is null ? null : SiteConfiguration.ApplyBasePath(basePath, new Uri(paginator.PrevUrl.OriginalString, UriKind.Relative))
        };
    }

    private static string ResolveLayoutPath(string themePath, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var path = Path.Combine(themePath, "layouts", candidate);
            if (File.Exists(path))
                return path;
        }
        throw new FileNotFoundException(
            $"No layout found in '{themePath}/layouts/'. Tried: {string.Join(", ", candidates)}");
    }

    internal static Uri GetTaxonomyOverviewUrl(TaxonomyDefinition def)
    {
        var slugPos = def.Permalink.IndexOf(":slug", StringComparison.OrdinalIgnoreCase);
        var path = slugPos < 0 ? $"/{def.Name}/" : def.Permalink[..slugPos];
        return new Uri(path, UriKind.Relative);
    }

    internal static string ToSlug(string text)
    {
#pragma warning disable CA1308 // Slug normalisation requires lowercase, not uppercase
        var lower = text.ToLowerInvariant();
#pragma warning restore CA1308
        var result = Regex.Replace(lower, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(result) ? "unnamed" : result;
    }
}
