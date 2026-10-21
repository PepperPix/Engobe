# kiln/build-modes Specification

## Purpose
This capability determines the development and production modes of a Kiln build and the optimization and validation steps that can be enabled in production.

## Requirements
### Requirement: Select the build environment
`kiln build` SHALL run in `development` mode by default and select `production` mode with `--production`, `-e production`, or the alias `--release`; `kiln serve` SHALL always build in development mode.

#### Scenario: Run development and production builds
- **WHEN** `kiln build` is started without a production option, with one of the production options, or `kiln serve` is started
- **THEN** Kiln SHALL use `development`, `production`, or always `development` for `serve`, respectively.

### Requirement: Configure production options
Kiln SHALL support the options `build.minify.css`, `js`, `html`, `svg`, `htmlAggressive`, `build.fingerprint`, `build.linkCheck`, and `assets.minifier` in `site.yaml` with camelCase configuration keys; production optimizations SHALL be enabled by default.

#### Scenario: Override production defaults
- **WHEN** a site explicitly disables individual `build` options or selects a minifier under `assets.minifier`
- **THEN** Kiln SHALL apply the specified values in the production build; an unknown minifier ID SHALL be reported as a build error with the available IDs.

### Requirement: Minify production assets
Kiln SHALL minify CSS, JavaScript, and SVG files in the asset prefix and rendered HTML files in production mode according to the respective minify options; on a minifier error, Kiln SHALL warn and retain the original content.

#### Scenario: Apply minification and HTML options
- **WHEN** a production build runs with the default minify options
- **THEN** Kiln SHALL minify CSS, JavaScript, SVG, and HTML; the `htmlAggressive` option SHALL also remove optional HTML tags when using the built-in NUglify minifier.

### Requirement: Generate asset fingerprints and references
Kiln SHALL generate enabled asset fingerprints from the final asset content after minification using SHA-256 and eight hexadecimal characters and rewrite local references in HTML and CSS to the hashed names.

#### Scenario: Fingerprint a minified asset
- **WHEN** a local asset eligible for fingerprinting is minified in production and fingerprinting is not disabled
- **THEN** Kiln SHALL compute the hash from the resulting asset bytes and rewrite the corresponding local HTML and CSS references to the hashed filename.

### Requirement: Check internal links in production output
Kiln SHALL apply enabled link checking after rendering and asset rewrites to root-relative `href` and `src` values in HTML and `url()` targets in CSS and report missing targets as build errors.

#### Scenario: Detect a dead internal target
- **WHEN** a checked HTML or CSS reference points to an internal resource that does not exist in the output directory
- **THEN** Kiln SHALL end the build with an error for the dead reference; external URLs SHALL not be checked.

### Requirement: Complete references during optimization
Kiln SHALL execute enabled production steps in this order: CSS/JavaScript/SVG minification, fingerprinting with reference rewriting, HTML minification, and link checking; in development, Kiln SHALL omit these production-specific steps.

#### Scenario: Leave development output unchanged
- **WHEN** a development build copies assets and renders pages
- **THEN** Kiln SHALL skip minification, fingerprinting, and link checking.

### Requirement: Optimize referenced images in production
Kiln SHALL optimize referenced content images from site `static/` and page bundles by default in production, but not theme or plugin assets or images whose only content reference is exempted by `imageOptimization: false` or that match `images.exclude`.

#### Scenario: Respect image scope and opt-out
- **WHEN** an image is referenced by a Markdown image in rendered content and image optimization is enabled
- **THEN** Kiln SHALL optimize the referenced site or bundle images, copy theme and plugin images unchanged, and copy exempted images without optimization unless another non-exempt content item also references them.

### Requirement: Keep image optimization configurable
Kiln SHALL control production image optimization through `images.enabled`, `images.maxWidth`, `images.quality`, `images.webp`, and `images.exclude`; the defaults SHALL be enabled, 2000 pixels, quality 82, and WebP disabled.

#### Scenario: Apply image parameters
- **WHEN** a site configures different image options or uses the defaults
- **THEN** Kiln SHALL apply the maximum width, encoder quality, and optional WebP conversion to referenced images.

### Requirement: Preserve the original file on image errors
Kiln SHALL warn on an image optimization error and copy the unoptimized source file to the output.

#### Scenario: Image optimizer cannot process the source file
- **WHEN** processing an image selected for optimization fails
- **THEN** Kiln SHALL issue a warning and continue the build with an unoptimized copy of the source file.
