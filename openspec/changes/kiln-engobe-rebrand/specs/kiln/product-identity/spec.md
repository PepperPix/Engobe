## Purpose

Define the Engobe identity exposed by the engine, command-line tool, generated projects, and manual upgrade documentation.

## ADDED Requirements

### Requirement: Engobe command and package identity
The engine SHALL publish the `Engobe` tool with command `engobe`, and libraries `Engobe.Core` and `Engobe.Abstractions`. CLI help, `dotnet-suggest` completion output, package metadata, and current product documentation SHALL use Engobe and the canonical website `https://engobe.studio`.

#### Scenario: Install and invoke the renamed tool
- **WHEN** a user installs a locally packed or published Engobe tool
- **THEN** `engobe --help` exposes the existing command families under the new name
- **AND** no `kiln` command alias is installed by that package.

#### Scenario: Complete an Engobe command through dotnet-suggest
- **WHEN** `dotnet-suggest` invokes the registered Engobe executable with a completion request and cursor position
- **THEN** Engobe returns matching command, option, and supported dynamic candidates
- **AND** completion output does not include shell-specific setup or the legacy `kiln` command.

### Requirement: Renamed public integration contract
The public engine API SHALL use Engobe namespaces and assembly names, including `AddEngobe()` and `IEngobeBuilder`, without compatibility types in Kiln namespaces. Existing engine operation semantics SHALL remain unchanged.

#### Scenario: Compile an embedding application
- **WHEN** an application references the Engobe libraries and registers `AddEngobe()`
- **THEN** it can build a site in-process without a CLI subprocess or Kiln package reference.

### Requirement: Engobe state and environment identity
Engine-owned project state SHALL use `.engobe` instead of `.kiln`. Environment overrides SHALL use the `ENGOBE_` prefix, including `ENGOBE_PAGEFIND_PATH` and `ENGOBE_PAGEFIND_CACHE_DIR`. Engobe SHALL NOT read or migrate legacy names automatically.

#### Scenario: Use new state and overrides
- **WHEN** a site is built with the new Pagefind overrides
- **THEN** the documented override precedence is preserved
- **AND** engine caches and plugin state use `.engobe`.

#### Scenario: Legacy state is present
- **WHEN** a project still contains `.kiln` or only legacy environment overrides
- **THEN** Engobe does not consume or overwrite that legacy state
- **AND** content in the project remains untouched by a compatibility migration.

### Requirement: Generated projects use Engobe
New projects and generated deployment instructions SHALL use the Engobe tool, verified Engobe package versions, `.engobe` ignore rules, Engobe attribution, and current repository/domain links. Their content schema, theme structure, and template syntax SHALL remain unchanged.

#### Scenario: Scaffold and build a new site
- **WHEN** `engobe new` scaffolds a supported template and its tools are restored
- **THEN** the generated project builds using `engobe build`
- **AND** current tool manifests, executable workflow commands, sample branding and product links contain no unintended Kiln identity.

### Requirement: Explicit manual upgrade documentation
The upgrade guide SHALL document backup, tool/package replacement, API renames, environment overrides, state transfer versus cache regeneration, plugin reinstallation, and rollback. It SHALL distinguish package history from new runtime contracts and SHALL NOT promise automatic migration.

#### Scenario: Upgrade a backed-up Kiln project
- **WHEN** a user follows the guide with an installed plugin and a pinned old tool
- **THEN** the resulting project uses the new tool and a regenerated Engobe plugin lock
- **AND** content and public URLs remain unchanged except explicitly documented branding
- **AND** the old tool and backup remain usable for rollback.
