# Design

## Context

See `proposal.md` for motivation and `specs/kiln/plugins/spec.md` for the normative behavior. `NuGetPluginClient.PreparePackageAsync` resolves the version, downloads the `.nupkg` through `FindPackageByIdResource.CopyNupkgToStreamAsync` into a `MemoryStream`, and then opens the ZIP archive. `PluginAddCommand` and `PluginUpdateCommand` use the same installation path. The installed directory hash is independently a SHA-256 hash.

The clarification dated 2026-09-05 records that Registration `packageHash` is absent from the actual nuget.org API. The selected source is instead the `.nupkg.sha512` file of the V3 PackageBaseAddress resource.

## Goals / Non-Goals

**Goals:**
- Verify the same downloaded package bytes against the matching feed SHA-512 value before ZIP processing.
- Explicitly report a missing hash on online custom feeds but permit installation; allow offline use only with a matching locally cached hash.
- Fail closed on mismatch and protect `add`/`update` from partial installation.

**Non-Goals:**
- No repository signature or certificate chain verification; a SHA-512 value from the same source does not protect against compromise of that source.
- No permanent unverified marker in `plugin info`/`list`; this would require additional persistence/lock data and is not part of this change.

## Decisions

1. **Confirmed — hash source:** Retrieve the `.nupkg.sha512` value through `PackageBaseAddressResource` (obtained from the NuGet V3 service index) for exactly the same normalized package ID and version. Do not query the NuGet Registration API for `packageHash`, because that field is unavailable.
2. **Confirmed — compare before any archive processing:** Compute SHA-512 over all `.nupkg` bytes and compare in the NuGet `.nupkg.sha512` format (Base64-encoded digest bytes). Do not open the ZIP, read the manifest, or extract before a successful comparison. An available, differing hash is always a hard error with package ID and version.
3. **Confirmed — missing hash online:** If the feed is reachable but provides no hash for the exact package version, issue a clear warning with feed, package ID/version, and a notice of missing integrity verification, and still continue installation. Add and update must use the same warning path.
4. **Confirmed — offline cache:** Offline, Kiln can install only when the exact matching package bytes and their SHA-512 are locally cached; it compares them and continues only on a match. If the local hash is missing, abort installation and issue a clear warning that offline installation without a cached SHA-512 is not permitted. This also applies to custom-feed packages whose feed publishes no hash online: the online warning/installation exception is not an offline bypass.
5. **Confirmed — no permanent unverified marker:** Missing hash verification is indicated immediately in the `add`/`update` CLI warning. `info`/`list` do not show a persistent unverified marker in this iteration because that would require a lock/status model and migration.
6. **Confirmed — installed directory hash:** After an allowed installation, `PluginContentHasher.ComputeDirectoryHash` and the SHA-256 `ContentHash` in the lock file remain unchanged. It describes installed content and does not replace the package SHA-512 value.
7. **Confirmed — fake-feed tests:** A local fake V3 feed provides independently controllable `.nupkg` and sidecar hash responses; tests cover matching hash, mismatch, missing hash online, offline cache hit/miss, and unchanged add/update state.

## Risks / Trade-offs

- [Hash and package come from the same feed source] → Document this as package integrity checking, not an author/signature guarantee; retain the existing trust/consent model.
- [Custom feeds without sidecars permit unverified installation online] → Warn clearly before extraction and indicate the installation in the CLI result.
- [Sidecar or package may independently be absent from the offline cache] → The offline installation path requires both and aborts without a matching hash.
- [The NuGet cache does not necessarily provide a matching hash-file cache hit] → Fake-feed tests must ensure that a cache miss does not silently fall back to unverified installation.

## Migration Plan

No lock file or installation directory migration. Add and update immediately apply the new check. Existing plugins retain their SHA-256 directory hash. Installation with a warning remains possible for online reachable feeds without `.nupkg.sha512`; offline, package bytes and hash must be locally available.
