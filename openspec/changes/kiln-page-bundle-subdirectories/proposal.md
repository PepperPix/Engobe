---
status: accepted
date: 2026-10-19
---

## Why
Page bundles can organize images and downloads in subdirectories, but `SiteAssetCopier.CopyNonMarkdownFiles` currently copies only files directly alongside `index.md`. `MarkdownProcessor` already produces a bundle asset path for `images/x.jpg`; the file is then missing from the output. This change completes the leaf-bundle semantics: all other content, including subdirectories, is assets, without further content detection inside a bundle.

## What Changes
- Copy bundle files recursively to `assets/content/<collection>/<sectionPath>/<slug>/`, preserving their relative paths.
- Omit only `*.md` files (case-insensitive extension), hidden files/directories (names starting with `.`), and symlinks. Do not traverse hidden directories or directory symlinks.
- An `index.md`, `article.md`, or `README.md` in a subdirectory does not block adjacent assets; only the Markdown file itself is never published.
- Resolve relative Markdown images and links to existing bundle downloads to the same asset namespace; keep `page_asset_url('images/x.jpg')` consistent. References to excluded `.md` targets remain unchanged and produce a warning.
- `ContentReader` still does not descend into leaf bundles: no section/content detection inside the bundle, regardless of Markdown files present there. Actual sections outside a bundle remain unchanged.
- Ensure optimization, WebP/fingerprint rewriting, and development pruning work for deep asset paths.

## Capabilities
### New Capabilities
None.

### Modified Capabilities
- `kiln/assets`: Recursive bundle assets with file-based Markdown exclusion, exclusion of hidden entries/symlinks, and relative image/download references.
- `kiln/content-model`: Recursive asset association with the leaf-bundle reading rule unchanged.

## Impact
- Target repository: `PepperPix/Kiln`; target branch: new feature branch from `beta`.
- Affected: `src/Kiln.Core/Services/SiteAssetCopier.cs`, `ContentReader.cs`, `MarkdownProcessor.cs`, `AssetReferenceIndexBuilder.cs`, `TemplateRenderer.cs`, existing TUnit tests, and product bundle documentation in embedded starter content.
- Clarify leaf-bundle semantics with the `.md` exclusion.
- Related contracts: image optimization, production pipeline, and base path; responsive image output remains out of scope.
- No new dependency. This plan does not itself change product code.
- **Out of scope:** No branch bundles, no `_index.md`, no recursive reading of content inside a leaf bundle, no new permalink rules, no responsive image generation, and no CDN configuration. CDN URLs are planned separately in `kiln-cdn-asset-prefix`; this change also works independently with `/assets/`.
