# Tasks

## 1. Root warning and regression tests

- [ ] 1.1 Make `SiteBuilder.BuildAsync` add the required homepage warning when `SiteConfiguration.Home` is absent without affecting build success; demonstrate this with a case without `home` and cases with `home.page`/`home.collection` in `SiteBuilderTests` or `HomepageEngineTests`.
- [ ] 1.2 Ensure that `BuildResult.Warnings` provides the exact message ``site root `/` is empty`` and that error cases for invalid home configuration remain unchanged; demonstrate this through existing `SiteConfigLoaderTests` and `HomepageEngineTests`.

## 2. Validation in the target repository

- [ ] 2.1 Run `rtk dotnet build` and confirm zero warnings/errors.
- [ ] 2.2 Run `dotnet test` (without `rtk`) and confirm all TUnit tests pass.
- [ ] 2.3 Run `jb inspectcode Kiln.slnx -e=WARNING -f=Text` and confirm zero findings.
- [ ] 2.4 Run an end-to-end check in a temporary directory: `kiln new`, then `kiln build` without homepage configuration; verify the successful build and the empty-root warning, then clean up that directory specifically.
- [ ] 2.5 Inspect `rtk git show --stat HEAD` and confirm that the file set contains no unexpected changes.
