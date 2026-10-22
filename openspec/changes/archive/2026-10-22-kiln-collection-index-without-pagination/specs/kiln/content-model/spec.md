# Spec Delta

## ADDED Requirements

### Requirement: Output a collection index without pagination
A collection with publishable items SHALL receive exactly one index page with all items included in the build when `paginate` is unset, `false`, or `0` and the theme provides a collection-specific template `<collection-name>-index.html`.

#### Scenario: Build an unpaginated collection with an index template
- **WHEN** a collection has multiple published items, no positive `paginate`, and a matching collection index template
- **THEN** Kiln SHALL output exactly one index page at the collection index URL with all those items and include that URL in the sitemap.

#### Scenario: Build a collection without an index template
- **WHEN** a collection has no positive `paginate` and no matching collection-specific index template
- **THEN** Kiln SHALL not derive an index page for that collection from generic `index.html` or `default.html` fallbacks.

#### Scenario: Configure an unpaginated collection as the homepage
- **WHEN** `home.collection` identifies a collection without positive `paginate`
- **THEN** Kiln SHALL continue to report the configuration error required by the homepage contract; the unpaginated index page SHALL not automatically be promoted to `/`.

#### Scenario: Output an existing homepage and collection index separately
- **WHEN** a site configures `home.page` and another collection has an enabled unpaginated index page
- **THEN** Kiln SHALL output the homepage at `/` and the collection index at its collection index URL; a URL collision SHALL remain a build error.
