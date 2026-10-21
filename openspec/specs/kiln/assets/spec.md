# kiln/assets Specification

## Purpose
This capability organizes site, theme, plugin, and page-bundle assets in the output namespace and makes their URLs available to themes.

## Requirements
### Requirement: Output static assets in the asset namespace
Kiln SHALL write theme and site files from `static/` under `assets/` in the output directory; the default URL prefix `assetPrefix` is `/assets/`, and site files SHALL overwrite theme files with the same name with a warning.

#### Scenario: Site asset overwrites theme asset
- **WHEN** the theme and site provide the same relative asset file
- **THEN** Kiln SHALL output the site file in the asset namespace and report a collision warning.

### Requirement: Store plugin assets separately
Kiln SHALL output plugin files from `plugins/<name>/static/` under `assets/plugins/<name>/` in the output directory.

#### Scenario: Copy a plugin asset
- **WHEN** a plugin provides a file in its `static/` directory
- **THEN** Kiln SHALL make it available in that plugin's namespace.

### Requirement: Associate page-bundle assets with the item
Kiln SHALL copy non-Markdown files directly contained in a page-bundle directory to `assets/content/<collection>/<sectionPath>/<slug>/` and provide a `page_asset_url(filename)` function in templates.

#### Scenario: Reference an asset of a nested bundle
- **WHEN** a bundle item with section path `guides/advanced` and slug `install` directly contains an image `diagram.svg`
- **THEN** Kiln SHALL output the file under `assets/content/posts/guides/advanced/install/diagram.svg` and resolve `page_asset_url('diagram.svg')` to that path.

### Requirement: Provide asset URLs for templates
Kiln SHALL provide `asset_url(path)` and `plugin_asset_url(plugin, path)` as template functions and compose asset URLs with `assetPrefix` and a configured site base path.

#### Scenario: Link site and plugin files
- **WHEN** a theme calls `asset_url('css/style.css')` or `plugin_asset_url('search', 'js/index.js')`
- **THEN** Kiln SHALL produce the URL in the asset or plugin namespace, including the site base path.

### Requirement: Rewrite relative bundle image paths
Kiln SHALL rewrite relative image targets in page-bundle Markdown to that bundle's asset path during rendering and leave external or root-relative image URLs unchanged.

#### Scenario: Render a relative Markdown image
- **WHEN** a bundle body contains `![Diagramm](diagram.svg)`
- **THEN** Kiln SHALL set the image source in the rendered HTML to `/assets/content/<collection>/<sectionPath>/<slug>/diagram.svg`.
