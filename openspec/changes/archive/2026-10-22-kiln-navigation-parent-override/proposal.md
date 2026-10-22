# Proposal

## Why

Automatic navigation already represents `SectionPath` as a directory tree, but does not evaluate the specified optional frontmatter field `parent`; `page.section` is also missing. A stable ID as an alternative to the source path makes parent references resilient to renames and allows Kiln Studio to reference content targets reliably.

## What Changes

- Add `parent` as an optional navigation override that accepts either the case-sensitive collection-relative `RelativePath`, including `.md`, or the stable UUID `id` of a target item.
- Distinguish the reference forms unambiguously: values ending in exactly `.md` are paths; other values must be UUIDs and are resolved as `id`.
- Report unknown, unpublished, cross-collection, ambiguous, and cyclic targets as build errors; the message contains the source file and invalid value. Because `kiln serve` performs a full rebuild, the error also appears there.
- Add `page.section` as the deepest structural section `{ title, path, url }` or `null`.
- Preserve the existing root-level `navtree` contract and `page.ancestors` without the current page.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `kiln/navigation`: Add unambiguous parent resolution, error behavior, and `page.section`; update navtree, breadcrumb, and active-path requirements.

## Impact

- Target repository: `PepperPix/Kiln`; target branch: feature branch from `beta`.
- Affected areas: `Kiln.Core` frontmatter/content model, `NavigationTreeBuilder`, `TemplateRenderer`, and parser, navigation, rendering, serve, and build tests; the existing contract of the default theme's `layouts/api.html` is preserved.
- Affected contracts and specifications: navigation/breadcrumbs, nested sections and path-based URLs, template data model, stable UUID `id` from the domain model, and the specified parent override; current contract `openspec/specs/kiln/navigation/spec.md`.

### Out of scope

- No change to the root-level `navtree` binding, the shape of `page.ancestors` (without the current page), URL generation, manual menus, or directory structure as the default hierarchy.
- No new fields in `site.yaml` and no migration of existing themes to `site.navtree`.
