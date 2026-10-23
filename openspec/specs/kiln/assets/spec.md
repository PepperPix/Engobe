# kiln/assets Specification

## Purpose
This capability organizes site, theme, plugin, and page-bundle assets in the output namespace and makes their URLs available to themes.

## Requirements
### Requirement: Output static assets in the asset namespace
Engobe SHALL write theme and site files from `static/` under `assets/` in the output directory; the default URL prefix `assetPrefix` is `/assets/`, and site files SHALL overwrite theme files with the same name with a warning.

#### Scenario: Site asset overwrites theme asset
- **WHEN** the theme and site provide the same relative asset file
- **THEN** Engobe SHALL output the site file in the asset namespace and report a collision warning.

The physical namespace SHALL always remain `<outputDir>/assets/` independently of `assetPrefix`; neither the full prefix nor its URL path component SHALL be used as the output directory.

#### Scenario: A CDN path is not a filesystem path
- **WHEN** `assetPrefix: https://cdn.example.com/v2/media/` is configured and a production build processes `static/css/site.css`
- **THEN** the file SHALL be output under `<outputDir>/assets/css/`, possibly with a fingerprint in the filename, and not under `https:`, `v2/media/`, or an absolute filesystem path.

### Requirement: Store plugin assets separately
Engobe SHALL output plugin files from `plugins/<name>/static/` under `assets/plugins/<name>/` in the output directory.

#### Scenario: Copy a plugin asset
- **WHEN** a plugin provides a file in its `static/` directory
- **THEN** Engobe SHALL make it available in that plugin's namespace.

### Requirement: Associate page-bundle assets with the item
Engobe SHALL recursively copy page-bundle files to `assets/content/<collection>/<sectionPath>/<slug>/`, preserving their relative structure, and provide a `page_asset_url(filename)` function in templates. Engobe SHALL omit only `*.md` files (case-insensitive extension), hidden files/directories (names starting with `.`), and symlinks; hidden directories and directory symlinks SHALL not be traversed.

#### Scenario: Reference an asset of a nested bundle
- **WHEN** a bundle item with section path `guides/advanced` and slug `install` directly contains an image `diagram.svg`
- **THEN** Engobe SHALL output the file under `assets/content/posts/guides/advanced/install/diagram.svg` and resolve `page_asset_url('diagram.svg')` to that path.

#### Scenario: Copy a multi-level asset subdirectory
- **WHEN** `content/posts/guides/install/index.md` has the files `images/screens/x.jpg` and `downloads/setup.zip` in subdirectories alongside it
- **THEN** Engobe SHALL copy them to `assets/content/posts/guides/install/images/screens/x.jpg` and `assets/content/posts/guides/install/downloads/setup.zip`; `page_asset_url('images/screens/x.jpg')` SHALL use the same relative structure in the public asset namespace.

#### Scenario: Markdown files do not block adjacent assets
- **WHEN** a bundle contains `child/index.md`, `child/photo.png`, `notes/article.md`, `notes/attachments/data.zip`, `images/manual/README.MD`, and `images/manual/b.jpg`
- **THEN** Engobe SHALL copy all three non-Markdown files with their relative subpaths and omit all listed Markdown files without blocking the surrounding directories.

#### Scenario: Do not publish hidden entries
- **WHEN** a bundle contains `.secret`, `images/.metadata`, `.private/picture.jpg`, and `images/.cache/x.jpg`
- **THEN** Engobe SHALL omit those files and the entire hidden directories while copying visible sibling assets.

#### Scenario: Do not copy or follow symlinks
- **WHEN** a bundle contains a symlink to a file or directory inside or outside its root
- **THEN** Engobe SHALL not copy the symlink as a bundle asset or traverse its target.

### Requirement: Provide asset URLs for templates
Engobe SHALL provide `asset_url(path)` and `plugin_asset_url(plugin, path)` as template functions and compose asset URLs from the effective public asset base: root-relative `assetPrefix` values with the configured site base path exactly once, absolute HTTP(S) values without an additional site base path.

#### Scenario: Link site and plugin files
- **WHEN** a theme calls `asset_url('css/style.css')` or `plugin_asset_url('search', 'js/index.js')`
- **THEN** Engobe SHALL produce the URL in the asset or plugin namespace, including the site base path for root-relative prefixes.

`page_asset_url(filename)` SHALL use the same base for `content/<collection>/<sectionPath>/<slug>/<filename>`. `site.asset_prefix` SHALL expose the effective public asset base, including any applied site base path and exactly one trailing slash; templates SHALL not need a second base-path composition.

#### Scenario: All helpers use the full CDN prefix
- **WHEN** production renders with `assetPrefix: https://cdn.example.com/v2/media/` and site base path `/docs`
- **THEN** `asset_url('css/site.css')` SHALL produce `https://cdn.example.com/v2/media/css/site.css`, `plugin_asset_url('demo', 'js/a.js')` SHALL produce `https://cdn.example.com/v2/media/plugins/demo/js/a.js`, and `page_asset_url('hero.jpg')` SHALL produce the corresponding full CDN URL under `content/`; `site.asset_prefix` SHALL be `https://cdn.example.com/v2/media/`.

#### Scenario: A base-URL override changes only local asset bases
- **WHEN** a production build uses `--base-url https://example.org/docs/` with `assetPrefix: /media/`
- **THEN** `asset_url('logo.svg')` SHALL return `/docs/media/logo.svg` and `site.asset_prefix` SHALL return `/docs/media/`; with an absolute CDN prefix, the same override SHALL leave its host and path unchanged.

#### Scenario: Use the effective prefix without duplicate base-path composition
- **WHEN** site base path `/docs` and `assetPrefix: /assets/` are configured and a theme uses `site.asset_prefix` for `logo.svg`
- **THEN** the exposed value SHALL be `/docs/assets/` and the reference SHALL be `/docs/assets/logo.svg`, without needing another base path.

### Requirement: Rewrite relative bundle image paths
Engobe SHALL rewrite bundle-relative image targets to canonical asset paths, normalizing `./` and internal `..` segments and preserving query/fragment suffixes. External URLs and root-relative targets outside the owned asset namespace SHALL remain unchanged. Hidden, symlink, excluded Markdown, and root-escaping targets SHALL not be rewritten; excluded `.md` targets SHALL trigger a warning. During output, owned canonical references SHALL be projected to the effective public asset base.

#### Scenario: Render a relative Markdown image
- **WHEN** a bundle body contains `![Diagramm](diagram.svg)`
- **THEN** the canonical image source SHALL be `/assets/content/<collection>/<sectionPath>/<slug>/diagram.svg` and the published source SHALL address the same relative asset file through the effective public base.

#### Scenario: Render a relative subdirectory image with a suffix
- **WHEN** a bundle body contains `![Bild](./images/screens/../x.jpg?v=2#detail)`
- **THEN** the image source SHALL point to the bundle asset path with subpath `images/x.jpg?v=2#detail`; in the default case without a base path, this is `/assets/content/<collection>/<sectionPath>/<slug>/images/x.jpg?v=2#detail`.

#### Scenario: Resolve an image adjacent to nested Markdown
- **WHEN** `child/index.md` and `child/photo.png` exist below a bundle and its outer Markdown body contains `![Bild](child/photo.png)`
- **THEN** Engobe SHALL rewrite the image source to the copied file under bundle asset path `child/photo.png`.

#### Scenario: Detect a missing eligible bundle image
- **WHEN** a bundle references `![Fehlt](images/missing.jpg)` without the file existing and production link checking is enabled
- **THEN** Engobe SHALL associate the relative image path with the bundle namespace and report the missing local asset reference as a build error.

#### Scenario: Diagnose a Markdown target used as an image reference
- **WHEN** the outer bundle body contains `![Kein Bild](child/index.md)`
- **THEN** Engobe SHALL leave the target unchanged, report a warning for the excluded `.md` file, and output no file or additional content page for it.

#### Scenario: Preserve the canonical reference index
- **WHEN** the same loaded items are used for builds with a local or absolute prefix
- **THEN** their canonical content references and the public asset reference index SHALL remain unchanged; only published output SHALL use the respective public base.

### Requirement: Link relative bundle downloads
Engobe SHALL rewrite relative Markdown links to existing non-Markdown files in the bundle asset tree to their asset path, preserving subdirectories and query/fragment suffixes. External/root-relative URLs, anchors, and targets outside the bundle root SHALL remain unchanged. Hidden/symlink targets SHALL not be resolved as assets. Links to excluded `.md` targets SHALL remain unchanged and trigger a warning.

#### Scenario: Link a download from a subdirectory
- **WHEN** a bundle contains `[Download](./downloads/setup.zip?download=1#file)` and the file is a visible, regular bundle asset
- **THEN** the rendered `href` SHALL point to its public bundle asset path with `downloads/setup.zip?download=1#file`.

#### Scenario: A Markdown target is not a download asset
- **WHEN** a bundle contains `[Kapitel](notes/article.md)` or `[Unterseite](child/index.md)`
- **THEN** Engobe SHALL leave the references unchanged, report a warning for each excluded `.md` file, and not publish those files.

#### Scenario: External links and anchors are preserved
- **WHEN** a bundle contains `[Extern](https://example.com/file.zip)` and `[Abschnitt](#details)`
- **THEN** Engobe SHALL leave the targets unchanged and not treat them as bundle downloads.

### Requirement: Configure the public asset prefix
Engobe SHALL accept `assetPrefix` in `site.yaml` as the public URL base: a root-relative path namespace other than `/` or an absolute HTTP(S) URL, default `/assets/`. The normalized base SHALL use exactly one trailing slash and preserve the scheme separator. The historical name `asset_prefix` SHALL not become a new alias.

#### Scenario: CDN prefix without a trailing slash
- **WHEN** `assetPrefix: https://cdn.example.com/assets` is set
- **THEN** Engobe SHALL use the same public base as for `https://cdn.example.com/assets/` and preserve the scheme separator.

### Requirement: Reject invalid asset bases
Engobe SHALL report relative prefixes without a leading slash, protocol-relative URLs, schemes other than HTTP(S), credentials, query/fragment in the prefix, raw or percent-encoded backslashes, and path-traversal segments as configuration errors. Query/fragment on individual asset references SHALL remain allowed.

#### Scenario: An invalid prefix is rejected
- **WHEN** `assetPrefix` contains, for example, `cdn.example.com/assets`, `//cdn.example.com/assets/`, `file:///private/`, `/../assets/`, or `https://cdn.example.com/assets/?token=1`
- **THEN** Engobe SHALL report a configuration error for `assetPrefix` before asset files are output.

#### Scenario: Reject backslashes before URI normalization
- **WHEN** a root-relative or absolute prefix contains a raw or percent-encoded backslash, including `/media\..\..\outside/` or `https://cdn.example.com/media%5Cimages/`
- **THEN** Engobe SHALL reject the prefix before URI parsing can normalize its path and before output.

### Requirement: Project content asset URLs to the public base
Engobe SHALL map owned canonical and already prefixed asset references in HTML attributes `src`, `href`, `srcset`, and `poster` to the effective public asset base. Subpaths, query/fragment, and `srcset` descriptors SHALL be preserved; foreign external URLs and content page links SHALL remain unchanged. Already correctly prefixed URLs SHALL not be prefixed again.

#### Scenario: Bundle Markdown is served from the CDN
- **WHEN** a bundle contains `![Hero](hero.jpg)` and production uses `assetPrefix: https://cdn.example.com/assets/`
- **THEN** its rendered image source SHALL start with `https://cdn.example.com/assets/content/`, while the file resides locally under `assets/content/` and configured image optimization still applies.

#### Scenario: A foreign image remains external
- **WHEN** content references `https://other.example/img.jpg` or `data:image/png;base64,AAAA`
- **THEN** Engobe SHALL leave those values unchanged and not associate them with the owned asset prefix.

#### Scenario: Project existing srcset and poster
- **WHEN** HTML contains a `srcset` with owned images, external or `data:` candidates and descriptors, and an owned `poster` asset
- **THEN** Engobe SHALL project only owned candidates and the owned poster to the effective base, preserve all descriptors and foreign candidates, and generate no additional image variants.

#### Scenario: Include theme, plugin, and shortcode HTML
- **WHEN** a theme, plugin slot, or shortcode produces owned asset references in supported attributes in the final HTML
- **THEN** the same projection SHALL apply; the canonical shortcode helper SHALL still be allowed to return `/assets/plugins/<plugin-name>/` without requiring an additional site or mode parameter.

#### Scenario: Do not rewrite arbitrary strings
- **WHEN** a document contains `/assets/logo.svg` as example text, in an inline script, or in inline CSS
- **THEN** Engobe SHALL not change that string as part of this asset rewrite.

### Requirement: Publish asset references in Atom content
Engobe SHALL project owned asset references in `src`, `href`, `srcset`, and `poster` in the HTML content of generated Atom feeds to absolute public URLs. For a local prefix, the site origin SHALL be added; for a CDN prefix, its absolute base SHALL apply. Feed paths, article links, and foreign references SHALL remain unchanged by this asset projection.

#### Scenario: Feed with a local prefix and base path
- **WHEN** the site is at `https://example.org/docs/`, uses `assetPrefix: /media/`, and feed content references `/assets/logo.svg`
- **THEN** its public asset URL SHALL be `https://example.org/docs/media/logo.svg`, possibly with a final fingerprint; the feed itself SHALL not move into the asset namespace.

#### Scenario: Output a feed after WebP and fingerprinting
- **WHEN** a feed image is converted to WebP and fingerprinted in production and an absolute CDN prefix applies
- **THEN** the feed HTML content SHALL use the absolute CDN URL of the hashed WebP file actually output and remain valid Atom XML with correctly escaped HTML.

#### Scenario: A development feed does not use the configured CDN host
- **WHEN** a development build generates a feed with an absolute CDN base configured
- **THEN** its owned asset references SHALL use absolute URLs from the site origin, site base path, and local `/assets/`, without fingerprints.

### Requirement: Keep local asset namespaces conflict-free
Engobe SHALL report overlaps of effective local asset namespaces with generated pages and reserved Pagefind/live-reload routes as errors before output. Absolute asset bases on another origin SHALL not count as local routes. URL namespace boundaries SHALL be compared by complete segments.

#### Scenario: A collection collides with a local prefix
- **WHEN** `assetPrefix: /media/` applies and a published content, index, taxonomy, or pagination page claims the same local base or a path within it
- **THEN** the build SHALL report an error with the asset base and colliding target before output, without silently preferring either page or asset.

#### Scenario: Do not take over a reserved namespace
- **WHEN** an effective local asset namespace overlaps the Pagefind namespace with search enabled or the live-reload endpoint
- **THEN** Engobe SHALL reject the colliding configuration; a merely similarly named neighboring path SHALL not constitute a collision.

#### Scenario: Distinguish a same-origin base from a foreign CDN origin
- **WHEN** an absolute production asset base has the same origin as the site and a namespace claimed by a generated page
- **THEN** the build SHALL report the collision; with a different CDN origin, the same path component SHALL not count as a collision with the site.

### Requirement: Preserve image selection independently of the asset prefix
Image optimization, `imageOptimization: false`, and `images.exclude` SHALL select or exclude the same local images as with `/assets/`, independently of the public base.

#### Scenario: Opt-out also applies to CDN output
- **WHEN** production uses an absolute CDN prefix and an image's only reference is from an item with `imageOptimization: false` or the image matches `images.exclude`
- **THEN** the file SHALL be copied without image optimization but use the same public URL base as other owned assets.

#### Scenario: Extended reference surfaces do not expand image selection
- **WHEN** an image is referenced only through additionally supported `srcset`, `poster`, CSS, or theme/plugin HTML
- **THEN** URL projection SHALL apply, but image optimization selection SHALL continue to follow the existing content-image contract.

### Requirement: Separate Pagefind from the asset prefix
Engobe SHALL continue to output the Pagefind module, index, chunks, and Pagefind CSS under `<outputDir>/pagefind/` and reference them in the default theme through the site base path, independently of the CDN prefix. Engobe's own search script in the asset namespace SHALL instead use the effective asset base.

#### Scenario: Search component with CDN and subfolder
- **WHEN** search is enabled, the site base path is `/docs`, and production uses an absolute CDN prefix
- **THEN** the default theme SHALL load Pagefind through `/docs/pagefind/pagefind.js` and its CSS under `/docs/pagefind/`, reference Engobe's `js/engobe-search.js` through the CDN prefix, and use the explicit site-local Pagefind module URL even from the CDN-loaded script.
