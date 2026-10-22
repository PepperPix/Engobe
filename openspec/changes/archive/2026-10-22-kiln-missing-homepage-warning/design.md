# Design

## Context

See `proposal.md`: `BuildResult.Warnings` is already the contract for nonfatal build diagnostics; a missing `home` block is currently silent.

## Goals / Non-Goals

**Goals:** Implement the missing-homepage warning rule without an additional configuration or template convention.

**Non-Goals:** Infer root output heuristically from item permalinks or filenames.

## Decisions

- The absence of `HomeConfiguration` triggers the warning, which is included in build warnings. An explicit `home.page` or `home.collection` block suppresses it; their existing validation and rendering logic remain unchanged.
- The diagnostic remains nonfatal and uses exactly ``site root `/` is empty``, as required by the homepage contract. The alternatives of a build error and automatic detection of `content/index.md` would contradict that contract.

## Risks / Trade-offs

- [Notice for sites that intentionally have no root content] → Retain the warning because the homepage contract explicitly requires it for backward compatibility as well; setting a homepage configuration remains the clear way to avoid it.

No configuration migration is required.
