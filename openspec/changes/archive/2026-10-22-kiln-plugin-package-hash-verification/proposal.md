# Proposal

## Why

`NuGetPluginClient` downloads `.nupkg` bytes through `FindPackageByIdResource.CopyNupkgToStreamAsync` and does not check a package hash before extraction; the existing SHA-256 hash protects only locally installed plugin content. The confirmed SHA-512 hash from the V3 PackageBaseAddress `.nupkg.sha512` file enables integrity checking before extraction without repeating the earlier assumption of a Registration `packageHash`, which is not available.

## What Changes

- Verify downloaded `.nupkg` bytes before opening/extracting against the SHA-512 value from the V3 PackageBaseAddress `.nupkg.sha512` file for the same package ID and version.
- Abort with a hard failure and without installation/lock changes when an available hash differs.
- Issue a clear warning for online feeds without an available hash and still install; offline, Kiln permits installation only with a matching locally cached hash.
- Retain the existing SHA-256 hash of the installed plugin directory.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `kiln/plugins`: Define the SHA-512 source, pre-extraction verification, mismatch abort, warning/installation behavior for a missing online hash, and stricter offline cache rule.

## Impact

- Target repository: `PepperPix/Kiln`; target branch: feature branch from `beta`.
- Affected areas: `Kiln.Core/Services/NuGetPluginClient.cs` (`PreparePackageAsync` and V3 PackageBaseAddress), `PluginAddCommand`, `PluginUpdateCommand`, NuGet client tests, and fake V3 test feed.
- Affected contracts and specifications: package integrity and the clarification dated 2026-09-05 that Registration `packageHash` is unavailable; separation of integrity checking from trust; current contract `openspec/specs/kiln/plugins/spec.md`.

### Out of scope

- No cryptographic signature/certificate verification, no new trust model, no plugin lock file schema change, and no change to the installed directory SHA-256.
- No persistent indication in `kiln plugin info`/`list` that a plugin was installed without a feed hash: that would require new lock/status data; the clear warning is issued during add/update.
