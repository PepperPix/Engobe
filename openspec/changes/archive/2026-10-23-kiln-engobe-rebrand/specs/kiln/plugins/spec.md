## MODIFIED Requirements

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

#### Scenario: Legacy prefix does not grant trust
- **WHEN** a package with an Engobe ecosystem tag uses the old `Kiln.Plugin.` prefix
- **THEN** Engobe does not classify it as first-party.

### Requirement: Plugin Installation Lock
Engobe SHALL record NuGet installations in `.engobe/plugins.lock.json` with package ID, version, source, trust level, and SHA-256 content hash. The default nuget.org source SHALL be recorded as `nuget`; an explicitly selected local feed SHALL be stored as a path relative to the project root, and an explicitly selected remote feed SHALL be stored as its non-secret NuGet v3 service index URL. Existing `nuget` entries SHALL continue to resolve to nuget.org.

#### Scenario: Write a Lock Entry
- **WHEN** `plugin add` or a successful `plugin update` installs a plugin
- **THEN** Engobe writes the entry under the plugin name in the lock file.

#### Scenario: Update from the recorded source
- **WHEN** `plugin update` installs a plugin with a custom source in its lock entry
- **THEN** Engobe uses that source without falling back to nuget.org.

#### Scenario: Local Content Changes
- **WHEN** the hash of the installed plugin directory differs from the lock entry
- **THEN** `engobe plugin list` marks the plugin as modified.

## ADDED Requirements

### Requirement: Explicit Plugin Package Source
`engobe plugin add` SHALL accept `--source <source>` for a local NuGet folder feed or a NuGet v3 service index URL. Without `--source`, Engobe SHALL use nuget.org. An explicit source SHALL be used exclusively for package lookup, inspection, and installation; Engobe SHALL NOT silently fall back to another source. Source credentials SHALL NOT be embedded in the command value or lock entry.

#### Scenario: Install from a local feed
- **WHEN** the user runs `engobe plugin add <package-id> --source <local-feed>`
- **THEN** Engobe installs the package from that feed and records the project-relative feed path in the lock entry.

#### Scenario: Install from a remote feed
- **WHEN** the user runs `engobe plugin add <package-id> --source <service-index-url>`
- **THEN** Engobe installs the package from that NuGet v3 source and records its non-secret service index URL in the lock entry.

#### Scenario: Update from a custom source
- **WHEN** the user updates a plugin whose lock entry contains a custom source
- **THEN** Engobe uses the recorded source and preserves it in the updated lock entry.

#### Scenario: Local source has no first-party metadata
- **WHEN** the selected feed does not provide prefix-reservation metadata
- **THEN** Engobe classifies the package as community and retains the existing confirmation requirement.
