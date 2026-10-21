# kiln/search Specification

## Purpose
This specification describes Kiln's Pagefind integration, resolution of the external binary, and the theme search component.

## Requirements
### Requirement: Pagefind Index
Kiln SHALL use Pagefind as the search indexer for the fully rendered site output.

#### Scenario: Build the Search Index
- **WHEN** search is enabled and Kiln executes the indexing step
- **THEN** Kiln starts the Pagefind binary with `--site <outputDir>` and Pagefind generates the index in the site output.

#### Scenario: Standard and Extended Builds
- **WHEN** search is enabled and `search.extended` is not set
- **THEN** Kiln uses `pagefind`; with `search.extended: true`, it uses `pagefind_extended`.

### Requirement: Search Configuration
Kiln SHALL read search configuration from `site.yaml` under `search` with the camelCase keys `enabled`, `extended`, and `binaryPath`.

#### Scenario: Search Disabled by Default
- **WHEN** `search` is absent or `search.enabled` is not set to `true`
- **THEN** search is disabled; `extended` defaults to `false`.

#### Scenario: Relative Binary Path
- **WHEN** `search.binaryPath` is specified as a relative path
- **THEN** Kiln resolves it relative to the project directory containing the site configuration.

### Requirement: Pagefind Binary Resolution
Kiln SHALL resolve the Pagefind binary in this order: `KILN_PAGEFIND_PATH`, `search.binaryPath`, `PATH`, Kiln cache, and, if downloads are allowed, automatic download.

#### Scenario: Explicit Environment Variable
- **WHEN** `KILN_PAGEFIND_PATH` points to an existing file
- **THEN** Kiln uses that file before all other sources.

#### Scenario: Explicit Configuration Path
- **WHEN** `search.binaryPath` points to an existing file
- **THEN** Kiln uses it without a version check; if the file is missing, Kiln reports an error and does not fall back to `PATH`, cache, or download.

#### Scenario: Binary on PATH
- **WHEN** no explicit path is used and a Pagefind binary is on `PATH`
- **THEN** Kiln uses a binary of version 1.5.0 or later.

#### Scenario: Cache or Download
- **WHEN** no earlier source provides a binary
- **THEN** Kiln uses the versioned binary from the user cache or, if allowed, downloads the pinned release for the target triple and checks its SHA-256 against the supplied release checksum.

### Requirement: Missing Search Binary Error
Kiln SHALL report the indexing step as failed when the Pagefind binary is missing or not executable and indicate manual installation sources or `KILN_PAGEFIND_PATH`.

#### Scenario: No Binary and Downloads Disabled
- **WHEN** Kiln finds no binary and the caller does not allow downloads
- **THEN** the result contains a clear error with installation instructions instead of reporting successful indexing.

### Requirement: Theme Search Component
The default theme SHALL display search only when `site.search.enabled` is set and use the Pagefind Component API for client-side search.

#### Scenario: Search Enabled
- **WHEN** a theme is rendered with `site.search.enabled` enabled
- **THEN** it contains a search field and results area, loads `pagefind/pagefind.js` and Kiln's search script, and renders at most five results as links.

#### Scenario: Search Disabled
- **WHEN** `site.search.enabled` is false
- **THEN** the default theme's search markup is not output.

#### Scenario: Site with a Base Path
- **WHEN** the site runs under a base path
- **THEN** the Pagefind module and search asset URLs contain the base path.
