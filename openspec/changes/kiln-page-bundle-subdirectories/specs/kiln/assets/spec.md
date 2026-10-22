## MODIFIED Requirements

### Requirement: Associate page-bundle assets with the item
Kiln SHALL recursively copy the files of a page bundle, including all subdirectories, to `assets/content/<collection>/<sectionPath>/<slug>/`, preserving their relative structure, and provide `page_asset_url(filename)` in templates. Kiln SHALL omit only `*.md` files (case-insensitive extension), hidden files/directories (names starting with `.`), and symlinks; hidden directories and directory symlinks SHALL not be traversed.

#### Scenario: Reference an asset of a nested bundle
- **WHEN** a bundle item with section path `guides/advanced` and slug `install` directly contains an image `diagram.svg`
- **THEN** Kiln SHALL output the file under `assets/content/posts/guides/advanced/install/diagram.svg` and resolve `page_asset_url('diagram.svg')` to that path.

#### Scenario: Copy a multi-level asset subdirectory
- **WHEN** `content/posts/guides/install/index.md` has the files `images/screens/x.jpg` and `downloads/setup.zip` in subdirectories alongside it
- **THEN** Kiln SHALL copy them to `assets/content/posts/guides/install/images/screens/x.jpg` and `assets/content/posts/guides/install/downloads/setup.zip`; `page_asset_url('images/screens/x.jpg')` SHALL use the same relative structure in the public asset namespace.

#### Scenario: Markdown files do not block adjacent assets
- **WHEN** a bundle contains `child/index.md`, `child/photo.png`, `notes/article.md`, `notes/attachments/data.zip`, `images/manual/README.MD`, and `images/manual/b.jpg`
- **THEN** Kiln SHALL copy all three non-Markdown files with their relative subpaths and omit all listed Markdown files without blocking the surrounding directories.

#### Scenario: Do not publish hidden entries
- **WHEN** a bundle contains `.secret`, `images/.metadata`, `.private/picture.jpg`, and `images/.cache/x.jpg`
- **THEN** Kiln SHALL omit those files and the entire hidden directories while copying visible sibling assets.

#### Scenario: Do not copy or follow symlinks
- **WHEN** a bundle contains a symlink to a file or directory inside or outside its root
- **THEN** Kiln SHALL not copy the symlink as a bundle asset or traverse its target.

### Requirement: Rewrite relative bundle image paths
Kiln SHALL rewrite relative image targets in page-bundle Markdown to its asset path, normalizing `./` and internal `..` segments and preserving query/fragment. External/root-relative URLs and excluded Markdown, hidden, or symlink targets SHALL remain unchanged; references to excluded `.md` targets SHALL trigger a warning. Targets outside the bundle root SHALL not be resolved as assets.

#### Scenario: Render a relative Markdown image
- **WHEN** a bundle body contains `![Diagramm](diagram.svg)`
- **THEN** Kiln SHALL set the image source in the rendered HTML to `/assets/content/<collection>/<sectionPath>/<slug>/diagram.svg`.

#### Scenario: Render a relative subdirectory image with a suffix
- **WHEN** a bundle body contains `![Bild](./images/screens/../x.jpg?v=2#detail)`
- **THEN** the image source SHALL point to the bundle asset path with subpath `images/x.jpg?v=2#detail`; in the default case without a base path, this is `/assets/content/<collection>/<sectionPath>/<slug>/images/x.jpg?v=2#detail`.

#### Scenario: Resolve an image adjacent to nested Markdown
- **WHEN** `child/index.md` and `child/photo.png` exist below a bundle and its outer Markdown body contains `![Bild](child/photo.png)`
- **THEN** Kiln SHALL rewrite the image source to the copied file under bundle asset path `child/photo.png`.

#### Scenario: Detect a missing eligible bundle image
- **WHEN** a bundle references `![Fehlt](images/missing.jpg)` without the file existing and production link checking is enabled
- **THEN** Kiln SHALL associate the relative image path with the bundle namespace and report the missing local asset reference as a build error.

#### Scenario: Diagnose a Markdown target used as an image reference
- **WHEN** the outer bundle body contains `![Kein Bild](child/index.md)`
- **THEN** Kiln SHALL leave the target unchanged, report a warning for the excluded `.md` file, and output no file or additional content page for it.

## ADDED Requirements

### Requirement: Link relative bundle downloads
Kiln SHALL rewrite relative Markdown links to existing non-Markdown files in the bundle asset tree to its asset path, preserving subdirectories and query/fragment. External/root-relative URLs, anchors, and targets outside the bundle root SHALL remain unchanged. Hidden/symlink targets SHALL not be resolved as assets. Links to excluded `.md` targets SHALL remain unchanged and trigger a warning.

#### Scenario: Link a download from a subdirectory
- **WHEN** a bundle contains `[Download](./downloads/setup.zip?download=1#file)` and the file is a visible, regular bundle asset
- **THEN** the rendered `href` SHALL point to its public bundle asset path with `downloads/setup.zip?download=1#file`.

#### Scenario: A Markdown target is not a download asset
- **WHEN** a bundle contains `[Kapitel](notes/article.md)` or `[Unterseite](child/index.md)`
- **THEN** Kiln SHALL leave the references unchanged, report a warning for each excluded `.md` file, and not publish those files.

#### Scenario: External links and anchors are preserved
- **WHEN** a bundle contains `[Extern](https://example.com/file.zip)` and `[Abschnitt](#details)`
- **THEN** Kiln SHALL leave the targets unchanged and not treat them as bundle downloads.
