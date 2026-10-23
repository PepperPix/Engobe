# kiln/plugins Specification

## Purpose
This specification describes the distribution and management of declarative content plugins through NuGet and their trust and lock behavior.

## Requirements
### Requirement: NuGet Plugin Package Format
Engobe SHALL accept plugin packages as Engobe plugins only when their NuGet manifest carries the `engobe-plugin` tag and their `content/` contains a `plugin.yaml`.

#### Scenario: Package with Required Metadata
- **WHEN** a package contains the tag and `content/plugin.yaml`
- **THEN** Engobe can install its plugin manifest and content files.

#### Scenario: Package without Tag or Manifest
- **WHEN** either required feature is missing
- **THEN** Engobe rejects installation; there is no override for packages without the tag.

### Requirement: Plugin Search
Engobe SHALL provide `engobe plugin search <query>` to search NuGet packages with the `engobe-plugin` tag.

#### Scenario: Search
- **WHEN** the user runs a search query
- **THEN** Engobe displays matching stable NuGet results with package ID, version, trust level, and description.

### Requirement: First-Party and Community Trust Levels
Engobe SHALL classify a package as first-party only when its ID starts with `Engobe.Plugin.` and the feed reports the prefix as reserved; other tagged packages are community packages.

#### Scenario: Feed Metadata Unavailable
- **WHEN** first-party metadata is unavailable
- **THEN** Engobe treats the package as community.

### Requirement: Plugin Information before Installation
Engobe SHALL provide `engobe plugin info <package-id> [--version <version>]` to display package information without installation.

#### Scenario: Inspect a Package
- **WHEN** a valid tagged package is inspected
- **THEN** Engobe displays information including owners, version, license, slots, shortcodes, file inventory, external hosts, and the computed content hash.

#### Scenario: External Hosts
- **WHEN** Engobe evaluates external hosts in package content
- **THEN** it labels the host list as a heuristic that cannot reliably detect URLs generated dynamically at runtime.

### Requirement: Community Consent on Add
Engobe SHALL run `engobe plugin add <package-id> [--version <version>] [--force] [--yes] [path]` with explicit consent before a community installation.

#### Scenario: Install a Community Plugin Interactively
- **WHEN** a community package is added without `--yes`
- **THEN** Engobe first displays package information and requires confirmation that defaults to no.

#### Scenario: Non-Interactive Installation
- **WHEN** the console is non-interactive and a community package is to be installed without `--yes`
- **THEN** Engobe aborts with instructions to run it again with `--yes`.

#### Scenario: Skip Consent
- **WHEN** the user passes `--yes` or `-y`
- **THEN** Engobe skips the community consent dialog.

#### Scenario: Installation Target
- **WHEN** a package is successfully added
- **THEN** Engobe copies the plugin content to `plugins/<name>` and does not automatically enable it in `site.yaml`.

### Requirement: Plugin Installation Lock
Engobe SHALL record NuGet installations in `.engobe/plugins.lock.json` with package ID, version, source, trust level, and SHA-256 content hash.

#### Scenario: Write a Lock Entry
- **WHEN** `plugin add` or a successful `plugin update` installs a plugin
- **THEN** Engobe writes the entry under the plugin name in the lock file.

#### Scenario: Local Content Changes
- **WHEN** the hash of the installed plugin directory differs from the lock entry
- **THEN** `engobe plugin list` marks the plugin as modified.

### Requirement: List Plugins
Engobe SHALL provide `engobe plugin list [path]` to list locally present plugins offline.

#### Scenario: Locked and Manual Installations
- **WHEN** the user lists installed plugins
- **THEN** Engobe displays package ID, version, source, and trust level for locked plugins and marks plugins without a lock entry as manual.

### Requirement: Plugin Updates
Engobe SHALL update through `engobe plugin update <name> [--all] [--force] [--yes] [path]` using lock entries and not automatically guess plugins without a lock entry.

#### Scenario: Update Available
- **WHEN** a locked plugin has a newer stable version
- **THEN** Engobe downloads the new version, installs it, and updates the lock entry.

#### Scenario: Significant Trust Change
- **WHEN** an update adds new external hosts or downgrades first-party status to community
- **THEN** Engobe requires confirmation before updating unless `--yes` is set.

#### Scenario: No Lock Entry
- **WHEN** the user wants to update a plugin without a lock entry
- **THEN** Engobe reports that it cannot be updated automatically.

### Requirement: Remove Plugins
Engobe SHALL provide `engobe plugin remove <name> [path] [--yes]` to remove the plugin directory and associated lock entry.

#### Scenario: Remove a Configured Plugin
- **WHEN** a plugin is enabled in `site.yaml` and the user removes it
- **THEN** Engobe removes the directory and lock entry, leaves `site.yaml` unchanged, and indicates that manual cleanup is needed.

### Requirement: NuGet Plugin Package Hash Verification
Engobe MUST verify downloaded `.nupkg` bytes against the SHA-512 value from the configured NuGet V3 PackageBaseAddress `.nupkg.sha512` file for the exact package ID and version before opening or extracting the archive.

#### Scenario: Package Hash Matches
- **WHEN** `engobe plugin add` or `engobe plugin update` obtains the exact version's `.nupkg.sha512` value and the calculated hash matches
- **THEN** Engobe proceeds with existing package validation and extraction.

#### Scenario: Package Hash Mismatch
- **WHEN** the downloaded package bytes have a different SHA-512 hash than the available `.nupkg.sha512` value
- **THEN** Engobe aborts with a clear error naming package ID and version, without writing plugin content or a new lock entry.

#### Scenario: Existing Install During Failed Update
- **WHEN** `engobe plugin update` detects a mismatch between downloaded bytes and an available hash
- **THEN** Engobe leaves the existing plugin directory and lock entry unchanged.

#### Scenario: Installed Content Hash Remains Separate
- **WHEN** a package is permitted to install after hash verification
- **THEN** Engobe continues recording the installed directory's SHA-256 content hash in the plugin lock file, separate from the package SHA-512 hash.

### Requirement: Missing NuGet Package Hash Metadata
Engobe MUST clearly warn when an online NuGet source provides no SHA-512 hash for an exact plugin package version and MUST NOT install offline cached package bytes without their matching locally cached hash.

#### Scenario: Online Custom Feed Omits Hash
- **WHEN** a reachable custom feed provides package bytes but no `.nupkg.sha512` value for that exact package version
- **THEN** Engobe warns that integrity could not be verified, identifies the feed and package version, and proceeds with installation.

#### Scenario: Offline Package and Hash Cache
- **WHEN** Engobe installs from an offline cache containing the exact package bytes and matching `.nupkg.sha512` value
- **THEN** Engobe verifies the bytes and proceeds only when the hashes match.

#### Scenario: Offline Package Without Cached Hash
- **WHEN** Engobe can read offline cached package bytes but cannot read the matching local `.nupkg.sha512` value
- **THEN** Engobe warns that the package cannot be verified offline and aborts without installing it.

#### Scenario: Custom Feed Without Hash in Offline Mode
- **WHEN** a custom-feed package has no published hash and only offline package bytes are available
- **THEN** Engobe applies the offline rule, warns and aborts unless the matching SHA-512 value is locally cached.

#### Scenario: Hash Status Is Reported at Installation
- **WHEN** Engobe installs an online package without an available SHA-512 value
- **THEN** the `engobe plugin add` or `engobe plugin update` output clearly states that installation proceeded without package hash verification.
