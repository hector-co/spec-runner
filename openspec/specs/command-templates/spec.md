# command-templates

## Purpose

Defines the plain-text command template files shipped with
`SpecRunner.Console` and the `ICommandTemplateRenderer` abstraction that
renders them, so that CLI-agent command prompts are authored as template
files rather than built via C# string interpolation.
## Requirements
### Requirement: Each CLI-agent command is defined by its own template file
`SpecRunner.Console` SHALL ship one plain-text template file per
CLI-agent command under `CommandTemplates/` (`propose.txt`, `apply.txt`,
`update.txt`, `archive.txt`), copied to the build output directory. Each
file SHALL contain the full, unquoted command text for that command,
including any `{{token}}` placeholders and the standing unattended-run
instruction block (see "Every command template ends with a standing
unattended-run instruction" below). No workflow runner SHALL build this
text via C# string interpolation.

#### Scenario: Template files exist for every current command
- **WHEN** the `SpecRunner.Console` output directory is inspected after a
  build
- **THEN** it SHALL contain `CommandTemplates/propose.txt`,
  `CommandTemplates/apply.txt`, `CommandTemplates/update.txt`, and
  `CommandTemplates/archive.txt`

### Requirement: The `propose` template includes the issue title above the issue body
The `propose` command template file (`CommandTemplates/propose.txt`) SHALL
contain an `{{issue_title}}` placeholder on its own line, positioned
above the `{{issue_body}}` placeholder line.

#### Scenario: Rendered propose prompt places the issue title above the issue body
- **WHEN** the `propose` template is rendered with `spec_name` set to
  `"45-add-login-page"`, `issue_title` set to `"Add Login Page"`, and
  `issue_body` set to `"We need a login page."`
- **THEN** the returned text SHALL contain the rendered issue title line
  immediately above the rendered issue body line, with no `{{...}}`
  token remaining

### Requirement: `ICommandTemplateRenderer` substitutes named placeholders in a template
`SpecRunner.Core.Abstractions` SHALL define an `ICommandTemplateRenderer`
with an operation that accepts a template name and a set of named
replacement values, reads the corresponding
`CommandTemplates/{name}.txt` file, and returns its content with every
`{{token_name}}` placeholder replaced by the matching supplied value,
after escaping that value so it cannot terminate a quoted block the
rendered text is placed inside. Escaping SHALL first double every
backslash (`\` → `\\`) in the value, then escape every double quote
(`"` → `\"`), applied to each supplied value independently before
substitution. Escaping SHALL NOT remove quote or backslash characters
from the rendered output; it SHALL only ensure they appear in escaped
form. `SpecRunner.Console` SHALL provide the implementation,
`CommandTemplateRenderer`.

#### Scenario: Placeholders are replaced with supplied values
- **WHEN** the `propose` template (content
  `` /opsx-propose {{spec_name}}\n{{issue_title}}\n{{issue_body}} ``
  plus the standing unattended-run block) is rendered with `spec_name`
  set to `"45-add-login-page"`, `issue_title` set to
  `"Add Login Page"`, and `issue_body` set to
  `"We need a login page."`
- **THEN** the returned text SHALL be
  `` /opsx-propose 45-add-login-page\nAdd Login Page\nWe need a login
  page. `` followed by the standing unattended-run block, with no
  `{{...}}` token remaining

#### Scenario: Rendering an unknown template name fails clearly
- **WHEN** `ICommandTemplateRenderer` is asked to render a template name
  with no corresponding `CommandTemplates/{name}.txt` file on disk
- **THEN** it SHALL throw an exception whose message includes the
  resolved file path, rather than returning empty or partial content

#### Scenario: A template placeholder with no supplied value fails clearly
- **WHEN** `ICommandTemplateRenderer` renders a template that contains a
  `{{token_name}}` placeholder for which the caller did not supply a
  value
- **THEN** it SHALL throw an exception identifying the unresolved
  `token_name`, rather than leaving the literal `{{token_name}}` text in
  the returned string

#### Scenario: A double quote in a supplied value is escaped, not removed
- **WHEN** the `update` template is rendered with `instructions` set to
  `` also handle the "edge case" comment ``
- **THEN** the returned text SHALL contain
  `` also handle the \"edge case\" comment ``, with both double quote
  characters preserved in escaped form rather than stripped

#### Scenario: A backslash in a supplied value is escaped before quote escaping
- **WHEN** the `update` template is rendered with `instructions` set to
  a value ending in a literal backslash immediately followed by a double
  quote (i.e. the two-character sequence `\"`)
- **THEN** the returned text SHALL contain that sequence rendered as
  `` \\\" `` (the original backslash doubled, followed by the escaped
  quote), not as `` \\" `` (which would leave an unescaped, terminating
  quote once unescaped by a standard-convention consumer)

#### Scenario: A value with no quotes or backslashes is unaffected
- **WHEN** any template is rendered with a supplied value containing
  neither `"` nor `\` characters
- **THEN** the returned text SHALL contain that value unchanged

### Requirement: Every command template ends with a standing unattended-run instruction
Each of the four shipped command template files SHALL end with the
following fixed instruction block, verbatim:

```
This is an unattended run — do not ask for confirmation or clarification
at any step. If something is ambiguous, make the most reasonable
assumption, note it in proposal.md under a brief "Assumptions" note, and
continue.
```

#### Scenario: Rendered prompt always carries the unattended-run instruction
- **WHEN** any of the `propose`, `apply`, `update`, or `archive`
  templates is rendered with a valid set of replacement values
- **THEN** the returned text SHALL end with the standing unattended-run
  instruction block shown above

