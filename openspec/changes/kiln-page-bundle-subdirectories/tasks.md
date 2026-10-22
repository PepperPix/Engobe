---
status: accepted
date: 2026-10-19
---

# Tasks

Implement only in `PepperPix/Kiln`, on a new feature branch from `beta`. All product paths below are relative to this repository. The rules under Decisions were confirmed on 2026-10-19; do not run builds/tests during planning.

## 1. Verify the leaf reading rule and asset exclusions

- [ ] 1.1 In `tests/Kiln.Core.Tests/Services/ContentReaderTests.cs`, alongside `ReadCollection_LeafBundle_NotRecursed`, add cases for the bundle root, `child/index.md`, subdirectories with `article.md` and adjacent non-Markdown files in each case, and normal sections outside a bundle; verify exactly one outer item, unchanged `SectionPath`/slug values, and no nested bundle items.
- [ ] 1.2 Add the shared recursive asset catalog as a new internal `PageBundleAssetCatalog` in `src/Kiln.Core/Services/PageBundleAssetCatalog.cs`: paths relative to the outer root; omit only `*.md` files (case-insensitive extension), hidden files/directories with names starting with `.`, and symlinks. Markdown files do not block directories; do not traverse hidden directories or symlinks. New `PageBundleAssetCatalogTests.cs` under `tests/Kiln.Core.Tests/Services/` check multiple levels, `README.MD`, `child/index.md`, invalid frontmatter/drafts and adjacent assets in each case, hidden entries at every level, and symlinks; completion: only the three exclusions apply and adjacent regular assets are included.
- [ ] 1.3 In `src/Kiln.Core/Templates/default/content/posts/06-page-bundles-and-images/index.md.template`, document the leaf reading rule and three asset exclusions, particularly that `README.md` in a media directory excludes only itself and does not block adjacent images; use the test fixture to check that the example matches actual detection.

## 2. Copy asset subtrees and integrate the image pipeline

- [ ] 2.1 Change `SiteAssetCopier.CopyNonMarkdownFiles` in `src/Kiln.Core/Services/SiteAssetCopier.cs` to use allowed catalog files, preserve subpaths with `Path.GetRelativePath`, and create target parent directories before `CopyOrOptimizeFile`; in `tests/Kiln.Core.Tests/Services/SiteBuilderTests.cs`, check output for `images/screens/x.jpg`, `downloads/setup.zip`, same-named files in different subdirectories, copied assets adjacent to nested `index.md`/`article.md` files, and absent output for `.md`, hidden entries, and symlinks.
- [ ] 2.2 In `tests/Kiln.Core.Tests/Services/AssetReferenceIndexBuilderTests.cs` and `AssetReferenceIndexBuilderE2ETests.cs`, test deep canonical bundle image paths; normalize query/fragment for local file association in the existing `AssetReferenceIndexBuilder.Build`, if present, without breaking `IAssetReferenceIndexBuilder.Build(items)`. Verify that the index contains the correct local image key and all referencing items, but no download-only links.
- [ ] 2.3 Extend `tests/Kiln.Core.Tests/Services/SiteBuilderImageOptimizationTests.cs` with a referenced subdirectory image, an unreferenced image, `imageOptimization: false`, `images.exclude`, optimizer errors, cache reuse, and WebP; adjust `SiteAssetCopier`/`AssetPipeline.RewriteReferences` only as necessary. Verify the same optimization decisions as for flat bundles and actual existing targets after WebP/fingerprint rewriting.
- [ ] 2.4 Align registration in `generatedFiles` in `SiteAssetCopier` with the file actually written, including changed extensions. In `tests/Kiln.Core.Tests/Services/SiteBuilderOutputDirectoryTests.cs`, test two development builds with addition, deletion, and renaming of deep assets; completion: new files remain, stale output disappears, and adjacent assets remain untouched.

## 3. Resolve relative Markdown references to the same assets

- [ ] 3.1 Connect `ContentReader.ReadFile` in `src/Kiln.Core/Services/ContentReader.cs` to the bundle catalog and extend `MarkdownProcessor` in `src/Kiln.Core/Services/MarkdownProcessor.cs` with owner-aware image/download resolution; keep existing `IMarkdownProcessor.ToHtml(markdown, assetBasePath)` callers compatible. `MarkdownProcessorTests.cs` and `ContentReaderTests.cs` check `images/x.jpg`, `./`, internal `..`, query/fragment, downloads, nested `index.md`/`README.md` with adjacent assets still resolved, unchanged `.md` targets with warnings, hidden/symlink targets without asset rewriting, anchors, external/root-relative targets, and root escapes.
- [ ] 3.2 Check `TemplateRenderer`/`page_asset_url` in `src/Kiln.Core/Services/TemplateRenderer.cs` for identical subpaths; extend `tests/Kiln.Core.Tests/Services/TemplateRendererTests.cs` with `page_asset_url('images/screens/x.jpg')` using SectionPath, a different frontmatter slug, and a site base path. Completion: Markdown and the helper point to the same copied file without duplicate slashes or duplicate section paths.
- [ ] 3.3 In `tests/Kiln.Core.Tests/Services/AssetPipelineIntegrationTests.cs`, add a production build with a nested image/download, CSS reference, enabled hashed names, and a missing eligible image; verify that existing references pass the link check, missing local image targets produce an error, and subdirectories are preserved with fingerprints.
- [ ] 3.4 Extend the starter bundle in `src/Kiln.Core/Templates/default/content/posts/06-page-bundles-and-images/index.md.template` with a clear subdirectory/download example and the distinction from sections; in `ScaffolderTests.cs`, check that the example is included consistently and without dead references after `kiln new`.

## 4. Validation in the target repository

- [ ] 4.1 Run `rtk dotnet build` at the root of `PepperPix/Kiln`; result: **zero warnings/errors**.
- [ ] 4.2 In the same root, run `dotnet test` without `rtk`; result: **all tests pass**, including the added bundle/Markdown/optimization/pruning tests. Use only TUnit `--treenode-filter` for focused preliminary runs.
- [ ] 4.3 Run `rtk proxy jb inspectcode Kiln.slnx -e=WARNING -f=Text`; result: **zero findings**.
- [ ] 4.4 With the newly built CLI, run an end-to-end check using `rtk kiln new bundle-subdirectories-e2e` and `rtk kiln build <fixture> --production --no-search`; use an isolated fixture directory created solely for this purpose within the working directory, not OS temporary storage. Create deep image/download files adjacent to `child/index.md` and `README.MD`, plus hidden files/directories and symlinks; inspect generated HTML links and output files; a second development build checks asset removal. Then remove the fixture specifically, without deleting tracked directories. Result: successful builds, correct target structure, and copied adjacent assets without publishing `.md`, hidden entries, or symlinks.
- [ ] 4.5 Inspect `rtk git show --stat HEAD` and `rtk git diff --stat` in the target repository; result: only expected Core/test/starter documentation files, no unexpected additions/deletions, and no private planning or author-marker files. This planning scope does not authorize a commit.
