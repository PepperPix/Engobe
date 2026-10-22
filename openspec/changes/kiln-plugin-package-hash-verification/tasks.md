# Tasks

## 1. Implement SHA-512 verification

- [x] 1.1 In `src/Kiln.Core/Services/NuGetPluginClient.cs`, retrieve the NuGet V3 `PackageBaseAddressResource` `.nupkg.sha512` value for the exact normalized package ID and version and compare the complete downloaded bytes before opening `ZipArchive`; `NuGetPluginClientTests` must cover Base64 SHA-512 match and mismatch.
- [x] 1.2 Abort on mismatch before any extraction with a clear error for package ID/version; `NuGetPluginClientSafetyTests` check no plugin directory for add and unchanged existing directory/lock for update.
- [x] 1.3 Represent a missing hash on a reachable online custom feed as a warning status in the shared installation path and permit installation; the warning must name source, package/version, and missing verification and appear in `PluginAddCommand`/`PluginUpdateCommand`; CLI tests check warning text and success.

## 2. Test offline cache and status behavior

- [x] 2.1 Extend the fake V3 test feed in `tests/Kiln.Core.Tests/Services/NuGetPluginClientTests.cs` with a PackageBaseAddress `.nupkg.sha512` sidecar; controlled tests for hash hit, missing online hash, and mismatch must verify exactly the same package bytes as the NuGet download.
- [x] 2.2 Add offline cache tests for package bytes plus locally cached hash and package bytes without a hash; offline installation is allowed only with a locally available matching hash, including custom feeds without a published hash file.
- [x] 2.3 `PluginAddCommand` and `PluginUpdateCommand` tests check that a warning appears for online installation without a hash, mismatch fails hard, and an offline cache miss produces no plugin/lock change; do not add persistent `info`/`list` markers or a lock schema change.
- [x] 2.4 Leave the existing `PluginContentHasher.ComputeDirectoryHash` SHA-256 value and `PluginLockEntry.ContentHash` unchanged; `PluginLockFileTests`/`PluginContentHasherTests` confirm that this is not the package SHA-512.

## 3. Validation in the target repository

- [x] 3.1 Run `rtk dotnet build` in `PepperPix/Kiln` with zero warnings/errors.
- [x] 3.2 Run the full `dotnet test` suite (without `rtk`) in `PepperPix/Kiln` and confirm all tests pass.
- [x] 3.3 Run `jb inspectcode Kiln.slnx -e=WARNING -f=Text` with zero findings.
- [x] 3.4 Run add/update integration against the fake V3 feed: hash match installs, online hash miss warns and installs, mismatch aborts without changes; offline hash hit installs and offline hash miss warns and aborts without changes.
- [ ] 3.5 Inspect `rtk git show --stat HEAD`; only expected Kiln code, test, and product documentation files may be included.
