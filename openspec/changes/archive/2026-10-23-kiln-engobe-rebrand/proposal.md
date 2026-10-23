## Why

The Kiln name is crowded and does not offer a suitable affordable domain. Engobe provides a distinct product identity with `https://engobe.studio` as the canonical website, starting with the engine's 2.0 release line.

## What Changes

- **BREAKING**: Rename the tool package and command to `Engobe` and `engobe`; rename libraries, namespaces, assemblies, public registration APIs, and solution/project paths from Kiln to Engobe.
- **BREAKING**: Replace `.kiln` project state with `.engobe`, `KILN_*` environment variables with `ENGOBE_*`, and plugin metadata with `engobe-plugin` and `Engobe.Plugin.`.
- Rebrand built-in templates, generated workflows, documentation, examples, package metadata, and generated-site attribution; support the `dotnet-suggest` completion protocol without shipping shell-specific integration.
- Allow plugin installation from an explicit NuGet v3 source or local feed, persist the selected source in the plugin lock, and reuse it for updates.
- Provide a manual migration guide. Do not add legacy aliases, dual reads, or automatic project migration.
- Update release paths and publication configuration while retaining release history and the existing 2.0 prerelease progression.

## Capabilities

### New Capabilities
- `kiln/product-identity`: Command, package, project-state, template, and migration contracts for the Engobe identity.

### Modified Capabilities
- `kiln/plugins`: New package tag, search tag, first-party prefix, and lock-file location.

## Impact

- Target: `PepperPix/Engobe` (renamed from `PepperPix/Kiln`); implementation on a feature branch from current `beta`, merge into `beta`, stable release through `main`.
- Affects all source/test projects, `Directory.Build.props`, `package.json` release project paths, NuGet validation baselines, embedded scaffold files, plugin source selection and lock/update behavior, and CI.
- Consumers must use `Engobe.Core` and `Engobe.Abstractions`; Studio and first-party plugins require coordinated updates.
- Repository rename, NuGet ownership/prefix reservation, OIDC configuration, and canonical website deployment are external cutover prerequisites, not implicit code permissions.
- Non-goals: unrelated features, internalizing public services, redesigning template syntax, changing licenses, rewriting Git history, or deleting old published packages.
