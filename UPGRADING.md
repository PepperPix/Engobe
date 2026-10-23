# Upgrading from Kiln to Engobe

Engobe is a new tool, package, API, and project-state identity. It does not provide a `kiln`
command alias, read legacy project state, or migrate projects automatically. Keep the previous
Kiln installation and a complete project backup until the Engobe build and plugin checks succeed.

## 1. Back up the project

Stop any running site preview and copy the entire project to a separate, access-controlled
location:

```bash
cp -a /path/to/site /path/to/site.backup
```

The backup must include `site.yaml`, all content and themes, `plugins/`, deployment configuration,
the existing `.kiln/` directory, and any project-local tooling. Verify the backup before changing
the working project. Do not commit secrets from deployment configuration.

## 2. Install Engobe and update integrations

Install an Engobe version that is available from the configured NuGet source:

```bash
dotnet tool install -g Engobe --version <engobe-version>
engobe --help
```

Use `engobe` in scripts, CI workflows, editor tasks, and deployment instructions. Keep the old
Kiln tool installed during the migration so it remains available for rollback.

Applications that use the engine in-process must replace `Kiln.Core` and `Kiln.Abstractions` with
`Engobe.Core` and `Engobe.Abstractions`, update Kiln namespaces to Engobe namespaces, and replace
`AddKiln()` / `IKilnBuilder` with `AddEngobe()` / `IEngobeBuilder`. Rebuild those applications
before migrating their sites.

## 3. Update explicit Pagefind overrides

Rename environment variables in shell profiles, CI variables, and service configuration:

| Kiln variable | Engobe variable |
|---|---|
| `KILN_PAGEFIND_PATH` | `ENGOBE_PAGEFIND_PATH` |
| `KILN_PAGEFIND_CACHE_DIR` | `ENGOBE_PAGEFIND_CACHE_DIR` |

`ENGOBE_PAGEFIND_PATH` still takes precedence over `search.binaryPath`; the configured path still
takes precedence over `PATH`, the cache, and an allowed download. `ENGOBE_PAGEFIND_CACHE_DIR`
selects the cache root. Engobe ignores the old `KILN_*` variables. The `search.binaryPath`
configuration key does not change.

## 4. Preserve content and plugin customizations

Do not rename or move `site.yaml`, content, theme directories, public URLs, collection paths, or
template syntax as part of this tool migration. Engobe uses the existing site schema and leaves
content in place.

Keep a separate copy or version-control diff of every locally customized file under `plugins/`.
Install each plugin again from a package that is compatible with Engobe and carries the
`engobe-plugin` NuGet tag:

```bash
engobe plugin add <package-id> --version <available-version>
engobe plugin list
```

Use the replacement package ID when a publisher renamed a first-party package; compatible
community package IDs do not have to change. Review the installed files against the saved
customizations and reapply only the required edits. Do not use `--force` until local changes have
been backed up. Installing the Engobe package writes a new `.engobe/plugins.lock.json` entry with
the package identity, trust level, and content hash.

Do not copy `.kiln/plugins.lock.json` into `.engobe/`, change its package ID by hand, or transfer
its trust value or hash. Engobe does not read that legacy lock. The new lock must be produced by
installing the Engobe-compatible package.

## 5. Regenerate disposable state and verify

Do not copy caches from `.kiln/` into `.engobe/`. Engobe creates its own project caches under
`.engobe/`; Pagefind's user cache is recreated under the selected Engobe cache root. Keep the old
`.kiln/` directory in the backup. Engobe does not consume it, and builds protect it from output
cleanup.

From the migrated project, run:

```bash
engobe build
engobe plugin list
```

Inspect the rendered site, links, deployment output, and plugin behavior before switching CI or
publishing. Commit `.engobe/plugins.lock.json` with the project after verifying the new package
versions. The content and public URL schema should remain unchanged.

## Rollback

If verification fails, restore the complete project backup and use the retained Kiln installation
and its original `.kiln/` state. Do not copy Engobe lock entries or caches back into `.kiln/`.
Keep the backup until the migrated project has passed its normal deployment checks.
