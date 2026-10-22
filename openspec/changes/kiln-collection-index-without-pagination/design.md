# Design

## Context

See `proposal.md` and the specification delta. Currently, `ContentGroup.Paginate`, `SiteBuilder.RenderCollectionIndexesAsync`, `VirtualPageCatalog.CollectVirtualUrls`, and `SitemapGenerator.Generate` jointly control index output; `TemplateRenderer.RenderCollectionIndex` has the fallback chain `<name>-index.html` → `index.html` → `default.html`.

## Goals / Non-Goals

**Goals:** Handle unpaginated collection indexes consistently in rendering, URL resolution, and the sitemap.

**Non-Goals:** Redefine existing homepage promotion or the generic layout fallback for paginated lists.

## Decisions

- Activation without pagination requires an existing collection-specific `<name>-index.html` template. `index.html` and `default.html` are fallbacks, not a declaration of intent for an individual collection. Positive `paginate` values retain the existing rendering path and its fallback chain.
- Unpaginated collections receive a single paginator with all items included in the build. The check for index generation must use the same rule for output, virtual URLs/collisions, menu references, and the sitemap.
- `paginate: false` is treated as unpaginated, as is `0`; the current `CollectionDto.Paginate` type must be checked/adjusted so that YAML `false` does not result in an invalid integer.
- Collection indexes continue to use `IndexUrl`. `home.page` independently occupies `/`; `home.collection` remains restricted to positive pagination. The `index.md` convention does not change.

## Risks / Trade-offs

- [New output for matching index templates with unset/false/0 `paginate`] → Intentional, documented behavior change; `posts` in the current blog/website repositories retain their behavior because of `paginate: 10`.
- [A large collection produces a large single HTML page] → This is the documented meaning of no pagination; themes can still configure a positive page length.

No site configuration migration is required; the template is the opt-in. There are no open decision questions.
