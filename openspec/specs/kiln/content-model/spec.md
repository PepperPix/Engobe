# kiln/content-model Specification

## Purpose
This capability describes how Kiln processes collections, Markdown content, frontmatter, permalinks, taxonomies, and cross-references and makes them available in templates.

## Requirements
### Requirement: Configure collections
`site.yaml` SHALL configure collections through `collections.<name>` with directory, permalink pattern, sorting (`date desc`, `date asc`, `title asc`, `weight asc`, or unchanged discovery order), feed, pagination, layout, taxonomy, and reference settings.

#### Scenario: Load collection settings
- **WHEN** a site configures a collection with `directory`, `permalink`, `sort`, `paginate`, `layout`, `taxonomies`, or `references`
- **THEN** Kiln SHALL use the settings for reading, URL generation, and rendering of that collection.

### Requirement: Read Markdown content and page bundles
Kiln SHALL read Markdown files with YAML frontmatter in collection directories and recursively discovered sections; a subdirectory with `index.md` SHALL be treated as one page-bundle item.

#### Scenario: Detect a section and page bundle
- **WHEN** a directory without `index.md` contains Markdown files and subdirectories, and a subdirectory contains `index.md`
- **THEN** Kiln SHALL read the Markdown files as items of their respective section, treat `index.md` as one bundle item, and associate its directly contained non-Markdown files as assets.

### Requirement: Provide frontmatter fields and custom values
Kiln SHALL adopt supported frontmatter fields such as `id`, `title`, `date`, `draft`, `layout`, `slug`, `description`, `url`, `weight`, `noIndex`, and `imageOptimization` and make other fields available in `page.extra`.

#### Scenario: Process known and additional fields
- **WHEN** a content item contains known frontmatter fields and additional YAML fields
- **THEN** Kiln SHALL assign the known values to the item, use `url` as a permalink override, and provide additional values through `page.extra`.

### Requirement: Filter drafts during builds
Kiln SHALL exclude draft items from rendered content pages and collection lists without the `--drafts` build option.

#### Scenario: Include or skip drafts
- **WHEN** `kiln build` is run without `--drafts`
- **THEN** Kiln SHALL skip draft items and not expose them in collection lists; with `--drafts`, Kiln SHALL include them in the build.

### Requirement: Generate teasers for content items
Kiln SHALL derive `page.teaser` from `description` when set, otherwise use the plain text before `<!--more-->`, and otherwise use a plain-text truncation to the collection word count with a default of 55.

#### Scenario: Apply the teaser fallback
- **WHEN** an item has no `description` but contains `<!--more-->` in its Markdown body
- **THEN** Kiln SHALL use the text before the marker as the teaser and remove the marker from the rendered full text.

### Requirement: Generate permalinks and section paths
Kiln SHALL generate permalinks from the collection pattern with `:slug`, `:collection`, and, when a date exists, `:year`, `:month`, and `:day`; nested items SHALL prepend their section path to the slug.

#### Scenario: Calculate the permalink for a nested item
- **WHEN** an item with slug `install` in section path `guides/advanced` uses the collection URL `/:slug/`
- **THEN** its URL SHALL be `/guides/advanced/install/`; a set frontmatter `url` value SHALL override the pattern.

### Requirement: Reject permalink collisions
Kiln SHALL report a build error if content items or generated virtual pages claim the same URL.

#### Scenario: Detect a duplicate target URL
- **WHEN** two items or an item and a generated index page produce the same URL
- **THEN** Kiln SHALL fail the build because of a permalink collision.

### Requirement: Evaluate generic taxonomies
Kiln SHALL evaluate taxonomies defined in `taxonomies` for all collections that declare the respective name under `taxonomies` and generate overview, term, and configured paginated pages.

#### Scenario: Aggregate a taxonomy across collections
- **WHEN** multiple collections use the same configured taxonomy with term values in their frontmatter fields
- **THEN** Kiln SHALL aggregate the values into terms across collections and provide the associated items for each term.

### Requirement: Expose taxonomy data in templates
Kiln SHALL provide `taxonomies.<name>.terms` with the term fields `name`, `slug`, `url`, `count`, and `items` and sort terms by descending item count.

#### Scenario: Use term data in a theme
- **WHEN** a theme accesses `taxonomies.tags.terms`
- **THEN** it SHALL be able to read each term's name, URL, count, and associated items.

### Requirement: Expose collection data globally
Kiln SHALL make `collections.<name>.items` and `collections.<name>.url` available in all page templates; items SHALL be sorted and provided without drafts, and a top-level `limit` filter SHALL limit a list to the specified count.

#### Scenario: Compose content from other collections
- **WHEN** a page template accesses `collections.posts.items`
- **THEN** it SHALL be able to use the sorted, published items of the posts collection to compose the page and limit them, for example, with `| limit 5`.

### Requirement: Resolve references between collections
Kiln SHALL resolve configured frontmatter references by slug in the target collection and expose successful results under `page.<referenzfeld>` with `title`, `url`, `slug`, and `extra`.

#### Scenario: Process resolved and missing references
- **WHEN** an item contains `author: marcel` and the collection configuration contains `references.author: authors`
- **THEN** Kiln SHALL provide `page.author` with the data of item `marcel`; a missing reference SHALL produce a warning and leave the resolved template field absent.

### Requirement: Respect no-index frontmatter
Kiln SHALL exclude items with `noIndex: true` from the sitemap, provide `page.no_index`, and insert a robots meta tag with `noindex, nofollow` into the rendered HTML when `</head>` is present.

#### Scenario: Exclude a page from search engines
- **WHEN** a content item contains `noIndex: true` in its frontmatter
- **THEN** Kiln SHALL omit it from `sitemap.xml` and inject the robots meta tag when a head is present; if `</head>` is missing, Kiln SHALL warn.
