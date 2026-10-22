---
status: accepted
date: 2026-10-19
updated: 2026-10-21
---

## Why
Kiln promises CDN-friendly asset URLs but partly treats `assetPrefix` as a filesystem path; absolute CDN bases therefore do not work consistently. Rendering, image optimization, fingerprinting, and offline checking must use the same mapping so that HTML, CSS, and Atom feeds point to files actually output.

## What Changes
- Keep physical asset output under `<outputDir>/assets/` regardless of URL/host; neither the URL string nor the CDN path component becomes the output directory.
- Use `assetPrefix` as the only camelCase configuration key for a root-relative prefix or absolute HTTP(S) CDN base; the default remains `/assets/`.
- Consistently project asset helpers, bundle Markdown, and owned references in HTML (`src`, `href`, `srcset`, `poster`), CSS (`url()`, both `@import` forms), and generated Atom feed content, combining them with WebP/fingerprints in production. Existing user-written `srcset` is processed, but no responsive image output is generated.
- Always output owned feed asset URLs as absolute URLs: through the CDN origin or, for a local prefix, the site origin. Feed files and article links remain site-local.
- Check owned references on these surfaces offline against local output files; continue to skip foreign external URLs. `build.linkCheck: false` continues to disable existence checks.
- Finalize CSS dependencies before hashing the referring stylesheet; with fingerprinting enabled, report cycles as build errors with a clear dependency diagnostic.
- Retain the base-path contract: root-relative prefixes receive the effective site base path exactly once, absolute prefixes never do. `--base-url` does not change the CDN host or its path.
- In every development build, replace only absolute CDN prefixes with the local base `/assets/`; root-relative configuration remains effective, including in CSS references. For a different local prefix, `DevServer` must map that namespace specifically to `assets/`. Changes to `assetPrefix` or the effective base path during `serve` require a restart with a clear message instead of publishing new output under stale routes.
- **BREAKING:** `site.asset_prefix` exposes the effective public base including the base path with a trailing slash, not the unchecked raw value. Custom themes with additional base-path composition must remove it; helper-based themes need no adjustment.
- Report overlaps of local asset namespaces with generated pages or reserved Pagefind/live-reload routes as errors instead of silently allowing one route to win.
- Pagefind remains under `<outputDir>/pagefind/` and `site.base_path + '/pagefind/'`; only Kiln's own search script from `static/` uses the asset prefix.

## Capabilities
### New Capabilities
None.

### Modified Capabilities
- `kiln/assets`: Separate the public asset base from physical output, unify helpers and content/feed rewrites, and define local namespace boundaries.
- `kiln/build-modes`: Apply production rewrites/link checking to all agreed reference surfaces, and define CSS hash dependencies and local development output, including the restart rule.

## Impact
- Target repository: `PepperPix/Kiln`; target branch: new feature branch from `beta`.
- Affected: `SiteConfiguration`, `SiteConfigLoader`, `SiteBuilder`, `TemplateRenderer`, `SharedRenderContext`, `AssetPipeline`, `SiteAssetCopier`, `FeedGenerator`, `DevServer`, and TUnit tests in `Kiln.Core.Tests`; CLI override regressions and starter/README documentation. Canonical shortcode output is included in later HTML/feed rewrites; the shortcode contract remains unchanged.
- Related contracts: production pipeline, live reload, image optimization/reference index, subfolder/base-URL override, Pagefind, shortcodes/API compatibility, and the shared asset URL contract.
- No new CDN library or configuration block. A CDN/reverse proxy must map `<public prefix>/<relative asset path>` to the content of `<outputDir>/assets/`; Kiln uploads nothing.
- **Out of scope:** No CDN provisioning, upload, cache-header management, CORS setup, or integrity-attribute generator; no Pagefind relocation, new search API, responsive image generation, JavaScript import rewriter, rewriting of inline CSS/scripts or arbitrary strings, or automatic migration of third-party themes. Image selection and public Core interfaces are not expanded; foreign external URLs remain unchanged. Bundle subdirectories are planned separately in `kiln-page-bundle-subdirectories`; the CDN correction works independently with current flat bundles.
