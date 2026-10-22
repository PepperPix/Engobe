# Design

## Context

See `proposal.md` for motivation and `specs/kiln/navigation/spec.md` for the behavior contract. `ContentItem` contains `SectionPath`, `RelativePath`, optional `Id`, and unknown frontmatter values in `Extra`. `NavigationTreeBuilder.Build` currently creates only directory sections and page leaves. `TemplateRenderer.BuildAncestors` builds breadcrumbs from the collection root and `SectionPath`; `BuildCommonScriptObject` provides `navtree` at root level and projects `is_active`/`is_ancestor`. `page.section` does not yet exist.

The template contract remains authoritative: `navtree.<collection>` stays at root level, and `page.ancestors` does not contain the current page. The default theme's `layouts/api.html` uses `page.ancestors.size > 1`.

## Goals / Non-Goals

**Goals:**
- Retain the directory hierarchy as the default, replace it with exactly one parent override, and generate all derived navigation data from the same hierarchy.
- Resolve parent references in a stable, unambiguous way independent of slug/URL changes.
- Make `page.section` available as structural directory context, independent of the navigation override.

**Non-Goals:**
- No conversion of `navtree` to `site.navtree`, and no inclusion of the current page in `page.ancestors`.
- No change to permalinks, collection routing, menus, or site configuration.

## Decisions

1. **Confirmed — two unambiguous parent forms:** A value ending in exactly `.md` is interpreted as a case-sensitive collection-relative `RelativePath` with `/` separators. Every other value must be a UUID and is resolved as the target item's stable `id`. The forms do not overlap: a source path ends in `.md`, whereas an `id` value is a UUID. An invalid string is not interpreted heuristically as a slug, URL, or file path. UUID IDs must be resolved within the same collection and must be unique there. IDs survive renames and support stable references from Kiln Studio.
   - **Rejected alternative:** Slugs and URLs can change or collide; absolute file paths are not portable.
2. **Confirmed — parent replaces the direct structural parent:** Without `parent`, the section hierarchy remains unchanged. An override places the item under the referenced page node; previous sections remain only if other items need them. Siblings retain ascending `weight`, then title order.
   - **Rejected alternative:** Using both parents simultaneously would produce an ambiguous navigation tree.
3. **Confirmed — invalid references are build errors:** Unknown, unpublished, cross-collection, or ambiguous targets and cycles stop the build before rendering. Error messages name the source file and invalid parent value. `kiln serve` performs a full rebuild and displays the same error.
   - **Rejected alternative:** A warning with structural fallback could silently produce different navigation for a typo.
4. **Confirmed — breadcrumb and active path:** `page.ancestors` follows the effective chain of collection root and actual section/page ancestors, without the current page. `is_active` remains URL-based; `is_ancestor` includes effective parent ancestors and retains existing URL-prefix detection for structural sections. Comparisons respect the base path.
5. **Confirmed — `page.section`:** `null` for empty `SectionPath`, otherwise an object for the deepest structural directory section with `title`, `path`, and base-path-aware `url`. `title` is humanized like a section title in the navtree. `parent` does not change this structural context.

## Risks / Trade-offs

- [A source path changes when a file is moved] → Use the stable UUID form for rename-resilient references.
- [A UUID may be invalid or duplicated within a collection] → Validate UUID format and unique collection resolution before rendering; report errors with source file and reference value.
- [Page-parent overrides can remove empty sections from the navtree] → Output only structural sections still needed and test both moved and remaining items.
- [Template data does not yet contain a publicly used `page.section`] → Add only the new binding; leave existing bindings unchanged.

## Migration Plan

No migration for existing sites: without `parent`, existing directory navigation is preserved. New references can be added as paths or UUIDs. Invalid references can be rolled back by correcting or removing `parent`. The same build error is visible during `kiln serve`.
