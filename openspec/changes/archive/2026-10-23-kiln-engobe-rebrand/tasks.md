# Tasks

## 1. Prepare the identity cut

- [x] 1.1 Start a feature branch from up-to-date `beta`, reconcile the pending 2.0 release and overlapping work, and verify branch ancestry and a clean scoped diff.
- [x] 1.2 Confirm ownership/availability of `Engobe`, `Engobe.Core`, `Engobe.Abstractions`, and `Engobe.Plugin.` reservation; record actual registry results and keep publishing gated until external configuration is ready.

## 2. Rename code and packaging

- [x] 2.1 Rename solution, source/test project paths, assemblies, namespaces, `AddKiln()`/builder contracts, embedded resource names, metadata and release project paths; verify all projects load and renamed public APIs compile in a small embedding test.
- [x] 2.2 Rename the CLI/package and help/diagnostics; implement the `dotnet-suggest` completion protocol with tests for `engobe` invocation, no installed legacy alias, and dynamic suggestions; update CLI/API documentation without shell-specific setup.
- [x] 2.3 Regenerate affected NuGet lock files with existing restore tooling, align package-validation settings with new IDs without removing validation, and inspect locally packed manifests/dependencies for unintended Kiln references.

## 3. Update project state and plugins

- [x] 3.1 Rename engine state/cache paths, output-directory protection, temporary naming and environment overrides; test `.engobe` protection and new override precedence, plus no legacy-state read or overwrite.
- [x] 3.2 Update plugin tags, search queries, ID resolution, trust prefix and lock paths; test reserved/unreserved prefixes, old-prefix classification, legacy-tag rejection, and successful install/list/update/remove without weakening consent or integrity checks.
- [x] 3.3 Write and execute a manual upgrade guide covering backup, overrides, content preservation, plugin customizations, regenerated locks/caches and rollback; verify it against a representative backed-up Kiln project.
- [x] 3.4 Add `engobe plugin add --source` for local folder feeds and NuGet v3 sources; persist custom sources in lock entries and reuse them for updates while preserving legacy `nuget` entries and existing trust/integrity checks.

## 4. Rebrand templates and current documentation

- [x] 4.1 Update every scaffold variant, tool manifest, generated workflow, example post filename/content, theme attribution, logo text, ignore rule and domain link; verify scaffold tests and expected generated filenames.
- [x] 4.2 Update README/contribution/API examples, current OpenSpec context and product wording, keeping capability paths and delta requirement matching stable; classify every remaining tracked Kiln occurrence as a necessary legacy reference or a defect.
- [x] 4.3 Update CI/release paths and metadata; test packaging with an isolated feed and verify the calculated next beta/stable version using a non-publishing release calculation, retaining existing tags.

## 5. Integration validation and handoff

- [x] 5.1 Run `rtk dotnet build` with zero warnings/errors, `dotnet test` without rtk using the repository's TUnit parallelism, and `rtk run jb inspectcode Engobe.slnx -e=WARNING -f=Text` with zero findings; distinguish any pre-existing failure.
- [x] 5.2 Install packed Engobe tools in an isolated directory; execute scaffold, tool restore, build, preview HTTP readiness, a `dotnet-suggest` completion request, and EmailProtect installation from a local feed using `--source`; verify the recorded source, rendered output and generated deployment commands, then stop only the helper process started and remove only named temporary outputs.
- [x] 5.3 Integrate exact package versions with Studio and both consumer sites, verify no accidental schema/URL changes, and run strict OpenSpec validation.
- [x] 5.4 Inspect staged paths/diff and `rtk git show --stat HEAD` after the implementation commit; verify no secrets, accidental generated files, provenance markers or unrelated changes, then hand off for an independent review.

## Verification notes

- Release build: zero warnings/errors. Tests: 826 passed, zero failed/skipped. InspectCode: zero findings. Normal package validation remains enabled and passes without an override; obsolete Kiln-baseline suppressions were removed.
- Isolated consumer builds preserve all 55 website and 22 blog HTML routes, configuration and content hashes. Blog search and plugin assets build successfully. Studio uses exact Engobe library versions; the user accepted its initial smoke test and postponed the remaining Studio-specific manual checks.
- A locally packed `2.0.0-beta.8+release-smoke` tool creates a manifest pinned to `2.0.0-beta.8`; tool restore and the generated site build succeed.
- Independent review found no beta-release blocker. Its positional-argument completion finding was fixed with regression tests and handed back for focused verification.
- The approved release target is the next beta, not stable. A publisher-free calculation with actual release notes recognizes `v2.0.0-beta.7` and calculates `2.0.0-beta.8` for a squash commit with a conventional `feat:` header and an explicit `BREAKING CHANGE:` footer.
