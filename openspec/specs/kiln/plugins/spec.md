# kiln/plugins Specification

## Purpose
This specification describes the distribution and management of declarative content plugins through NuGet and their trust and lock behavior.

## Requirements
### Requirement: NuGet Plugin Package Format
Kiln SHALL accept plugin packages as Kiln plugins only when their NuGet manifest carries the `kiln-plugin` tag and their `content/` contains a `plugin.yaml`.

#### Scenario: Package with Required Metadata
- **WHEN** a package contains the tag and `content/plugin.yaml`
- **THEN** Kiln can install its plugin manifest and content files.

#### Scenario: Package without Tag or Manifest
- **WHEN** either required feature is missing
- **THEN** Kiln rejects installation; there is no override for packages without the tag.

### Requirement: Plugin Search
Kiln SHALL provide `kiln plugin search <query>` to search NuGet packages with the `kiln-plugin` tag.

#### Scenario: Search
- **WHEN** the user runs a search query
- **THEN** Kiln displays matching stable NuGet results with package ID, version, trust level, and description.

### Requirement: First-Party and Community Trust Levels
Kiln SHALL classify a package as first-party only when its ID starts with `Kiln.Plugin.` and the feed reports the prefix as reserved; other tagged packages are community packages.

#### Scenario: Feed Metadata Unavailable
- **WHEN** first-party metadata is unavailable
- **THEN** Kiln treats the package as community.

### Requirement: Plugin Information before Installation
Kiln SHALL provide `kiln plugin info <package-id> [--version <version>]` to display package information without installation.

#### Scenario: Inspect a Package
- **WHEN** a valid tagged package is inspected
- **THEN** Kiln displays information including owners, version, license, slots, shortcodes, file inventory, external hosts, and the computed content hash.

#### Scenario: External Hosts
- **WHEN** Kiln evaluates external hosts in package content
- **THEN** it labels the host list as a heuristic that cannot reliably detect URLs generated dynamically at runtime.

### Requirement: Community Consent on Add
Kiln SHALL run `kiln plugin add <package-id> [--version <version>] [--force] [--yes] [path]` with explicit consent before a community installation.

#### Scenario: Install a Community Plugin Interactively
- **WHEN** a community package is added without `--yes`
- **THEN** Kiln first displays package information and requires confirmation that defaults to no.

#### Scenario: Non-Interactive Installation
- **WHEN** the console is non-interactive and a community package is to be installed without `--yes`
- **THEN** Kiln aborts with instructions to run it again with `--yes`.

#### Scenario: Skip Consent
- **WHEN** the user passes `--yes` or `-y`
- **THEN** Kiln skips the community consent dialog.

#### Scenario: Installation Target
- **WHEN** a package is successfully added
- **THEN** Kiln copies the plugin content to `plugins/<name>` and does not automatically enable it in `site.yaml`.

### Requirement: Plugin Installation Lock
Kiln SHALL record NuGet installations in `.kiln/plugins.lock.json` with package ID, version, source, trust level, and SHA-256 content hash.

#### Scenario: Write a Lock Entry
- **WHEN** `plugin add` or a successful `plugin update` installs a plugin
- **THEN** Kiln writes the entry under the plugin name in the lock file.

#### Scenario: Local Content Changes
- **WHEN** the hash of the installed plugin directory differs from the lock entry
- **THEN** `kiln plugin list` marks the plugin as modified.

### Requirement: List Plugins
Kiln SHALL provide `kiln plugin list [path]` to list locally present plugins offline.

#### Scenario: Locked and Manual Installations
- **WHEN** the user lists installed plugins
- **THEN** Kiln displays package ID, version, source, and trust level for locked plugins and marks plugins without a lock entry as manual.

### Requirement: Plugin Updates
Kiln SHALL update through `kiln plugin update <name> [--all] [--force] [--yes] [path]` using lock entries and not automatically guess plugins without a lock entry.

#### Scenario: Update Available
- **WHEN** a locked plugin has a newer stable version
- **THEN** Kiln downloads the new version, installs it, and updates the lock entry.

#### Scenario: Significant Trust Change
- **WHEN** an update adds new external hosts or downgrades first-party status to community
- **THEN** Kiln requires confirmation before updating unless `--yes` is set.

#### Scenario: No Lock Entry
- **WHEN** the user wants to update a plugin without a lock entry
- **THEN** Kiln reports that it cannot be updated automatically.

### Requirement: Remove Plugins
Kiln SHALL provide `kiln plugin remove <name> [path] [--yes]` to remove the plugin directory and associated lock entry.

#### Scenario: Remove a Configured Plugin
- **WHEN** a plugin is enabled in `site.yaml` and the user removes it
- **THEN** Kiln removes the directory and lock entry, leaves `site.yaml` unchanged, and indicates that manual cleanup is needed.
