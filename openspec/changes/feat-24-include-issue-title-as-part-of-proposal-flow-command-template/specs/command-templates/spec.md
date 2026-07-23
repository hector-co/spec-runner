## ADDED Requirements

### Requirement: The `propose` template includes the issue title above the issue body
The `propose` command template file (`CommandTemplates/propose.txt`)
SHALL contain an `{{issue_title}}` placeholder on its own line,
positioned above the `{{issue_body}}` placeholder line.

#### Scenario: Rendered propose prompt places the issue title above the issue body
- **WHEN** the `propose` template is rendered with `spec_name` set to
  `"45-add-login-page"`, `issue_title` set to `"Add Login Page"`, and
  `issue_body` set to `"We need a login page."`
- **THEN** the returned text SHALL contain the rendered issue title line
  immediately above the rendered issue body line, with no `{{...}}`
  token remaining

## MODIFIED Requirements

### Requirement: `ICommandTemplateRenderer` substitutes named placeholders in a template
`SpecRunner.Core.Abstractions` SHALL define an `ICommandTemplateRenderer`
with an operation that accepts a template name and a set of named
replacement values, reads the corresponding
`CommandTemplates/{name}.txt` file, and returns its content with every
`{{token_name}}` placeholder replaced by the matching supplied value.
`SpecRunner.Console` SHALL provide the implementation,
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
