# Proposal

## Why

The homepage contract requires a warning during a successful build when the `home` block is absent, because Kiln otherwise produces a site without output for `/` and does not diagnose it. This warning is missing, leaving a significant and easily overlooked gap in the site scaffold undetected.

## What Changes

- Kiln reports ``site root `/` is empty`` when no `home` block is configured in `site.yaml`.
- The build remains successful; explicitly configured homepages and existing error handling remain unchanged.
- `content/index.md` is not automatically detected as the homepage.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `kiln/homepage`: Diagnose a missing homepage with a warning instead of silently leaving the root empty.

## Impact

- Target repository: `PepperPix/Kiln`, feature branch `feature/kiln-missing-homepage-warning` from `beta`.
- Affected areas: `Kiln.Core/Services/SiteBuilder.BuildAsync` and `Kiln.Core/Models/BuildResult.Warnings`; tests in `Kiln.Core.Tests/Services/SiteBuilderTests` or `HomepageEngineTests`.
- Affected contract: a completely absent `home` block produces a build warning.
- Out of scope: no scaffold redesign, no implicit `index.md` convention, no change to `home.page`/`home.collection` validation, and no build errors for a missing homepage.
