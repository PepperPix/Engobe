# Proposal

## Why

The current builder renders collection index pages only with positive `paginate`; a non-paginated listing is therefore entirely absent. This contradicts the domain model's intended semantics (`paginate: int|false`, default `false` = no pagination), where an index without pagination should still show all items on one page.

## What Changes

- A collection without positive pagination receives exactly one index page with all items included in the build when the theme provides an explicit collection index template `<collection-name>-index.html`.
- Unset `paginate`, `false`, or `0` means a single unpaginated page; positive values retain the existing multi-page behavior.
- Without a collection-specific index template, an unpaginated collection still has no index page. Generic `index.html`/`default.html` fallbacks do not activate this new case.
- Generated unpaginated index pages are consistently included in virtual URL/collision checks, menu references, and the sitemap.
- **Behavior change:** Sites that previously had no listing with unset/false/0 `paginate` will receive one if the theme contains a matching collection index template. Existing blog and website sites configure `posts.paginate: 10`; their posts indexes therefore remain unchanged. Their unpaginated `pages` collections do not change unless a `pages-index.html` exists. The existing test that creates `posts-index.html` with `paginate: 0` must expect the new output.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `kiln/content-model`: Collection index pages can be output as one complete page without pagination; activation requires an explicit collection index template.

## Impact

- Target repository: `PepperPix/Kiln`, feature branch `feature/kiln-collection-index-without-pagination` from `beta`.
- Affected areas: `Kiln.Core/Services/SiteBuilder.RenderCollectionIndexesAsync`, `VirtualPageCatalog.CollectVirtualUrls`, `SitemapGenerator.Generate`, `TemplateRenderer.RenderCollectionIndex`, `SiteConfigLoader.CollectionDto.Paginate`/`MapCollection`; tests `CollectionIndexTests`, `PaginatorTests`, `SitemapGeneratorTests`, `SiteConfigLoaderTests`, and `HomepageEngineTests`.
- Affected contracts: root URL/homepage promotion and the pagination default `paginate: false`. `home.page` remains an independent root page; a collection index continues to use `collection.IndexUrl`. `home.collection` retains its positive-`paginate` prerequisite and is not redefined by this change. A URL conflict remains a build error.
- Out of scope: no new `indexLayout` configuration, no change to item permalinks, no automatic root/homepage behavior for `index.md`, and no change to the existing paginated fallback to `index.html`/`default.html`.
