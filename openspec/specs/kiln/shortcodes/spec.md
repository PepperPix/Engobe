# kiln/shortcodes Specification

## Purpose
This specification describes inline shortcodes from content plugins and their processing in Markdown content.

## Requirements
### Requirement: Plugin Shortcode Declaration
Engobe SHALL support shortcodes declared under `shortcodes` in `plugin.yaml` and provided as `shortcodes/<name>.html` in the plugin directory.

#### Scenario: Declared Shortcode
- **WHEN** a content invocation matches the shortcode name of a loaded plugin
- **THEN** Engobe renders the partial with that name from the plugin.

### Requirement: Inline Shortcode Syntax
Engobe SHALL process the self-closing syntax `{% name argumente %}` in raw Markdown before Markdown conversion.

#### Scenario: Quoted Argument
- **WHEN** a shortcode encloses values with spaces in single or double quotes
- **THEN** Engobe treats the quoted content as one positional argument.

#### Scenario: Code Region
- **WHEN** shortcode syntax occurs in a fenced code block or an inline code span
- **THEN** Engobe leaves it unchanged as example text.

### Requirement: Shortcode Template Context
Engobe SHALL provide shortcode partials with `args`, individual arguments as `arg0`, `arg1`, etc., `plugin_name`, `plugin_asset_url`, and `string.base64_encode`, without providing `page` or `collection`.

#### Scenario: Partial Uses an Argument and Plugin Asset
- **WHEN** a shortcode partial references positional arguments or a plugin asset
- **THEN** it can use `arg0` or `plugin_asset_url`, respectively; the asset URL starts with `/assets/plugins/<plugin-name>/`.

### Requirement: Unresolvable Shortcodes
Engobe SHALL leave unknown or unrenderable shortcodes in the Markdown and collect a warning for them.

#### Scenario: Missing Declaration or Partial
- **WHEN** no plugin declares the name or the corresponding partial is missing or invalid
- **THEN** the original shortcode text remains and Engobe logs a warning.
