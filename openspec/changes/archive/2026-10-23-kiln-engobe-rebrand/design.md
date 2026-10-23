# Design

## Context

See [proposal.md](proposal.md) for motivation. The inspected engine was already `2.0.0-beta.7`; older coordination notes still described 1.3. Release settings live inside `package.json`, including three project paths. Before this rebrand, the CLI package was `Kiln`, while Core's root namespace was `Kiln`, not `Kiln.Core`.

Before this change, persistent identity occurred in `.kiln/plugins.lock.json`, the image cache, and Pagefind caches. `PagefindBinaryProvider` read `KILN_PAGEFIND_PATH` and `KILN_PAGEFIND_CACHE_DIR`. Plugin discovery and classification separately used the tag and reserved prefix. Scaffold resources contained tool manifests, example posts, attribution, ignore rules, and deployment instructions.

## Goals / Non-Goals

**Goals:** Make current executable, package, API, and generated-project contracts consistently Engobe; preserve rendering and content semantics; provide a reproducible manual migration.

**Non-Goals:** No compatibility facade, automatic migration, unrelated 2.0 API cleanup, template-language redesign, or release-history reset.

## Decisions

1. **Perform a complete identity cut, not dual branding.** Use `Engobe`, `Engobe.Core`, `Engobe.Abstractions`, `engobe`, root namespace `Engobe`, `AddEngobe()` and `IEngobeBuilder`. Rename source/test directories and the solution to `Engobe.slnx`. Update resource lookup strings, assembly-qualified references, test fixtures and snapshots with semantic checks, not blind replacement.
2. **Separate user data from disposable caches.** New operations use `.engobe` only. The migration guide backs up the entire project, preserves deployment data owned by Studio, reinstalls trusted plugin packages into the new lock contract, and allows caches to be regenerated. Never copy a lock entry and change only its package ID or trust value. Keep `site.yaml`, content paths, plugin names and template syntax unchanged.
3. **Keep trust fail-closed.** Require `engobe-plugin`; first-party still requires both `Engobe.Plugin.` and feed-reported reservation. Do not assume a naming change grants reservation or ownership. Missing reservation produces community behavior, not a bypass.
4. **Preserve release lineage.** Continue existing tags and semantic-release state. Verify the actual next 2.0 prerelease and stable 2.0.0 without fabricated tags, forced version resets, or new package IDs masquerading as compatibility. Evaluate package validation for the new IDs explicitly: no Engobe baseline exists initially; retain validation tooling and establish a matching Engobe stable baseline after first publication rather than comparing unrelated package IDs.
5. **Keep planning identities stable during this change.** Existing `kiln/...` capability paths and active change IDs are not runtime contracts and remain stable. Update their current product wording and technical literals during implementation without duplicating specs or rewriting archived evidence. This avoids breaking delta matching.
6. **Use the `dotnet-suggest` request protocol without owning shell setup.** Keep `Spectre.Console.Cli` and handle its `[suggest:<cursor-position>]` request before normal command execution. Generate static command and option candidates from the configured command model and provide dynamic candidates where Engobe has safe local data. Do not ship shell scripts or modify shell profiles; users install/register `dotnet-suggest` and configure their shells.
7. **Persist explicit plugin sources.** `plugin add --source` accepts a local NuGet folder feed or a NuGet v3 service index URL. An explicit source is exclusive for lookup, inspection, and installation; it never falls back to nuget.org. Store local feed paths relative to the project root and remote service index URLs without embedded credentials. Obtain credentials through NuGet configuration or credential providers, never from the command value or lock. New lock entries from the default nuget.org source keep the value `nuget`, preserving compatibility; custom-source entries are reused by `plugin update`. Missing source metadata remains fail-closed for trust classification, and package integrity/consent checks do not change.

## Registry and Publishing Check

On 2026-10-23, NuGet flat-container, registration, and search endpoints returned no package for `Engobe`, `Engobe.Core`, or `Engobe.Abstractions`. The exact `Engobe` registration endpoint returned no prefix metadata, so public APIs cannot independently confirm prefix reservation before publication. The user confirms that NuGet publishing permissions have been corrected. The GitHub `RELEASE_ENABLED` variable is currently `true`, and the release workflow uses NuGet OIDC login. Do not publish from this implementation pass; preserve fail-closed classification until the feed reports the prefix as reserved.

## Risks / Trade-offs

- New package IDs are already owned or unavailable -> check NuGet ownership before merging the technical rename; stop rather than silently choosing different IDs.
- Repository rename invalidates trusted publishing and permissions -> pause release execution, update OIDC policies and repository-scoped integrations, verify before enabling.
- Broad replacement damages a URL, fixture, resource path, or author credit -> inventory tracked occurrences, use build/pack and generated-project checks, preserve genuine authorship and external references.
- A template pins an unpublished version -> test from a temporary local feed, then pin a verified public Engobe version before the stable cutover.
- A recorded local feed path is unavailable in another checkout -> fail the update explicitly using the recorded source; do not silently fall back to nuget.org.

## Migration Plan

1. Rebase implementation onto current `beta` after related work is resolved. Keep the pending stable release from publishing mixed identities.
2. Build and pack renamed engine packages locally without publishing. Test tool installation and scaffold/build/plugin workflows from an isolated feed.
3. Independently review; coordinate repository renames and NuGet policies before an approved Engobe beta publication.
4. Let Studio and plugins integrate against an exact available package version. Publish stable Engine 2.0 only after cross-repository acceptance and canonical documentation are ready.
5. Publish the manual upgrade guide, retaining old packages and Git tags. Where supported, mark old NuGet packages deprecated with replacement information after replacements exist.
6. Roll back deployments using the previous pinned tool/package versions and pre-migration project backup. Do not delete packages or retag a public release; package fixes require a new version.
