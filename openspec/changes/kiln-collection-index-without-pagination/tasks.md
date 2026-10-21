# Tasks

## 1. Map unpaginated configuration values

- [ ] 1.1 Adjust `SiteConfigLoader.CollectionDto.Paginate` and `MapCollection` so that absent `paginate`, YAML `false`, and `0` mean unpaginated while positive integers are preserved; add and run `SiteConfigLoaderTests` for all four values.

## 2. Render collection indexes and make them discoverable

- [ ] 2.1 In `SiteBuilder.RenderCollectionIndexesAsync`, accept only an existing `<collection-name>-index.html` as an opt-in when positive pagination is absent, and use a single paginator with all build-eligible items; add `CollectionIndexTests` for all items and exactly one page.
- [ ] 2.2 Align the new index case in `VirtualPageCatalog.CollectVirtualUrls` and `SitemapGenerator.Generate` with the same theme/layout rule so that virtual URL checks, menu references, and the sitemap contain exactly the index URL actually generated; add `SitemapGeneratorTests` and `MenuResolutionTests`.
- [ ] 2.3 Adjust `PaginatorTests.BuildAsync_CollectionNoPaginate_NoIndexPage` to the new opt-in semantics and cover both a missing collection template and unchanged positive `paginate`; tests must confirm that generic `index.html`/`default.html` fallbacks are not an opt-in.
- [ ] 2.4 Add the interaction with `home.page` to `HomepageEngineTests` and retain the existing error for `home.collection` without positive `paginate`; test root output, collection URL, and the error separately.

## 3. Validation in the target repository

- [ ] 3.1 Run `rtk dotnet build` and confirm zero warnings/errors.
- [ ] 3.2 Run `dotnet test` (without `rtk`) and confirm all TUnit tests pass.
- [ ] 3.3 Run `jb inspectcode Kiln.slnx -e=WARNING -f=Text` and confirm zero findings.
- [ ] 3.4 Run an end-to-end check in a temporary directory: `kiln new`, configure a collection without positive `paginate` and with a matching collection index template, and run `kiln build`; verify all items on exactly one collection index page and the sitemap entry, then clean up that directory specifically.
- [ ] 3.5 Inspect `rtk git show --stat HEAD` and confirm that the file set contains no unexpected changes.
