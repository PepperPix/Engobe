# kiln/menus Specification

## Purpose
This capability provides manually configured hierarchical menus with resolved page targets and active state for themes.

## Requirements
### Requirement: Define menus in site.yaml
Kiln SHALL load named menus from `site.yaml` with items containing `title`, `url` or `ref`, `external`, and recursive `children`.

#### Scenario: Read a menu structure
- **WHEN** `site.yaml` defines a menu with nested items
- **THEN** Kiln SHALL use the items, including their titles, targets, external flag, and submenus, for rendering.

### Requirement: Resolve menu references
Kiln SHALL resolve `ref` values in the format `<collection>/<slug>` to an item and `<collection>/` to the collection index; an invalid or missing reference target SHALL produce a build error.

#### Scenario: Use item and index references
- **WHEN** a menu refers to `pages/about` or `posts/`
- **THEN** Kiln SHALL resolve the references to the item URL or collection index, respectively; unknown targets SHALL fail the build.

### Requirement: Validate static menu URLs
Kiln SHALL check internal static menu URLs against known content and virtual pages and warn for unknown targets; items with `external: true` SHALL be exempt from this check.

#### Scenario: Handle internal and external URLs
- **WHEN** a menu item contains a non-external URL without a matching site target or a URL marked as external
- **THEN** Kiln SHALL warn for the unknown internal target and not validate the external URL against site pages.

### Requirement: Expose menu data and active state
Kiln SHALL make menus available in templates as `menus.<name>` with `title`, resolved `url`, `external`, `children`, and an `active` value calculated for the currently rendered URL context.

#### Scenario: Display the active entry
- **WHEN** a theme renders `menus.main` on a site page
- **THEN** it SHALL be able to use the menu URLs and each entry's current active state for navigation.

### Requirement: Reflect homepage promotion in menu references
Kiln SHALL resolve a reference to the index of a collection promoted to the homepage to its overridden index URL.

#### Scenario: Menu entry for a promoted collection
- **WHEN** `home.collection` activates a collection for `/` and a menu refers to `<collection>/`
- **THEN** Kiln SHALL set the menu URL to `/` instead of the original collection index URL.
