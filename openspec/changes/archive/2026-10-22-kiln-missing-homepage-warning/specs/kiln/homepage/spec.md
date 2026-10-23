# Spec Delta

## ADDED Requirements

### Requirement: Report a missing homepage as a root warning
When `site.yaml` defines no `home` block, Kiln SHALL issue a warning that the site root `/` is empty and not silently report the build as fully rendered without that notice.

#### Scenario: Build a site without homepage configuration
- **WHEN** a valid site project without a `home` block is built
- **THEN** the build SHALL contain the warning ``site root `/` is empty`` and otherwise remain successful.

#### Scenario: Configure an explicit homepage
- **WHEN** a valid `home.page` or `home.collection` block is configured
- **THEN** Kiln SHALL not issue the missing-homepage warning.
