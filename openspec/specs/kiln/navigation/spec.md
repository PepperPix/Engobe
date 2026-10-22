# kiln/navigation Specification

## Purpose
This specification describes the navigation tree automatically generated from content directories, breadcrumb data, and section URLs.

## Requirements
### Requirement: Navigation Tree per Collection
Kiln SHALL provide a navigation tree for each collection from its published content items and their directories.

#### Scenario: Items in Sections
- **WHEN** a published page is in a collection subdirectory
- **THEN** `navtree.<collection>` contains the corresponding section node and the page as a child.

#### Scenario: Unpublished Items
- **WHEN** an item is not among the published items
- **THEN** it is not included in the navigation tree.

### Requirement: Sibling Ordering
Kiln SHALL sort navigation nodes by ascending `weight` and, for equal weights, alphabetically by title without regard to case.

#### Scenario: Equal Weights
- **WHEN** siblings have the same `weight`
- **THEN** they appear in alphabetical title order.

### Requirement: Breadcrumb Ancestors
Kiln SHALL provide `page.ancestors` as an ordered list containing the collection root and the current page's parent sections.

#### Scenario: Page in Nested Section
- **WHEN** a theme renders `page.ancestors` for a page in multiple directory levels
- **THEN** the list contains the collection and its sections from outermost to innermost, each with `title` and `url`, but not the current page itself.

### Requirement: Navtree Section URLs
Kiln SHALL provide section nodes as directory URLs with a trailing slash and respect the configured base path.

#### Scenario: Site in Subdirectory
- **WHEN** `baseUrl` contains a path such as `/site`
- **THEN** section URLs contain that prefix, for example `/site/docs/api/`.

#### Scenario: Permalink Override in a Section
- **WHEN** Kiln derives a section URL from items with and without a `url` override
- **THEN** it prefers an item without an override; if all items have an override, an overridden item serves as the fallback.

### Requirement: Active Navtree Path
Kiln SHALL provide each navtree node with `is_active` and `is_ancestor` relative to the current page's URL.

#### Scenario: Active Page and Ancestor Section
- **WHEN** a node has the same URL as the current page
- **THEN** `is_active` is true; if the current URL starts with the node URL and the node is not active, `is_ancestor` is true.

#### Scenario: URLs with Base Path
- **WHEN** the page is rendered under a configured base path
- **THEN** Kiln compares node and page URLs with the base path applied.

### Requirement: Manual Menus Remain Separate
Kiln SHALL provide the automatic navtree in addition to manually configured `menus`, without replacing menus.

#### Scenario: Theme Renders Header and Sidebar
- **WHEN** a theme needs manual header menus and a structural sidebar
- **THEN** it can use `menus` for manual navigation and `navtree.<collection>` for the page hierarchy.
