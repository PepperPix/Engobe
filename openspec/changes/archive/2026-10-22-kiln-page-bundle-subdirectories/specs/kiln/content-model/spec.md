## MODIFIED Requirements

### Requirement: Read Markdown content and page bundles
Kiln SHALL read Markdown files with YAML frontmatter in collection directories and recursively discovered sections; a subdirectory with `index.md` SHALL be treated as one page-bundle item. Inside this leaf bundle, Kiln SHALL not detect further content items or sections, even with nested `index.md`. All other content, including subdirectories, SHALL be assets, except `.md` files, hidden entries, and symlinks.

#### Scenario: Detect a section and page bundle
- **WHEN** a directory without `index.md` contains Markdown files and subdirectories, and a subdirectory contains `index.md`
- **THEN** Kiln SHALL read the Markdown files as items of their respective section, treat `index.md` as one bundle item, and associate its other files, including subdirectories with the defined exclusions, as assets.

#### Scenario: A leaf bundle remains one item without Markdown-subtree blocking
- **WHEN** `content/posts/outer/index.md`, `outer/child/index.md`, `outer/child/photo.png`, `outer/notes/article.md`, and `outer/notes/data.zip` exist
- **THEN** only the item from `outer/index.md` SHALL be created; the nested Markdown files SHALL neither create items nor be published, while `child/photo.png` and `notes/data.zip` are assets of the outer bundle.

#### Scenario: A normal nested section remains content
- **WHEN** `content/posts/guides/advanced/install/index.md` exists, but neither `guides/` nor `advanced/` has its own `index.md`
- **THEN** the install bundle SHALL be read with `SectionPath` `guides/advanced` and retain its own asset namespace.
