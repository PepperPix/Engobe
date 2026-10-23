## MODIFIED Requirements

### Requirement: Minify production assets
Kiln SHALL minify CSS, JavaScript, and SVG files under `<outputDir>/assets/` in production mode, independently of the public asset base, and rendered HTML files according to the respective minify options; on a minifier error, Kiln SHALL warn and retain the original content.

#### Scenario: Apply minification and HTML options
- **WHEN** a production build runs with the default minify options
- **THEN** Kiln SHALL minify CSS, JavaScript, SVG, and HTML; the `htmlAggressive` option SHALL also remove optional HTML tags when using the built-in NUglify minifier.

### Requirement: Generate asset fingerprints and references
Kiln SHALL generate enabled asset fingerprints from the final asset content after minification using SHA-256 and eight hexadecimal characters. Owned asset references in HTML `src`/`href`/`srcset`/`poster`, CSS `url()`/`@import`, and the HTML content of generated Atom feeds SHALL be rewritten to the final filenames.

#### Scenario: Fingerprint a minified asset
- **WHEN** a local asset eligible for fingerprinting is minified in production and fingerprinting is not disabled
- **THEN** Kiln SHALL compute the hash from the resulting asset bytes and rewrite the corresponding local HTML and CSS references to the hashed filename.

Local asset file selection SHALL not depend on the URL path component of an absolute CDN prefix. After mapping to local asset files, references SHALL be output with the effective public base; owned feed asset URLs SHALL always be absolute. Query/fragment SHALL remain unchanged. Image format changes such as WebP SHALL be accounted for before final fingerprint rewriting. With fingerprinting disabled, the public base SHALL still apply, but without hashed names.

#### Scenario: Fingerprint CDN references and CSS targets
- **WHEN** production processes CSS under `assets/css/site.css` with `url('../fonts/main.woff2?v=2#font')` and HTML with an owned asset reference, and the prefix is `https://cdn.example.com/v2/media/`
- **THEN** references SHALL point to full CDN URLs with matching hashed names, the font suffix `?v=2#font` SHALL be preserved, and all files SHALL reside locally under `assets/`; the stylesheet hash SHALL cover its final rewritten bytes.

#### Scenario: Combine WebP and fingerprinting
- **WHEN** a referenced bundle image changes extension through enabled WebP optimization and production performs fingerprinting with a CDN prefix
- **THEN** final HTML SHALL contain the full CDN URL with `.webp` and the fingerprint of the file actually output.

#### Scenario: Apply a prefix without fingerprinting
- **WHEN** production uses `build.fingerprint: false` with an absolute CDN prefix
- **THEN** Kiln SHALL still output asset references with that prefix and preserve filenames without a hash.

#### Scenario: Finalize srcset candidates and feed content
- **WHEN** an owned asset appears after WebP and fingerprinting in an HTML `srcset`, a `poster`, or the HTML content of an Atom feed
- **THEN** all owned references SHALL use the same final filename, `srcset` descriptors SHALL be preserved, and feed asset URLs SHALL be absolute.

### Requirement: Check internal links in production output
Kiln SHALL apply enabled link checking after asset rewrites to root-relative HTML `href`/`src`/`srcset`/`poster`, owned absolute asset URLs, CSS `url()`/`@import`, and owned asset references in the HTML of generated Atom feeds. Missing local targets SHALL produce build errors with source and reference; foreign external URLs SHALL remain unchecked.

#### Scenario: Detect a dead internal target
- **WHEN** a checked HTML or CSS reference points to an internal resource that does not exist in the output directory
- **THEN** Kiln SHALL end the build with an error for the dead reference; foreign external URLs outside the owned asset base SHALL not be checked.

Kiln SHALL treat a URL as an owned asset when scheme, host, and effective port match the configured absolute base and its path lies within that base's segment-accurate namespace; similar hosts or neighboring paths SHALL not match. Owned asset URLs SHALL be mapped back to `<outputDir>/assets/<relative asset path>` without sending HTTP requests. For site-local URLs, the effective site base path SHALL be removed exactly once before file checking. Query/fragment SHALL be ignored for local existence checking. Normalized paths SHALL not be allowed to leave their respective local output namespace.

#### Scenario: A missing owned CDN file is an error
- **WHEN** HTML or CSS references `https://cdn.example.com/assets/missing.png?v=2#detail` under the configured base `https://cdn.example.com/assets/` and the local file is missing
- **THEN** the build SHALL end with an error including source and asset reference, without contacting the CDN host.

#### Scenario: Target paths have case-sensitive URL semantics
- **WHEN** a checked reference differs in path case from the local output file, such as `Logo.svg` instead of `logo.svg`
- **THEN** Kiln SHALL report a missing target on HTML, CSS, and Atom reference surfaces, independently of the local filesystem's case behavior; exactly matching asset and page paths SHALL pass, and fingerprint renames SHALL not correct mismatched case silently.

#### Scenario: An owned CDN file with query and fragment exists
- **WHEN** an asset reference under the configured CDN base carries query/fragment and the associated local output file exists
- **THEN** local checking SHALL succeed independently of a network connection to the CDN.

#### Scenario: A prefix-similar external URL remains external
- **WHEN** the configured prefix is `https://cdn.example.com/assets/` and a reference is `https://cdn.example.com.evil.test/assets/x.jpg` or `https://cdn.example.com/assets-other/x.jpg`
- **THEN** Kiln SHALL not check or rewrite those URLs as owned assets.

#### Scenario: Check a subfolder deployment locally
- **WHEN** the site base path is `/docs` and a root-relative prefix `/media/` is used
- **THEN** `/docs/media/logo.svg` SHALL be checked against `<outputDir>/assets/logo.svg` and `/docs/posts/hello/` against `<outputDir>/posts/hello/index.html`, without assuming an additional `docs/` output directory.

#### Scenario: Report missing targets on all agreed reference surfaces
- **WHEN** an owned target in `srcset`, `poster`, a relative CSS import, or Atom HTML content is missing and `build.linkCheck` is enabled
- **THEN** checking SHALL fail the build with the source file and original reference; an absolute feed asset URL under the site's own origin and local asset prefix SHALL also be mapped back to `assets/` offline.

#### Scenario: Explicitly disable link checking
- **WHEN** `build.linkCheck: false` applies and an owned referenced target is missing
- **THEN** Kiln SHALL not produce an existence-checking error for it; URL projection and enabled fingerprinting of existing files SHALL still occur.

#### Scenario: Search entrypoints are generated after the Core build
- **WHEN** search is enabled and the site-local `pagefind/pagefind.js` or `pagefind/pagefind-ui.css` entrypoint under the effective site base path is not yet present
- **THEN** Core link checking SHALL defer only that entrypoint and report an explicit warning that search indexing must run before publishing; other missing paths, incorrect base paths, and missing asset files SHALL remain errors, and search-disabled builds SHALL not apply this exception.

### Requirement: Complete references during optimization
Kiln SHALL minify CSS/JavaScript/SVG in production, then finalize asset dependencies including fingerprints and reference rewrites, then minify HTML, and finally run enabled link checking. In development, Kiln SHALL omit minification, fingerprinting, and link checking; public URL projection SHALL still apply.

#### Scenario: Leave development output unchanged
- **WHEN** a development build copies assets and renders pages
- **THEN** Kiln SHALL skip minification, fingerprinting, and link checking; reference changes SHALL be limited to the agreed public URL projection.

#### Scenario: Produce final feed content before checking
- **WHEN** a production build performs image format changes and fingerprinting
- **THEN** checking SHALL inspect HTML, CSS, and Atom content only after rewriting them to final asset names.

## ADDED Requirements

### Requirement: Finalize CSS dependencies before the stylesheet hash
Kiln SHALL resolve owned CSS `url()` targets and `@import` targets in string or `url()` form relative to the respective stylesheet or the owned public asset base. Before hashing a stylesheet, its references SHALL point to already finalized dependencies. Import conditions and query/fragment SHALL be preserved; foreign targets SHALL remain unchanged.

#### Scenario: Process nested imports from inside out
- **WHEN** `css/site.css` references `css/base.css` through `@import "base.css" screen`, and that file references a local font through `url('../fonts/main.woff2?v=2#font')`
- **THEN** Kiln SHALL fingerprint the font first, then `base.css` with the final font reference, and then `site.css` with the final import reference; `screen` and the suffix SHALL be preserved.

#### Scenario: Project a url import without fingerprinting
- **WHEN** `@import url('base.css')` is processed with `build.fingerprint: false` and an absolute CDN prefix
- **THEN** the import SHALL point to the full CDN URL of `base.css` without a hashed name and retain the same CSS import semantics.

### Requirement: Reject circular fingerprint dependencies
Kiln SHALL report self-references and cycles in the resolved local asset dependency graph as build errors with the involved files when fingerprinting is enabled. Kiln SHALL publish neither order-dependent hashed names nor unhashed cycle files as a successful fingerprint build.

#### Scenario: Diagnose a CSS import cycle
- **WHEN** `a.css` imports `b.css` and `b.css` references `a.css` again, and fingerprinting is enabled
- **THEN** the build SHALL fail with a clear diagnostic of the chain `a.css -> b.css -> a.css`.

#### Scenario: No additional cycle restriction without fingerprinting
- **WHEN** the same CSS files are processed with `build.fingerprint: false` and their targets exist
- **THEN** Kiln SHALL project their references without hashed names and report no fingerprint-cycle error.

### Requirement: Replace the CDN prefix locally in development mode
Kiln SHALL replace absolute HTTP(S) asset prefixes with `/assets/` for output in `development` for both `kiln build` and `kiln serve`, but retain root-relative prefixes; the site base path SHALL then be applied exactly once. Helpers, bundle content, and `site.asset_prefix` SHALL use the same effective base. Stored configuration SHALL remain unchanged; minification, fingerprinting, and link checking SHALL continue to be omitted.

#### Scenario: Preview CDN configuration locally
- **WHEN** `assetPrefix: https://cdn.example.com/v2/media/` is set and a development build runs without a site base path
- **THEN** HTML SHALL contain owned asset URLs under `/assets/` without a CDN host or fingerprint, and `site.asset_prefix` SHALL return `/assets/`.

#### Scenario: Serve a local prefix under a subfolder
- **WHEN** `kiln serve` runs with site base path `/docs` and root-relative prefix `/media/`
- **THEN** a request for `/docs/media/css/site.css` SHALL return `<outputDir>/assets/css/site.css`; the server SHALL continue to reject foreign host headers and output-directory escapes.

#### Scenario: CDN fallback also applies to a plain development build with a base path
- **WHEN** `kiln build` runs without a production option with site base path `/docs` and `assetPrefix: https://cdn.example.com/v2/media/`
- **THEN** `site.asset_prefix` SHALL return `/docs/assets/`, all owned asset references SHALL use that local base, and configuration SHALL retain the original CDN prefix.

#### Scenario: Preview CSS with a local custom prefix
- **WHEN** a development build with site base path `/docs` and `assetPrefix: /media/` produces CSS with local `url()` or `@import` targets
- **THEN** owned CSS targets SHALL be reachable through `/docs/media/` without fingerprints; minification and link checking SHALL continue to be omitted.

### Requirement: Routing changes during serve require a restart
During `serve`, Kiln SHALL detect changes to configured `assetPrefix` or effective site base path since startup before a new build and issue a clear restart message through the existing error channel. Kiln SHALL neither publish new output under different routes nor silently change the previous routing base.

#### Scenario: Change the prefix during preview
- **WHEN** configuration changes from `/media/` to `/files/` during `serve`
- **THEN** Kiln SHALL reject that rebuild before file output, display a restart request, and continue serving the last output under `/media/`.

#### Scenario: Restore configuration or restart the server
- **WHEN** the original routing configuration is restored or `serve` is restarted with the new valid configuration
- **THEN** preview SHALL build again with the respective valid routing base; ordinary content/CSS changes without routing changes SHALL continue to trigger live reload or CSS hot swap.
