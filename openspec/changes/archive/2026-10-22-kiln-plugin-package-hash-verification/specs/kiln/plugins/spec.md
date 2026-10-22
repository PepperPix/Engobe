# Spec Delta

## ADDED Requirements

### Requirement: NuGet Plugin Package Hash Verification
Kiln MUST verify downloaded `.nupkg` bytes against the SHA-512 value from the configured NuGet V3 PackageBaseAddress `.nupkg.sha512` file for the exact package ID and version before opening or extracting the archive.

#### Scenario: Package Hash Matches
- **WHEN** `kiln plugin add` or `kiln plugin update` obtains the exact version's `.nupkg.sha512` value and the calculated hash matches
- **THEN** Kiln proceeds with existing package validation and extraction.

#### Scenario: Package Hash Mismatch
- **WHEN** the downloaded package bytes have a different SHA-512 hash than the available `.nupkg.sha512` value
- **THEN** Kiln aborts with a clear error naming package ID and version, without writing plugin content or a new lock entry.

#### Scenario: Existing Install During Failed Update
- **WHEN** `kiln plugin update` detects a mismatch between downloaded bytes and an available hash
- **THEN** Kiln leaves the existing plugin directory and lock entry unchanged.

#### Scenario: Installed Content Hash Remains Separate
- **WHEN** a package is permitted to install after hash verification
- **THEN** Kiln continues recording the installed directory's SHA-256 content hash in the plugin lock file, separate from the package SHA-512 hash.

### Requirement: Missing NuGet Package Hash Metadata
Kiln MUST clearly warn when an online NuGet source provides no SHA-512 hash for an exact plugin package version and MUST NOT install offline cached package bytes without their matching locally cached hash.

#### Scenario: Online Custom Feed Omits Hash
- **WHEN** a reachable custom feed provides package bytes but no `.nupkg.sha512` value for that exact package version
- **THEN** Kiln warns that integrity could not be verified, identifies the feed and package version, and proceeds with installation.

#### Scenario: Offline Package and Hash Cache
- **WHEN** Kiln installs from an offline cache containing the exact package bytes and matching `.nupkg.sha512` value
- **THEN** Kiln verifies the bytes and proceeds only when the hashes match.

#### Scenario: Offline Package Without Cached Hash
- **WHEN** Kiln can read offline cached package bytes but cannot read the matching local `.nupkg.sha512` value
- **THEN** Kiln warns that the package cannot be verified offline and aborts without installing it.

#### Scenario: Custom Feed Without Hash in Offline Mode
- **WHEN** a custom-feed package has no published hash and only offline package bytes are available
- **THEN** Kiln applies the offline rule, warns and aborts unless the matching SHA-512 value is locally cached.

#### Scenario: Hash Status Is Reported at Installation
- **WHEN** Kiln installs an online package without an available SHA-512 value
- **THEN** the `kiln plugin add` or `kiln plugin update` output clearly states that installation proceeded without package hash verification.
