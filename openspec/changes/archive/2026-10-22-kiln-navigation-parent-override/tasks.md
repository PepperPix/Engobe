# Tasks

## 1. Parent frontmatter and reference resolution

- [x] 1.1 Add optional `parent` to `FrontMatter` in `src/Kiln.Core/Models/FrontMatter.cs` and pass it unchanged to `ContentItem` in `src/Kiln.Core/Services/ContentReader.cs`; `ContentReaderTests` check field absence and both reference forms.
- [x] 1.2 Add a parent value to `ContentItem` in `src/Kiln.Core/Models/ContentItem.cs` and implement reference resolution in `src/Kiln.Core/Services/NavigationTreeBuilder.cs`: an exact `.md` suffix means a case-sensitive collection-relative `RelativePath`; otherwise the value must be a UUID and resolve uniquely as `id` within the same collection; `NavigationTreeBuilderTests` check paths, UUIDs, and ambiguity prevention.
- [x] 1.3 Report unknown, unpublished, and cross-collection parent targets, duplicate IDs, and cycles as build errors before rendering; `NavigationTreeBuilderTests` check that every error message contains the source file and invalid value.

## 2. Navigation, breadcrumbs, and template context

- [x] 2.1 Extend `NavigationTreeBuilder.Build` so that `parent` replaces the direct structural parent, sections remain only for remaining items, and `weight`/title sorting stays unchanged; integration tests check path and UUID targets and siblings that were not moved.
- [x] 2.2 Make `TemplateRenderer.BuildAncestors` in `src/Kiln.Core/Services/TemplateRenderer.cs` follow the effective section/page-parent chain; `TemplateRendererTests` check breadcrumb paths for both parent forms and the retained collection root without the current page.
- [x] 2.3 In `TemplateRenderer.BuildItemScriptObject`, provide `page.section` as `null` or the deepest structural section `{ title, path, url }` with the base path; `TemplateRendererTests` check root, nested, and parent-override pages.
- [x] 2.4 Extend `ProjectNavNode` and the `is_ancestor` projection so that effective parent ancestors remain marked even with non-prefix URLs and existing URL-prefix detection is preserved; `NavTreeIntegrationTests` and `ApiThemeLayoutTests` check root-level `navtree`, `is_active`, and existing `page.ancestors.size > 1` usage.
- [x] 2.5 Add serve/build integration for invalid parent references; tests check that `kiln serve` reports the error with source file and value during a full rebuild.

## 3. Validation in the target repository

- [x] 3.1 Run `rtk dotnet build` in `PepperPix/Kiln` with zero warnings/errors.
- [x] 3.2 Run the full `dotnet test` suite (without `rtk`) in `PepperPix/Kiln` and confirm all tests pass.
- [x] 3.3 Run `jb inspectcode Kiln.slnx -e=WARNING -f=Text` with zero findings.
- [x] 3.4 Run an end-to-end check in an isolated temporary directory: `kiln new`, create content with path and UUID parents, run `kiln build`, and check breadcrumbs, `page.section`, and navtree active states; also verify an invalid reference through `kiln serve` and remove only that test directory.
- [x] 3.5 Inspect `rtk git show --stat HEAD`; only expected Kiln code, test, and product documentation files may be included.
