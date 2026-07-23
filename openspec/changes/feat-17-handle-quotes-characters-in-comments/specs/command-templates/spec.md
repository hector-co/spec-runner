## MODIFIED Requirements

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
