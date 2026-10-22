# kiln/homepage Specification

## Purpose
This capability controls which source Kiln renders at the site's root URL: a single Markdown page or a collection index.

## Requirements
### Requirement: Configure an unambiguous homepage source
A configured `home` block in `site.yaml` SHALL contain exactly one of `page` or `collection`; an unknown collection or a missing `home.page` file SHALL fail the build.

#### Scenario: Validate homepage configuration
- **WHEN** `home` contains both options, neither option, an unknown collection, or a nonexistent page path
- **THEN** Kiln SHALL report the invalid configuration as an error.

### Requirement: Render a single Markdown page as the homepage
When `home.page` is set, Kiln SHALL render the specified Markdown file as a published content item at the root URL `/` (or a draft when included in the build) and make the global collection model available in the template context.

#### Scenario: Generate a standalone homepage
- **WHEN** `home.page` points to an existing Markdown file
- **THEN** Kiln SHALL provide its rendered page as the root output `index.html` and allow that page's templates to access `collections`.

### Requirement: Promote a collection index to the homepage
When `home.collection` points to a collection with positive `paginate` and at least one item included in the build, Kiln SHALL output its index page at `/` instead of the index URL derived from the permalink, without changing individual item permalinks.

#### Scenario: Move a blog collection to the root
- **WHEN** `home.collection: posts` is configured with positive `paginate` for the collection
- **THEN** Kiln SHALL output the first index page at `/` and subsequent pages at `/page/<nummer>/`, while individual posts retain their collection permalinks.

### Requirement: Report a missing homepage as a root warning
When `site.yaml` defines no `home` block, Kiln SHALL issue a warning that the site root `/` is empty and not silently report the build as fully rendered without that notice.

#### Scenario: Build a site without homepage configuration
- **WHEN** a valid site project without a `home` block is built
- **THEN** the build SHALL contain the warning ``site root `/` is empty`` and otherwise remain successful.

#### Scenario: Configure an explicit homepage
- **WHEN** a valid `home.page` or `home.collection` block is configured
- **THEN** Kiln SHALL not issue the missing-homepage warning.
