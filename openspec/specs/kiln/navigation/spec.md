# kiln/navigation Specification

## Purpose
This specification describes the navigation tree automatically generated from content directories, breadcrumb data, and section URLs.

## Requirements
### Requirement: Navigation Tree per Collection
Engobe SHALL provide a navigation tree for each collection from its published content items and their directories; an optional `parent` entry replaces an item's direct structural parent.

#### Scenario: Items in Sections
- **WHEN** a published page is in a collection subdirectory and has no `parent` set
- **THEN** the root-level Scriban binding `navtree.<collection>` contains the corresponding section node and the page as a child.

#### Scenario: Unpublished Items
- **WHEN** an item is not among the published items
- **THEN** it is not included in the navigation tree.

#### Scenario: Parent Override
- **WHEN** a published page sets `parent` to the collection-relative `RelativePath` of another published page in the same collection
- **THEN** it appears as a child of that page in root-level `navtree.<collection>` instead of under its structural directory parent; empty section nodes are not displayed.

#### Scenario: Unparented Children Keep Their Structure
- **WHEN** a page without `parent` is in the same section as a page with a parent override
- **THEN** the page without an override remains under its structural section node.

#### Scenario: Parent Override by Stable ID
- **WHEN** a published page sets `parent` to the stable UUID `id` of another published page in the same collection
- **THEN** it appears as a child of the referenced item in root-level `navtree.<collection>`, regardless of the target's path, slug, or URL.

### Requirement: Sibling Ordering
Engobe SHALL sort navigation nodes by ascending `weight` and, for equal weights, alphabetically by title without regard to case.

#### Scenario: Equal Weights
- **WHEN** siblings have the same `weight`
- **THEN** they appear in alphabetical title order.

### Requirement: Breadcrumb Ancestors
Engobe SHALL provide `page.ancestors` as an ordered list containing the collection root and the current page's parent sections.

#### Scenario: Page in Nested Section
- **WHEN** a theme renders `page.ancestors` for a page in multiple directory levels with no effective parent override
- **THEN** the list contains the collection and its sections from outermost to innermost, each with `title` and `url`, but not the current page itself.

#### Scenario: Breadcrumbs Follow Parent Override
- **WHEN** a theme renders `page.ancestors` for a page whose `parent` chain differs from the directory structure
- **THEN** the list contains the collection root and effective navigation ancestors from the root through the parent page, each with `title` and `url`, but not the current page itself.

### Requirement: Navtree Section URLs
Engobe SHALL provide section nodes as directory URLs with a trailing slash and respect the configured base path.

#### Scenario: Site in Subdirectory
- **WHEN** `baseUrl` contains a path such as `/site`
- **THEN** section URLs contain that prefix, for example `/site/docs/api/`.

#### Scenario: Permalink Override in a Section
- **WHEN** Engobe derives a section URL from items with and without a `url` override
- **THEN** it prefers an item without an override; if all items have an override, an overridden item serves as the fallback.

### Requirement: Active Navtree Path
Engobe SHALL provide each navtree node with `is_active` and `is_ancestor` relative to the current page's URL.

#### Scenario: Active Page and Ancestor Section
- **WHEN** a node has the same URL as the current page
- **THEN** `is_active` is true; a non-active node has `is_ancestor` true if its URL is a prefix of the current URL.

#### Scenario: URLs with Base Path
- **WHEN** the page is rendered under a configured base path
- **THEN** Engobe compares node and page URLs with the base path applied.

#### Scenario: Parent Page Is an Ancestor
- **WHEN** a page is placed through `parent` under a page with a non-prefix URL
- **THEN** `is_ancestor` is true for the parent page and all further effective navtree ancestors even if their URLs are not prefixes of the current URL.

#### Scenario: Former Section Is Not an Ancestor
- **WHEN** an item is moved by `parent` out of its previous directory section and that section is no longer in its effective parent chain
- **THEN** `is_ancestor` is false for that section unless its URL independently is a prefix of the current URL.

### Requirement: Page Section Context
Engobe MUST provide `page.section` for templates as the current page's structural directory context.

#### Scenario: Page in Nested Section
- **WHEN** a page in a nested section is rendered
- **THEN** `page.section` contains the deepest structural section with `title`, `path` (collection-relative `SectionPath`), and `url` (with the base path applied); `title` is humanized like a navtree section title.

#### Scenario: Page at Collection Root
- **WHEN** a page without `SectionPath` is rendered
- **THEN** `page.section` is `null`.

#### Scenario: Parent Override Does Not Rewrite Structural Section
- **WHEN** a page in a section is rendered and its `parent` overrides the navigation hierarchy
- **THEN** `page.section` still describes its original structural directory section.

### Requirement: Parent References Are Unambiguous
Engobe MUST interpret `parent` as a case-sensitive collection-relative source path ending in `.md` or as the stable UUID `id` of a published item in the same collection.

#### Scenario: Resolve a Parent by Source Path
- **WHEN** `parent` contains the `RelativePath` of a published page in the same collection with the exact suffix `.md`
- **THEN** Engobe resolves the value to exactly that page, independently of slug and URL overrides.

#### Scenario: Resolve a Parent by Stable ID
- **WHEN** `parent` contains a UUID set as the `id` of exactly one published item in the same collection
- **THEN** Engobe resolves the reference independently of the target item's path, slug, and URL.

#### Scenario: Parent Form Is Unambiguous
- **WHEN** a `parent` value ends in exactly `.md`
- **THEN** it is interpreted exclusively as a case-sensitive path; otherwise it must be a UUID and is interpreted exclusively as `id`.

#### Scenario: Unknown, Unpublished, or Cross-Collection Parent
- **WHEN** `parent` does not unambiguously identify a published target in the same collection
- **THEN** the build aborts with a clear error message naming the source file and invalid value.

#### Scenario: Parent Path Is Case-Sensitive
- **WHEN** `parent` contains a path ending in `.md` whose case differs from the actual `RelativePath`
- **THEN** Engobe does not resolve the target and aborts the build with an error message naming the source file and path.

#### Scenario: Duplicate Parent ID
- **WHEN** the UUID referenced as `parent` is assigned to multiple items in the same collection
- **THEN** the build aborts with a clear error message naming the source file and UUID.

#### Scenario: Parent Cycle
- **WHEN** the resolved `parent` relationships contain a cycle
- **THEN** the build aborts before rendering with a clear error message naming the source file and invalid reference value.

#### Scenario: Serve Reports Parent Resolution Error
- **WHEN** `engobe serve` finds an invalid parent reference during a full rebuild
- **THEN** it displays the same build error, including source file and invalid value.

### Requirement: Manual Menus Remain Separate
Engobe SHALL provide the automatic navtree in addition to manually configured `menus`, without replacing menus.

#### Scenario: Theme Renders Header and Sidebar
- **WHEN** a theme needs manual header menus and a structural sidebar
- **THEN** it can use `menus` for manual navigation and `navtree.<collection>` for the page hierarchy.
