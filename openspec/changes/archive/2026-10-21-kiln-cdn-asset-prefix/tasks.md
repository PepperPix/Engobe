---
status: accepted
date: 2026-10-19
updated: 2026-10-21
---

# Tasks

Implemented in `PepperPix/Kiln` on a feature branch targeting `beta`. All product paths are relative to this repository. The basis is the design decisions confirmed and extended on 2026-10-21, both specification deltas, and the approved review corrections below.

## 1. Implement the agreed URL contract and configuration

- [x] 1.1 Add a shared internal asset-addressing model in a new `src/Kiln.Core/Services/AssetUrlResolver.cs`: account for canonical relative keys, physical `assets/` paths, effective URL base, inverse prefix mapping, query/fragment handling, and mode. New `tests/Kiln.Core.Tests/Services/AssetUrlResolverTests.cs` verify `/assets/`, `/media/`, absolute HTTP(S) bases with differing paths, with/without a slash, host/port/path boundaries, encoding, and escape prevention; no network access.
- [x] 1.2 Extend `SiteConfigLoader.MapToConfig` in `src/Kiln.Core/Configuration/SiteConfigLoader.cs` with prefix validation according to the delta; check value preservation in `SiteConfiguration.AssetPrefix` and `WithBaseUrl` in `src/Kiln.Core/Models/SiteConfiguration.cs`. `tests/Kiln.Core.Tests/Configuration/SiteConfigLoaderTests.cs` checks camelCase, default, valid root-relative/CDN values, and all invalid forms; do not introduce an `asset_prefix` alias. Completion: clear configuration errors before file output.
- [x] 1.3 In `README.md` and `src/Kiln.Core/Templates/default/site.yaml.template`, distinguish physical output from public base; use a `/v2/media/` example to show that the CDN origin must point to the content of `assets/`. Verify that the loader accepts the documented configuration, with no upload/cache-header guarantee.
- [x] 1.4 In `SiteBuilder`, before clearing/writing output, check the effective asset namespace against content and virtual pages and active Pagefind/live-reload routes. `SiteBuilderTests.cs` and appropriate configuration tests cover pages at/within the namespace, subfolders, segment-similar non-collisions, unchanged old output on errors, same-origin asset bases and external CDN origins, and the development fallback. Document reservation and remediation in `README.md`; completion: no silent routing precedence.

## 2. Connect rendering, canonical references, and image optimization

- [x] 2.1 Connect `SiteBuilder.BuildAsync`, `RenderPassContext`, and `SharedRenderContext.Build` in `src/Kiln.Core/Services/` with an explicit build mode/effective asset base; do not silently mutate the loaded `SiteConfiguration` or existing public render/reference-index callers. `tests/Kiln.Core.Tests/Services/SiteBuilderTests.cs` checks consecutive builds of the same configuration in both modes without prefix leakage.
- [x] 2.2 Change `TemplateRenderer.BuildCommonScriptObject`, `BuildPageScriptObject`, and `SharedRenderContext` in `src/Kiln.Core/Services/{TemplateRenderer,SharedRenderContext}.cs` to shared URL composition: `asset_url`, `plugin_asset_url`, `page_asset_url`, `site.asset_prefix`, and rendered `page.content`. `TemplateRendererTests.cs` checks full CDN URLs, root-relative custom prefixes with a base path, unchanged page URLs, and exactly one trailing slash on the exposed prefix.
- [x] 2.3 Retain canonical `ContentItem.HtmlContent`/`AssetReferenceIndexBuilder.Build(items)` keys under `/assets/` for image selection and apply the public base only during render/output projection. Connect `SiteAssetCopier.Copy`, `CopyOrOptimizeFile`, and `AssetPipeline.RewriteReferences` through a shared WebP/fingerprint manifest. `SiteBuilderImageOptimizationTests.cs` and `AssetReferenceIndexBuilderTests.cs` check a CDN bundle image, site-static image, opt-out, exclusion, unreferenced image, and extension change, plus unchanged selection with additional `srcset`/`poster`/CSS references; completion: canonical index unchanged, output and URL agree.
- [x] 2.4 Extend `src/Kiln.Core/Templates/default/content/posts/10-configuration-deep-dive.md.template` and `README.md` with camelCase `assetPrefix`, helper use, and the explicitly **BREAKING** migration from `site.base_path + site.asset_prefix` to the already effective base; use renderer tests to verify that examples do not double-prefix. No automatic migration of third-party themes.
- [x] 2.5 Preserve the canonical `ShortcodeProcessor`/`IShortcodeProcessor` contract and integrate HTML output from shortcodes and plugin slots into later projection. `ShortcodeProcessorTests.cs` and pipeline integration tests check the unchanged direct helper call and public plugin asset URLs in production/development with a base path; inline scripts and example text remain unchanged. Completion: no new public parameters or configuration mutation.

## 3. Correct the production pipeline and offline link checking

- [x] 3.1 In `src/Kiln.Core/Services/AssetPipeline.cs`, always direct filesystem access in `RunMinifyAssetsAsync` and `RunFingerprint` to `Path.Combine(outputDir, "assets")`; form public URLs only through the resolver. `AssetPipelineIntegrationTests.cs` checks minification/fingerprinting for absolute prefixes with `/assets/` and `/v2/media/` and a root-relative custom prefix; completion: no `https:`/CDN path directories and unchanged copier collision priority.
- [x] 3.2 Share bounded HTML/CSS reference processing between `AssetPipeline.cs` and render projections: HTML `src`/`href`/`srcset`/`poster`, CSS `url()` and both `@import` forms. Reuse existing parsers; no global string replacement or naive comma splitting. `AssetPipelineIntegrationTests.cs` checks all CSS quoting forms, relative CSS targets, import conditions, mixed `srcset` candidates with data/external URLs and descriptors, query/fragment, and unchanged inline CSS/scripts and example text. Completion: only owned supported references are projected, including without fingerprinting.
- [x] 3.3 Finalize fingerprinting in dependency order: rewrite leaf assets, then imported CSS files, and finally importing stylesheets to final target names, and hash the final bytes. Tests with nested imports, WebP+font, query/fragment, and repeated builds compute SHA-256 independently and compare the first eight hexadecimal characters to each CSS filename. A self-reference/cycle must fail with the file chain even with link checking disabled; with `build.fingerprint: false`, files remain unhashed without an additional cycle error. `README.md` documents order, cycle diagnostics, and deliberate opt-out.
- [x] 3.4 Include Atom feeds generated in `SiteBuilder.WriteFeedsAndMetaAsync` in final projection: read only HTML content fields through structured XML processing, map them to final absolute asset URLs using the shared rewrite, and write correctly escaped XML. `FeedGeneratorTests.cs` and pipeline integration tests check local prefix+site origin, CDN, base-URL override, WebP+hash, query/fragment with `&`, `srcset`/`poster`, and development. Canonical items, feed metadata, article links, and foreign references remain unchanged. Document feed behavior in `README.md`.
- [x] 3.5 Extend `RunLinkCheckAsync`, `IsInternalRef`, `ResolveInternalRef`, and reference checking with inverse asset mapping on all agreed HTML/CSS/Atom surfaces; map final absolute feed asset URLs under the site's own origin locally, remove the base path once, and never send HTTP requests. `AssetPipelineIntegrationTests.cs` checks existing/missing targets, relative CSS imports, query/fragment, similar hosts/neighboring paths, root/subfolder pages, and `build.linkCheck: false`. Completion: the error names source and reference; foreign external targets remain unchecked.
- [x] 3.6 Extend `tests/Kiln.Cli.Tests/BuildCommandAppTests.cs` with `--production --base-url https://example.org/docs/` using local and absolute asset prefixes; correct existing forwarding in `src/Kiln.Cli/Commands/BuildCommand.cs` only if needed. Also inspect actual HTML/feed output in `AssetPipelineIntegrationTests.cs`; completion: the override changes the page/local asset base and absolute feed site origin, never the absolute CDN base.
- [x] 3.7 Document the rules for offline checking, query/fragment, supported reference surfaces, unchanged inline/JavaScript-import references, and `build.fingerprint: false` in `README.md` and compare them with integration-test fixtures; explicitly state that CDN reachability/CORS are not checked.

## 4. Verify local preview and Pagefind

- [x] 4.1 In `SiteBuilder`'s asset context, reset only absolute CDN bases to local `/assets/` for development and retain root-relative prefixes; helpers, content, CSS, and feed HTML must use the same file mapping, with the site origin added for feeds. Run shared reference projection outside the previously production-only pipeline call as well. `AssetPipelineIntegrationTests.cs` checks CSS imports and development without hashed names/minification/CDN host and with unchanged configuration; `SiteBuilderOutputDirectoryTests.cs` checks repeated local builds without stale output.
- [x] 4.2 Connect `DevServer.RunAsync`/`ServeRequestAsync` in `src/Kiln.Core/Services/DevServer.cs` to the effective local asset base; map only the matching asset namespace including site base path back to `assets/` and correctly serve normal site/Pagefind paths under the base path. `DevServerPathSafetyTests.cs` checks `/docs/media/css/site.css`, `/docs/assets/...` with CDN configuration, local pages, and traversal/host-header regressions; `DevServerLiveReloadTests.cs` verifies rebuild and CSS hot swap for the local base. Completion: local responses with actual new bytes, no remote proxy.
- [x] 4.3 Extend `tests/Kiln.Core.Tests/Services/SearchUiIntegrationTests.cs` and `PagefindSearchIndexerTests.cs` with a CDN+subfolder regression. `src/Kiln.Core/Templates/default/themes/default/partials/search.html` and `static/js/kiln-search.js` currently read the module URL from `site.base_path`/`data-pagefind-module`; preserve this behavior. Completion: module and Pagefind CSS remain `/docs/pagefind/...`, Kiln's search script uses the CDN base, and the indexer continues to use local output; no changes to binary resolution or version.
- [x] 4.4 Before `DevServer` rebuilds, compare current `assetPrefix`/base-path values with startup configuration. On changes, abort before build/file output and report a restart request through SSE/overlay. `DevServerLiveReloadTests.cs` checks prefix and base-path changes, unchanged old files/routes, no new target paths, recovery after reverting, adoption after restart, and continued CSS hot swap for normal changes. Completion: no automatic routing mutation.
- [x] 4.5 Document development fallback, local custom prefixes, restart rule, and the Pagefind exception in `src/Kiln.Core/Templates/default/content/posts/10-configuration-deep-dive.md.template` and `README.md`; compare example output against preview/search tests.

## 5. Validation in the target repository

- [x] 5.1 Run `rtk dotnet build` at the root of `PepperPix/Kiln`; result: **zero warnings/errors**.
- [x] 5.2 In the same root, run `dotnet test` without `rtk`; result: **all tests pass**. Use only TUnit `--treenode-filter` for focused preliminary runs; no VSTest filters.
- [x] 5.3 Run `rtk proxy jb inspectcode Kiln.slnx -e=WARNING -f=Text`; result: **zero findings**.
- [x] 5.4 With the newly built CLI, run `rtk kiln new cdn-asset-prefix-e2e` in an isolated fixture directory created solely for this purpose. Add `assetPrefix: https://cdn.example.com/v2/media/`, an Atom feed, a CSS import chain, and `srcset`/`poster`; use `rtk kiln build <fixture> --production --base-url https://example.org/docs/ --no-search` to check final HTML/CSS/feed URLs, valid Atom XML, actual SHA-256 filenames, and only `assets/` on disk. A missing owned target must fail with link checking enabled, and a CSS cycle must fail with fingerprinting enabled. A development build and short `kiln serve` request must return new local bytes; a prefix change reports a restart without output changes. Also enable search with an available Pagefind binary and check the site-local module/index; explicitly report unavailable search validation. Clean up the fixture and the specific server started. Result: no CDN connection, complete final references, and working local preview.
- [x] 5.5 Inspect `rtk git show --stat HEAD` and `rtk git diff --stat` in the target repository; result: only expected Core/CLI/test/product documentation files, no unexpected deletions/additions, and no private planning or author-marker files. Also run `rtk dotnet pack` against the existing package baseline; result: no unintended breaks in stable .NET API signatures, with the documented template behavior migration remaining explicit.

## 6. Review corrections

The maintainer approved these corrections after review. URL tests use existing public configuration, renderer, and builder contracts rather than exposing implementation types to the test assembly. This replaces the direct internal resolver tests described in task 1.1 without expanding the public API.

- [x] 6.1 Remove the friend-assembly declaration and replace direct internal calls with public contract tests, retaining prefix, mode, encoding, boundary, suffix, and traversal coverage.
- [x] 6.2 Generate and reuse deterministic PNG fixture data efficiently, retain real image optimization assertions, and measure the focused image tests and full suite without raising timeouts or skipping tests.
- [x] 6.3 Preserve document types when rewriting asset references and test production output with a rewritten fingerprinted asset.
- [x] 6.4 Recognize configured CDN input URLs in development while projecting to the effective local base; cover HTML, CSS, and Atom content plus foreign origin/path boundaries.
- [x] 6.5 Complete CDN image-selection regressions and virtual-page namespace collision coverage, preserving existing output on failure.
- [x] 6.6 Repeat build, full tests, inspection, package validation, and focused CLI verification after the corrections.
- [x] 6.7 Include the search script in newly scaffolded sites and defer only the two known local Pagefind entrypoints when search is enabled, with an explicit diagnostic. Keep missing assets, unknown Pagefind paths, neighboring paths, and wrong-base paths as errors. Verify a fresh production site with the installed Pagefind binary. Isolate the missing-binary CLI smoke test from installed binaries.
- [x] 6.8 Compare offline target URL paths case-sensitively; cover matching and mismatched case in local/CDN HTML, CSS, Atom content, and site-page links, with fingerprinting enabled and disabled. Preserve case in rename manifests and CSS dependencies rather than silently correcting invalid references.
- [x] 6.9 Reject raw and percent-encoded backslashes in root-relative and absolute asset prefixes before output, including backslashes normalized by URI parsing.
- [x] 6.10 Repeat final build, tests, inspection, package/API validation, and CLI checks; synchronize main specifications and validate readiness for archival and a pull request against beta.

### Review correction results

The test project compiles without friend-assembly access or new public APIs. Public contract tests cover helper composition, encoded keys and suffixes, origin/path/port boundaries, rejected traversal, relative CSS, and repeated modes using the same configuration and canonical items.

The original eight real-codec image tests passed in 2.99 seconds including the runner after replacing per-pixel native calls with a single managed pixel buffer and reusing the encoded PNG. The complete Core suite then passed 542 tests in 6.41 seconds. No timeout or parallelism settings changed.

The image-selection matrix now runs for both local and CDN prefixes, including bundle/static images, opt-out, exclusions, untouched images, cache reuse, and real WebP conversion. Added taxonomy collision cases exposed missing base-path composition for virtual asset-namespace checks; those routes now receive the effective site base path before comparison. Rejected builds retain the old output. The expanded Core suite passed 557 tests in 7.26 seconds including the runner.

Pagefind 1.5.2 became available during final validation. The missing-binary CLI smoke test now isolates its child process from the system PATH, explicit Pagefind overrides, and the user's cache. A fresh starter production build with search enabled passes offline checks, reports deferred entrypoint checks, and then creates the real local Pagefind module, CSS, and index. The search script is included in the scaffold and is fingerprinted under the CDN base. All generated page document types survive, and independent SHA-256 checks match the final CSS filenames. A development CLI build localizes authored CDN references in HTML, CSS, and Atom content without changing configuration. The final solution suite passed 679 tests in 7.42 seconds, with no skipped tests.

Initial correction validation: solution build completed with zero warnings or errors; warning-level inspection reported no findings; packing passed the existing API baseline. Both temporary end-to-end sites and the isolated diagnostic directories were removed.

A separate independent follow-up review found two remaining issues: case-insensitive offline target lookup and accepted backslashes in asset prefixes. Both were reproduced with failing public-contract tests and corrected. Target sets, rename manifests, and CSS dependency keys now preserve URL path case. Eight root-relative/absolute raw/encoded backslash cases are rejected before URI normalization and output.

Final validation after these fixes: 695 solution tests passed with no skips in 8.55 seconds (582 Core tests in 7.38 seconds); build had zero warnings/errors, inspection had zero findings, and package/API baseline validation passed. The real CLI accepted a fresh CDN/subfolder starter with Pagefind 1.5.2, matched final-byte CSS hashes, rejected incorrectly cased assets with fingerprinting enabled, and rejected an encoded-backslash prefix while retaining every previous output byte. The final fixture and isolated test directories were removed. Both main specifications were synchronized with their deltas, preserving existing titles, purposes, and unrelated requirements.

The maintainer explicitly requested archival on the feature branch before opening the pull request, rather than after merge. Archival does not imply the pull request has merged or CI has passed.

### CLI end-to-end results

The built CLI passed production builds with CDN and local prefixes, a site base-URL override, final-byte SHA-256 stylesheet checks, nested CSS imports, HTML `srcset`/`poster`, suffix preservation, and valid Atom HTML with absolute asset URLs. Missing owned targets and CSS fingerprint cycles failed with the expected diagnostics.

Development builds used local unhashed URLs without changing configuration. Preview served the new CSS bytes under the site base path, rejected routing changes through an SSE restart error without changing any output file, recovered after configuration restoration, and adopted a custom local prefix after restart. CSS-only edits emitted the CSS update event. The temporary fixture was removed and both temporary preview instances were stopped.

The initial pass had no Pagefind binary. The review correction pass completed the search-enabled CLI check with the installed Pagefind 1.5.2 binary as described above.

### Cross-platform CI correction

The first Windows CI run reported 16 failures. Fixture configuration used platform-native
line endings but injected prefixes using LF-only matches. The fixtures now normalize line
endings before text edits, with explicit LF/CRLF cases asserting the loaded prefix, base
path, feed, and search settings. The cycle assertion accepts either rotation of the same
closed cycle rather than depending on filesystem enumeration order. Atom diagnostics use
the same web-path formatting as HTML and CSS diagnostics.

Local validation passed all 699 tests in both Debug and the Release CI configuration
(`--maximum-parallel-tests 4`), with no skips. The Release build had zero warnings/errors
and inspection had zero findings. No timeouts, parallelism settings, or platform exclusions
were changed. Windows confirmation remains subject to the next CI run.
