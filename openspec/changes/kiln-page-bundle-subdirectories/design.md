---
status: accepted
date: 2026-10-19
---

## Context
See `proposal.md`. `ContentReader.ReadSection` stops at the bundle's `index.md`; `SiteAssetCopier.CopyNonMarkdownFiles` uses `TopDirectoryOnly`. `MarkdownProcessor` already generates subpaths; `AssetReferenceIndexBuilder` recognizes canonical `/assets/` images. Optimization and the rename manifest are in the copier; `SiteBuilder` uses `generatedFiles` for development pruning.

## Goals / Non-Goals
**Goals:** Identical bundle-relative paths for copying, URLs, and optimization; recursive asset output with clear exclusions.

**Non-Goals:** No section/content detection inside leaf bundles, no different permalink rules, and no CDN redesign.

## Decisions
**Confirmed on 2026-10-19:**

1. All other content of a leaf bundle, including subdirectories, is assets, consistent with leaf-bundle semantics and Hugo's leaf-bundle practice. The previously proposed Markdown-subtree blocking is removed entirely: `child/index.md` or `images/README.md` does not affect the classification of adjacent files.
2. Apply exactly three exclusions recursively: `*.md` files at every level (case-insensitive extension), hidden files/directories with names starting with `.`, and file/directory symlinks. Do not traverse hidden directories or symlinks. Never publish Markdown files, regardless of frontmatter/draft status, but continue traversing surrounding directories.
3. `ContentReader` stops at the outer bundle root. Nested `index.md` or other Markdown files create neither new items nor sections. Regular sections outside a bundle remain content. The leaf-bundle contract is clarified only by the `.md` exclusion; the leaf-bundle reading rule does not change.
4. Use a shared asset catalog for copying and reference resolution: associate existing download targets and image paths using the same exclusions; normalize `./` and internal `..`, preserve query/fragment, and prevent root escapes. Leave references to excluded `.md` files unchanged and warn, even when they are image targets. External/root-relative URLs and anchors remain unchanged. Do not resolve hidden/symlink targets as assets; missing eligible images remain detectable by the production link check.
5. Reuse the existing `CopyOrOptimizeFile`; `Path.GetRelativePath` preserves subpaths. Reuse image index/exclusion/opt-out/cache behavior unchanged. Register actual output files in `generatedFiles`, including extension changes, instead of introducing a second optimization path.

## Risks / Trade-offs
- [Markdown files within the asset tree] → Omit only those files; test `README.MD`, `child/index.md`, and adjacent images.
- [Publishing or traversing hidden files/symlinks] → Check exclusions before copying and directory descent; use identical rules for reference resolution.
- [Dead Markdown references] → Warn for references to excluded `.md` targets; do not produce an artificial asset URL.
- [Interaction with CDN URLs] → Copying and the reference index retain canonical asset paths; apply the public base separately.
