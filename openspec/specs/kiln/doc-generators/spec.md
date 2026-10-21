# kiln/doc-generators Specification

## Purpose
This specification describes the CLI generators for .NET XML documentation and OpenAPI and their file output.

## Requirements
### Requirement: OpenAPI Reference Generator
Kiln SHALL provide `kiln gen docs --openapi <spec> [--output <dir>] [--project <path>]` to generate Markdown content from an OpenAPI file.

#### Scenario: JSON or YAML Input
- **WHEN** the user supplies an existing JSON or YAML OpenAPI specification path
- **THEN** Kiln writes the generated Markdown pages to the output directory and reports written, skipped, and conflicting files.

#### Scenario: Operations
- **WHEN** the specification contains a path with operations
- **THEN** Kiln generates one Markdown page per operation.

#### Scenario: Missing Input
- **WHEN** `--openapi` is missing or the file does not exist
- **THEN** Kiln reports the error and exits the command with exit code 1.

### Requirement: .NET XML Reference Generator
Kiln SHALL provide `kiln gen dotnet-xml --xml <path> [--assembly <path>] [--package-version <version>] [--output <dir>] [--project <path>]`.

#### Scenario: Assembly Filtering
- **WHEN** the user supplies a valid assembly with `--assembly`
- **THEN** the generated reference contains only types and members that are publicly visible in that assembly.

#### Scenario: Package Version
- **WHEN** `--package-version` contains a nonempty value
- **THEN** Kiln writes the trimmed value as `extra.version` into the frontmatter of generated pages.

#### Scenario: Optional Assembly and Version
- **WHEN** `--assembly` or `--package-version` is not specified
- **THEN** Kiln generates the XML reference without assembly filtering or without `extra.version`, respectively.

#### Scenario: Invalid Input
- **WHEN** `--xml` is missing or the XML file or specified assembly does not exist
- **THEN** Kiln reports the error and exits the command with exit code 1.

### Requirement: Generated Files as Project Content
Kiln SHALL store generator output as regular Markdown files under the specified output path in the project directory instead of generating it only in a build artifact.

#### Scenario: Default Output Paths
- **WHEN** no output is specified
- **THEN** `kiln gen docs` uses `content/api` and `kiln gen dotnet-xml` uses `content/api-dotnet`, relative to the project path.

#### Scenario: Output Path Not in a Collection
- **WHEN** `site.yaml` exists and no collection refers to the output folder
- **THEN** Kiln still writes the files and indicates that a collection must be added.

### Requirement: Generation Ownership and Conflicts
Kiln SHALL mark new generated pages with `generated: true` and a `source_hash` of the generated body and not silently overwrite existing user changes.

#### Scenario: Unchanged Generated Page
- **WHEN** an existing generated page still matches the stored body hash
- **THEN** the generator writes the updated generated page again.

#### Scenario: Manually Edited Generated Page
- **WHEN** the stored body hash no longer matches the current body
- **THEN** the generator writes the new version alongside it as `<datei>.regenerated` and reports the conflict.

#### Scenario: Adopted Page
- **WHEN** an existing file has no `generated: true`, for example because it was adopted
- **THEN** Kiln leaves it unchanged and reports it as skipped/adopted.

### Requirement: XML Pages Define Navigation
Kiln SHALL create XML type pages in files whose directory path follows the namespace hierarchy.

#### Scenario: Namespaced Type
- **WHEN** XML documentation contains a type with a namespace
- **THEN** Kiln creates its Markdown page under the corresponding namespace directories so that normal directory navigation represents the hierarchy.
