# Engobe

[![CI](https://github.com/PepperPix/Engobe/actions/workflows/ci.yml/badge.svg)](https://github.com/PepperPix/Engobe/actions/workflows/ci.yml)

Engobe is a fast, extensible static site generator for .NET. It is distributed as a .NET global tool and focuses on a collection-based content model, Scriban templates, and CDN-friendly asset namespaces.

**Status:** Early development — see the roadmap and tests for current progress.

## Quick Start

Install the global tool (once published) and create a site:

```bash
dotnet tool install -g Engobe
engobe new my-site
```

New sites pin the creating Engobe package version in `.config/dotnet-tools.json`,
including its prerelease suffix. Restore that exact version with `dotnet tool restore`.

NuGet symbol packages contain portable managed PDBs. Native Windows PDBs from
runtime dependencies are excluded from tool packages; native runtime libraries
remain included. CI and the release workflow validate symbol packages before publishing.

Build and serve locally:

```bash
engobe build
engobe serve
```

## Asset URLs and CDN hosting

Engobe always writes site, theme, bundle, and plugin files to `<outputDir>/assets/` (normally `_site/assets/`). `assetPrefix` changes only their public URLs; Engobe does not upload files or configure cache headers. For example:

```yaml
baseUrl: https://example.org/docs/
assetPrefix: https://cdn.example.com/v2/media/
```

The CDN origin must map `https://cdn.example.com/v2/media/` to the **contents** of `_site/assets/`; Engobe does not create `_site/v2/media/`. Root-relative prefixes such as `/media/` receive the site's base path once, while absolute HTTP(S) prefixes retain their own origin and path. A `--base-url` override changes local asset URLs and the site origin used for feeds, but never changes an absolute CDN prefix.

Prefixes must not contain credentials, query strings, fragments, traversal segments, or raw or
percent-encoded backslashes. Production link checking compares URL paths case-sensitively on every
platform: `Logo.svg` does not match an output file named `logo.svg`.

Use `asset_url`, `plugin_asset_url`, and `page_asset_url` in themes. **BREAKING template change:** `site.asset_prefix` is now the effective public base, including the site base path and one trailing slash. Themes that previously composed `site.base_path + site.asset_prefix` must remove that extra composition; helper-based themes need no change. `site.yaml` uses camelCase `assetPrefix`; `asset_prefix` is not a configuration alias.

Production rewrites owned `src`, `href`, `srcset`, and `poster` attributes, plus CSS `url()` and both `@import` forms. Atom HTML content receives the same projection and absolute asset URLs (CDN origin or site origin for a local prefix). Query strings and fragments are preserved. Inline CSS/scripts, arbitrary example text, and JavaScript imports are not rewritten. Pagefind remains at the site-local `/pagefind/` path under the site's base path; only Engobe's own search script uses the asset prefix.

Fingerprinting hashes final asset bytes; imported CSS is finalized before its importing stylesheet. A CSS dependency cycle is a build error when fingerprinting is enabled; resolve the cycle or set `build.fingerprint: false`. Prefixing still applies when fingerprinting is disabled. Production link checking is offline: it checks owned local output files after rewriting, including CSS and Atom references, and never contacts a CDN. It skips foreign external URLs and does not verify CDN reachability, CORS, or cache behavior. Set `build.linkCheck: false` to disable existence checks.

In development, an absolute CDN prefix falls back to local `/assets/`; a root-relative custom prefix remains local and is served from `assets/`. References already written with the configured CDN base in supported HTML attributes, external CSS, or Atom HTML content also map to local preview URLs; unrelated hosts, ports, and neighboring paths remain unchanged. If the effective asset prefix or site base path changes while `engobe serve` is running, restart the server. Local asset namespaces may not overlap generated pages, Pagefind when enabled, or the live-reload route; choose another prefix if a build reports a collision.

## Features

- Collection-based content model with nested (recursive) content sections
- Taxonomies (tags, categories)
- Pagination helpers
- Plugin slots and extensible templates
- CDN-friendly `/assets/` namespacing
- Atom/RSS feeds and sitemap generation
- Local dev server with auto-rebuild (dev server)
- Reference documentation generation from OpenAPI specs and .NET XML docs
- Built-in search indexing (Pagefind)

## Project Layout

```
src/
├── Engobe.Core/    # Engine: content pipeline, templating, builders
└── Engobe.Cli/     # CLI: commands, dev server, tooling
tests/
├── Engobe.Core.Tests/
└── Engobe.Cli.Tests/
```

## API stability

The `Engobe.Core` and `Engobe.Abstractions` packages follow semantic versioning for the following supported surface:

- everything in `Engobe.Abstractions`
- all interfaces (including `ISiteConfigLoader`)
- all types in the `Engobe.Models` namespace
- `AddEngobe()` and `IEngobeBuilder`

Concrete classes in `Engobe.Services` (for example `SiteBuilder`, `DevServer`, `NuGetPluginClient`) are public for compatibility
but are **not** covered by this guarantee; they may change in any release. New members on interfaces are added as default interface members.

## CLI Reference

Common commands (exact names):

- `engobe new <site-name>` — create a new site scaffold
- `engobe build` — build the static site (output directory: `_site`)
- `engobe serve` — start local dev server with live-reload
- `engobe deploy <target>` — initialize deployment workflows; supported targets: `github-pages`, `azure-swa`
- `engobe gen docs --openapi <spec> --output <dir>` — generate reference documentation from an OpenAPI spec
- `engobe gen dotnet-xml --xml <path> --output <dir>` — generate reference documentation from a .NET XML documentation file
- `engobe search index [path] [--no-download] [--extended] [--output <dir>]` — build a Pagefind search index over the build output

Examples:

```bash
engobe new blog
engobe build
engobe serve

# Initialize GitHub Pages deployment workflow in the current project
engobe deploy github-pages

# Initialize Azure Static Web Apps workflow and staticwebapp.config.json
engobe deploy azure-swa

# Generate API reference docs from an OpenAPI spec into content/api
engobe gen docs --openapi openapi.json --output content/api

# Generate API reference docs from a .NET XML documentation file
engobe gen dotnet-xml --xml bin/Release/net10.0/MyLib.xml --output content/api-dotnet

# Build a search index over the default output directory
engobe search index
```

Important: the `deploy` command expects a target argument, e.g. `github-pages` or `azure-swa`.

## Shell completion

Engobe responds to the `dotnet-suggest` completion protocol with command, option, and local plugin-name suggestions. To enable completion, install and register Engobe with `dotnet-suggest` as described in the [.NET tab completion guide](https://learn.microsoft.com/dotnet/standard/commandline/how-to-enable-tab-completion). Shell-specific shim and profile setup is managed by the user; Engobe does not install `dotnet-suggest` or modify shell configuration.

For manual migration from Kiln, see [UPGRADING.md](./UPGRADING.md).

## Plugins find & install

Engobe can discover and install content plugins from NuGet packages marked with the `engobe-plugin` tag.

> Security warning: content plugins can inject arbitrary HTML and JavaScript into pages, so install only packages from trusted sources.

```bash
# Search public NuGet packages for Engobe plugins (shows the trust level)
engobe plugin search email-protect

# Inspect a package without installing it
engobe plugin info Engobe.Plugin.EmailProtect

# Install a plugin into the current project
engobe plugin add Engobe.Plugin.EmailProtect --version 1.0.0

# Install from a local folder feed (or a NuGet v3 service index URL)
engobe plugin add Engobe.Plugin.EmailProtect --source ../packages --yes

# Skip the confirmation for community plugins (scripts, CI)
engobe plugin add Contoso.Widget --yes

# Update a plugin that was installed through engobe plugin add
engobe plugin update email-protect

# Update all plugins recorded in .engobe/plugins.lock.json
engobe plugin update --all

# Remove a plugin folder and its lock entry
engobe plugin remove email-protect --yes

# List local plugins, their installation source and trust level
engobe plugin list
```

Each installed plugin is recorded in `.engobe/plugins.lock.json` with the package ID, installed version, source (`nuget`), trust level and a content hash. This file is intentionally project-local and versioned so `engobe plugin update` can resolve the correct package without guessing names.

Without `--source`, installation uses nuget.org and records `source: nuget`. An explicit source is used exclusively, without fallback. Local feed paths supplied to `plugin add` are relative to the current directory and are stored relative to the project root; remote feeds are stored as NuGet v3 service index URLs. Updates, including `--all`, reuse each plugin's recorded source. A missing local feed must be restored at its recorded location before updating. Do not put credentials, query parameters, or fragments in source URLs; configure feed credentials through NuGet configuration instead. Local feeds without prefix-reservation metadata are community sources and still require confirmation or `--yes`.

### Package integrity

For NuGet V3 sources, `plugin add` and `plugin update` compare all downloaded package bytes with the exact version's Base64 SHA-512 value from the PackageBaseAddress `.nupkg.sha512` sidecar before reading the ZIP archive. A mismatch or malformed hash aborts without changing the installed plugin or its lock entry, even with `--force`.

A reachable source without a sidecar may still install, but the command warns with the source, package ID, and version that installation proceeded without package hash verification. Local package sources without V3 hash metadata also report this warning. Offline installation requires both cached package bytes and a matching cached SHA-512 sidecar; cached package bytes alone are not sufficient, including packages from custom feeds that omit hashes online.

The lock file's `contentHash` remains the installed directory's SHA-256 hash, not the package SHA-512. No permanent unverified marker is added to the lock file or to `plugin info`/`plugin list`. A hash from the same source checks package integrity; it is not a signature, proof of authorship, or protection against a compromised source. The trust and confirmation rules below still apply.

### Trust model

- The `engobe-plugin` tag is required: packages without it are never installed, and there is no override.
- **First-party** plugins use the `Engobe.Plugin.` package ID prefix, which is reserved on nuget.org. `engobe plugin search` and `engobe plugin info` show a package as first-party only if the ID has the prefix and nuget.org reports the reservation. Without feed metadata (local feed, offline) a package counts as community.
- **Community** plugins are any other tagged package. `engobe plugin add` shows the package information and asks for confirmation (default: no). Without an interactive console, pass `--yes`.
- `engobe plugin info` lists publisher, license, slots, shortcodes, file inventory and the external hosts referenced by the plugin's scripts, styles and markup. The host list is a heuristic; URLs that a script builds at runtime are not detected.
- `engobe plugin update` asks only when the new version references additional external hosts or a first-party plugin has become a community package. `--yes` skips the question.
- An existing plugin directory is only replaced if it is tracked in the lock file and unmodified; use `--force` to overwrite it.

### Writing a plugin package

- Add the tag `engobe-plugin` to the package's `.nuspec`.
- Put `plugin.yaml` and all assets under `content/` in the package; ship built assets, Engobe does not run npm or resolve dependencies at install time.
- The `Engobe.Plugin.` ID prefix is reserved for first-party plugins; choose a different ID for your own package.

## Theme & Template Development

Engobe uses Scriban templates for layouts, partials and helpers. Basic concepts:

- Layouts: top-level HTML layout files
- Partials: reusable template fragments
- Slots: named insertion points for plugins and themes

The default theme includes an `api` layout (`themes/default/layouts/api.html`) and dedicated stylesheet (`themes/default/static/css/api.css`) designed for reference documentation. It features a sticky sidebar with collapsible namespaces, breadcrumbs, and badges for type kinds and package versions. When creating a site with `engobe new`, theme files are copied into the project directory; existing projects can copy `api.html` and `api.css` manually.

Refer to `templates/default` in the project for a minimal theme layout and examples.

## Content Plugin Shortcodes

Content plugins can declare optional `shortcodes:` entries in `plugin.yaml` and provide matching partials under `plugins/<name>/shortcodes/<shortcode-name>.html`.

Example plugin manifest:

```yaml
name: email-protect
version: "1.1.0"
slots:
  - body_end
shortcodes:
  - email
```

A shortcode partial for the `email` shortcode would live at:

```text
plugins/email-protect/shortcodes/email.html
```

Content authors then write inline calls in Markdown body content using the `{% ... %}` syntax:

```md
Contact {% email "hello@cscharf.de" %} today.
```

The shortcode partial receives its parsed arguments, a `plugin_asset_url` helper scoped to the active plugin, and the built-in `string.base64_encode` filter. The current v1 scope intentionally does not provide a full `page`/`collection` render context; the shortcode is resolved while content is read and before the main page-render pipeline starts.

Shortcodes are not resolved inside fenced code blocks (` ``` ` / `~~~`), so example snippets remain literal text.

Template data available to layouts and partials includes:

- `page.ancestors` — the breadcrumb chain for the current page, ordered root → … → parent (the
  current page itself is not included). Each entry is `{title, url}`. The chain includes the
  collection root and follows the effective section/page hierarchy, including parent overrides.
- `page.section` — the deepest structural directory section as `{title, path, url}`, or `null`
  for a page at the collection root. `path` is collection-relative, the title is humanized, and
  the URL includes the site base path. A parent override does not change this directory context.
- `page.no_index` — `true` when the current page has `noIndex: true` in front matter; the engine
  injects the robots meta tag automatically and does not require theme logic to re-render it.
- `navtree.<collection-name>` — the hierarchical navigation tree for a given collection. Each node
  is `{title, url, weight, is_active, is_ancestor, children}`, where `is_active`/`is_ancestor` are
  computed relative to the page currently being rendered. Effective ancestors are marked even
  when their URLs are not prefixes of the current URL; URL-prefix detection also remains active.

Navigation follows content directories by default. Set optional front matter `parent` to move
a page under another published page in the same collection:

```yaml
parent: guides/overview.md
```

An exact `.md` suffix selects a case-sensitive, collection-relative source path with `/`
separators, not a slug or URL. Otherwise, the value must be a UUID matching the target's `id`:

```yaml
parent: a869d67a-e4e1-48c5-a08c-75241df633d1
```

UUID references survive target file renames and URL changes. IDs must resolve uniquely within
the collection. The override replaces the direct directory parent; unused sections disappear,
while other pages keep their structure. Siblings remain sorted by ascending weight, then title.
Unknown, unpublished, cross-collection, ambiguous, or cyclic references fail the build before
rendering, with the source file and invalid value in the error. `engobe serve` reports the same
error during its full rebuilds.

Front matter can also set `noIndex: true` on individual pages to keep them out of the generated
`sitemap.xml` and force a `<meta name="robots" content="noindex, nofollow">` tag in the rendered
HTML. This is intentionally separate from `robots.txt` generation; the engine handles the page-level
no-index signal without creating a route-specific `Disallow` entry.

## Reference Documentation Generation

Engobe can generate reference documentation content from two sources:

- **OpenAPI specs** (`engobe gen docs --openapi <spec> --output <dir>`) — generates one content file
  per endpoint/operation.
- **.NET XML documentation** (`engobe gen dotnet-xml --xml <path> --output <dir>`) — generates one
  content file per documented type/member from a project built with
  `<GenerateDocumentationFile>true</GenerateDocumentationFile>`.

Both adapters share a three-tier ownership model for generated content files:

1. Generated files are written with `generated: true` and a `source_hash` in their frontmatter.
2. A file with `generated: false` is treated as **adopted** — it is never touched again by
   subsequent generation runs, even if the source spec/XML changes.
3. If a generated file was edited outside of Engobe (its content no longer matches `source_hash`) and
   the source changed again, Engobe does not overwrite it — it writes `<file>.md.regenerated` next to
   it instead, so no manual edits are lost.

```bash
engobe gen docs --openapi openapi.json --output content/api
engobe gen dotnet-xml --xml bin/Release/net10.0/MyLib.xml --output content/api-dotnet
engobe gen dotnet-xml --xml bin/Release/net10.0/MyLib.xml --assembly bin/Release/net10.0/MyLib.dll --output content/api-dotnet
engobe gen dotnet-xml --xml bin/Release/net10.0/MyLib.xml --assembly bin/Release/net10.0/MyLib.dll --package-version 1.0.0 --output content/api-dotnet
```

The compiler's XML file also contains comments of `internal` and `private` members. Pass
`--assembly <path>` (the compiled `.dll`) to restrict the output to publicly visible types and
members; each page then also names the kind of the type (`class`, `record`, `struct`, `interface`,
`enum`, `delegate`, `static class`). Members are matched by name only, so an `internal` overload that
shares its name with a public member is not told apart. The assembly is only read, not loaded.

Pass `--package-version <version>` to record the package version in the front matter (`extra.version`),
enabling themes to display the version of the documented package.

The output directory must be registered as a collection in `site.yaml` to be included in the build;
both commands print a warning if it is not. Use `layout: api` to render reference pages with the built-in reference documentation layout:

```yaml
collections:
  api:
    directory: content/api
    permalink: /api/:slug/
    sort: weight asc
    layout: api

menus:
  main:
    - title: API
      url: /api/
```

## Search

Engobe integrates [Pagefind](https://pagefind.app) for static full-text search.

Enable it in `site.yaml`:

```yaml
search:
  enabled: true      # default: false
  extended: false    # use the Pagefind extended binary (multilingual support)
  binaryPath: null   # explicit path to a Pagefind binary (relative to the project directory)
```

Build the index after `engobe build`:

```bash
engobe build
engobe search index
```

The Pagefind binary is resolved in this order:

1. `ENGOBE_PAGEFIND_PATH` environment variable, if set and the file exists. This is an explicit
   choice and the binary is never version-checked.
2. The `search.binaryPath` setting from `site.yaml`. Relative paths are resolved against the project
   directory (the folder containing `site.yaml`). Like `ENGOBE_PAGEFIND_PATH`, this is an explicit
   choice: the binary is not version-checked, and a path that does not exist is an error (Engobe does
   not fall back to `PATH`, the cache or a download).
3. The system `PATH`. A binary found there is only used if `--version` reports Pagefind 1.5.0 or
   newer; otherwise it is skipped, and with `--no-download` the reason is included in the error if nothing else is found.
4. The local cache at `~/.engobe/tools/pagefind/<version>/` (override the cache root with
`ENGOBE_PAGEFIND_CACHE_DIR`).
5. Automatic download of the Pagefind version pinned by Engobe (currently 1.5.2) from the Pagefind
   GitHub releases, with SHA256 verification (use `--no-download` to disable this and fail
   instead). The archive is extracted to a temporary file and moved into the cache only when
   complete.

Binaries from `ENGOBE_PAGEFIND_PATH` or `search.binaryPath` are not version-checked, so make sure
they are Pagefind 1.5.0 or newer, the minimum version Engobe requires.

### Trust model

A downloaded binary comes from the official Pagefind GitHub release of the version pinned by Engobe.
The SHA256 checksum is fetched from the same release as the archive, so it protects against
corrupted or truncated downloads, but not against a compromised release or GitHub account. If you
cannot accept that, pass `--no-download` and provide the binary yourself, either through
`ENGOBE_PAGEFIND_PATH` or `search.binaryPath` (you are responsible for verifying it) or by installing
it through a channel you trust.

Search is disabled by default and `engobe serve` never triggers indexing or a download. The default
theme ships with an opt-in search UI that is self-guarded and requires no extra setup once search is
enabled and indexed.

Search indexing runs after the Core build. With search enabled, production link checking reports
and defers missing `/pagefind/pagefind.js` and `/pagefind/pagefind-ui.css` entrypoints under the
effective site base path. Other missing files remain errors. Run search indexing before publishing;
`engobe build` does this automatically unless `--no-search` is passed. With search disabled, neither
entrypoint is exempt from checking.

## Nested Content Sections

Content collections are read recursively. For a given directory:

- A directory containing an `index.md` is a **leaf bundle** and is not recursed into further.
- Any other directory is a **section** and is read recursively.

URLs of nested content mirror the directory path, e.g. `guides/advanced/install.md` becomes
`/guides/advanced/install/`. Flat sites (no subdirectories) behave exactly as before.

## CI / CD Deployment Recipes

The Engobe CLI generates example workflows for common hosting platforms. Workflows produced by `engobe deploy` use the following build pattern:

- Restore tools: `dotnet tool restore`
- Build site: `engobe build`
- Publish directory: `_site`

Below are platform-specific recipes. CI YAML snippets must keep the same build commands used by the generated workflows.

### GitHub Pages (GitHub Actions)

Engobe creates a GitHub Actions workflow at `.github/workflows/deploy.yml` when you run `engobe deploy github-pages`. The generated workflow uses `dotnet tool restore` and `engobe build` and then uploads `_site` as the pages artifact.

Example (generated by the CLI):

```yaml
name: Deploy to GitHub Pages

on:
	push:
		branches: [main]

permissions:
	contents: read
	pages: write
	id-token: write

jobs:
	build-and-deploy:
		runs-on: ubuntu-latest
		steps:
			- uses: actions/checkout@v4
			- name: Setup .NET
				uses: actions/setup-dotnet@v4
				with:
					dotnet-version: '10.0.x'
			- name: Restore tools
				run: dotnet tool restore
			- name: Build site
				run: engobe build --base-url ${{ vars.SITE_URL || 'https://username.github.io/repo' }}
			- name: Upload artifact
				uses: actions/upload-pages-artifact@v3
				with:
					path: _site
			- name: Deploy to GitHub Pages
				id: deployment
				uses: actions/deploy-pages@v4
```

### Azure Static Web Apps (GitHub Actions)

Engobe generates `.github/workflows/azure-swa.yml` and a `staticwebapp.config.json` when you run `engobe deploy azure-swa`.

Key points:

- The generated job sets `app_build_command: "dotnet tool restore && engobe build"` to ensure tools are available during the build step.
- Output folder is `_site`.
- `staticwebapp.config.json` contains a `routes`/`navigationFallback` and cache headers for `/assets/*`.

Example (generated by the CLI):

```yaml
name: Deploy to Azure Static Web Apps

on:
	push:
		branches: [main]
	pull_request:
		branches: [main]

jobs:
	build-and-deploy:
		runs-on: ubuntu-latest
		steps:
			- uses: actions/checkout@v4
			- name: Setup .NET
				uses: actions/setup-dotnet@v4
				with:
					dotnet-version: '10.0.x'
			- name: Build and Deploy
				uses: Azure/static-web-apps-deploy@v1
				with:
					azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
					app_location: "/"
					output_location: "_site"
					app_build_command: "dotnet tool restore && engobe build"
					skip_api_build: true
```

`staticwebapp.config.json` example (generated):

```json
{
	"navigationFallback": {
		"rewrite": "/index.html",
		"exclude": ["/assets/*"]
	},
	"routes": [
		{
			"route": "/assets/*",
			"headers": {
				"Cache-Control": "public, max-age=31536000, immutable"
			}
		}
	]
}
```

### Azure DevOps (YAML)

Use the following snippet in an Azure Pipelines YAML to restore tools and build. Ensure the agent has .NET 10 SDK installed.

```yaml
pool:
	vmImage: 'ubuntu-latest'

steps:
- script: |
		dotnet tool restore
		engobe build
	displayName: 'Restore tools and build'
```

### Netlify

Set the build command to the following (Netlify UI or netlify.toml):

Build command:

```bash
dotnet tool restore && engobe build
```

Publish directory: `_site`

### Cloudflare Pages

Use a build command equivalent to Netlify. Ensure the selected environment supports .NET 10 SDK.

Build command:

```bash
dotnet tool restore && engobe build
```

Publish directory: `_site`

### Generic CI

Any CI system that can run `dotnet tool restore && engobe build` and publish `_site` as the artifact/website can be used.

## NuGet Package Description

Engobe — A fast, extensible static site generator for .NET. Collection-based content model, Scriban templates, plugin slots, CDN-friendly asset namespacing, built-in Atom feeds and sitemaps.

## Roadmap

- Markdown rendering, frontmatter, templates, theme system, sitemap, feeds, dev server, plugin API, image optimization.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for developer guidelines and tests. Keep commits focused and descriptive.

## License

[Apache-2.0](LICENSE)

Copyright 2026 Marcel Kummerow
