# kiln/content-model Specification

## Purpose
This capability describes how Engobe processes collections, Markdown content, frontmatter, permalinks, taxonomies, and cross-references and makes them available in templates.

## Requirements
### Requirement: Configure collections
`site.yaml` SHALL configure collections through `collections.<name>` with directory, permalink pattern, sorting (`date desc`, `date asc`, `title asc`, `weight asc`, or unchanged discovery order), feed, pagination, layout, taxonomy, and reference settings.

#### Scenario: Load collection settings
- **WHEN** a site configures a collection with `directory`, `permalink`, `sort`, `paginate`, `layout`, `taxonomies`, or `references`
- **THEN** Engobe SHALL use the settings for reading, URL generation, and rendering of that collection.

### Requirement: Read Markdown content and page bundles
Engobe SHALL read Markdown files with YAML frontmatter in collection directories and recursively discovered sections; a subdirectory with `index.md` SHALL be treated as one page-bundle item. Inside this leaf bundle, Engobe SHALL not detect further content items or sections, even with nested `index.md`. All other content, including subdirectories, SHALL be assets, except `.md` files, hidden entries, and symlinks.

#### Scenario: Detect a section and page bundle
- **WHEN** a directory without `index.md` contains Markdown files and subdirectories, and a subdirectory contains `index.md`
- **THEN** Engobe SHALL read the Markdown files as items of their respective section, treat `index.md` as one bundle item, and associate its other files, including subdirectories with the defined exclusions, as assets.

#### Scenario: A leaf bundle remains one item without Markdown-subtree blocking
- **WHEN** `content/posts/outer/index.md`, `outer/child/index.md`, `outer/child/photo.png`, `outer/notes/article.md`, and `outer/notes/data.zip` exist
- **THEN** only the item from `outer/index.md` SHALL be created; the nested Markdown files SHALL neither create items nor be published, while `child/photo.png` and `notes/data.zip` are assets of the outer bundle.

#### Scenario: A normal nested section remains content
- **WHEN** `content/posts/guides/advanced/install/index.md` exists, but neither `guides/` nor `advanced/` has its own `index.md`
- **THEN** the install bundle SHALL be read with `SectionPath` `guides/advanced` and retain its own asset namespace.

### Requirement: Provide frontmatter fields and custom values
Engobe SHALL adopt supported frontmatter fields such as `id`, `title`, `date`, `draft`, `layout`, `slug`, `description`, `url`, `weight`, `noIndex`, and `imageOptimization` and make other fields available in `page.extra`.

#### Scenario: Process known and additional fields
- **WHEN** a content item contains known frontmatter fields and additional YAML fields
- **THEN** Engobe SHALL assign the known values to the item, use `url` as a permalink override, and provide additional values through `page.extra`.

### Requirement: Filter drafts during builds
Engobe SHALL exclude draft items from rendered content pages and collection lists without the `--drafts` build option.

#### Scenario: Include or skip drafts
- **WHEN** `engobe build` is run without `--drafts`
- **THEN** Engobe SHALL skip draft items and not expose them in collection lists; with `--drafts`, Engobe SHALL include them in the build.

### Requirement: Generate teasers for content items
Engobe SHALL derive `page.teaser` from `description` when set, otherwise use the plain text before `<!--more-->`, and otherwise use a plain-text truncation to the collection word count with a default of 55.

#### Scenario: Apply the teaser fallback
- **WHEN** an item has no `description` but contains `<!--more-->` in its Markdown body
- **THEN** Engobe SHALL use the text before the marker as the teaser and remove the marker from the rendered full text.

### Requirement: Generate permalinks and section paths
Engobe SHALL generate permalinks from the collection pattern with `:slug`, `:collection`, and, when a date exists, `:year`, `:month`, and `:day`; nested items SHALL prepend their section path to the slug.

#### Scenario: Calculate the permalink for a nested item
- **WHEN** an item with slug `install` in section path `guides/advanced` uses the collection URL `/:slug/`
- **THEN** its URL SHALL be `/guides/advanced/install/`; a set frontmatter `url` value SHALL override the pattern.

### Requirement: Reject permalink collisions
Engobe SHALL report a build error if content items or generated virtual pages claim the same URL.

#### Scenario: Detect a duplicate target URL
- **WHEN** two items or an item and a generated index page produce the same URL
- **THEN** Engobe SHALL fail the build because of a permalink collision.

### Requirement: Evaluate generic taxonomies
Engobe SHALL evaluate taxonomies defined in `taxonomies` for all collections that declare the respective name under `taxonomies` and generate overview, term, and configured paginated pages.

#### Scenario: Aggregate a taxonomy across collections
- **WHEN** multiple collections use the same configured taxonomy with term values in their frontmatter fields
- **THEN** Engobe SHALL aggregate the values into terms across collections and provide the associated items for each term.

### Requirement: Expose taxonomy data in templates
Engobe SHALL provide `taxonomies.<name>.terms` with the term fields `name`, `slug`, `url`, `count`, and `items` and sort terms by descending item count.

#### Scenario: Use term data in a theme
- **WHEN** a theme accesses `taxonomies.tags.terms`
- **THEN** it SHALL be able to read each term's name, URL, count, and associated items.

### Requirement: Expose collection data globally
Engobe SHALL make `collections.<name>.items` and `collections.<name>.url` available in all page templates; items SHALL be sorted and provided without drafts, and a top-level `limit` filter SHALL limit a list to the specified count.

#### Scenario: Compose content from other collections
- **WHEN** a page template accesses `collections.posts.items`
- **THEN** it SHALL be able to use the sorted, published items of the posts collection to compose the page and limit them, for example, with `| limit 5`.

### Requirement: Resolve references between collections
Engobe SHALL resolve configured frontmatter references by slug in the target collection and expose successful results under `page.<referenzfeld>` with `title`, `url`, `slug`, and `extra`.

#### Scenario: Process resolved and missing references
- **WHEN** an item contains `author: marcel` and the collection configuration contains `references.author: authors`
- **THEN** Engobe SHALL provide `page.author` with the data of item `marcel`; a missing reference SHALL produce a warning and leave the resolved template field absent.

### Requirement: Respect no-index frontmatter
Engobe SHALL exclude items with `noIndex: true` from the sitemap, provide `page.no_index`, and insert a robots meta tag with `noindex, nofollow` into the rendered HTML when `</head>` is present.

#### Scenario: Exclude a page from search engines
- **WHEN** a content item contains `noIndex: true` in its frontmatter
- **THEN** Engobe SHALL omit it from `sitemap.xml` and inject the robots meta tag when a head is present; if `</head>` is missing, Engobe SHALL warn.

### Requirement: Output a collection index without pagination
A collection with publishable items SHALL receive exactly one index page with all items included in the build when `paginate` is unset, `false`, or `0` and the theme provides a collection-specific template `<collection-name>-index.html`.

#### Scenario: Build an unpaginated collection with an index template
- **WHEN** a collection has multiple published items, no positive `paginate`, and a matching collection index template
- **THEN** Engobe SHALL output exactly one index page at the collection index URL with all those items and include that URL in the sitemap.

#### Scenario: Build a collection without an index template
- **WHEN** a collection has no positive `paginate` and no matching collection-specific index template
- **THEN** Engobe SHALL not derive an index page for that collection from generic `index.html` or `default.html` fallbacks.

#### Scenario: Configure an unpaginated collection as the homepage
- **WHEN** `home.collection` identifies a collection without positive `paginate`
- **THEN** Engobe SHALL continue to report the configuration error required by the homepage contract; the unpaginated index page SHALL not automatically be promoted to `/`.

#### Scenario: Output an existing homepage and collection index separately
- **WHEN** a site configures `home.page` and another collection has an enabled unpaginated index page
- **THEN** Engobe SHALL output the homepage at `/` and the collection index at its collection index URL; a URL collision SHALL remain a build error.
