## ADDED Requirements

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
  `` /opsx-propose {{spec_name}}\n{{issue_body}} `` plus the standing
  unattended-run block) is rendered with `spec_name` set to
  `"45-add-login-page"` and `issue_body` set to `"We need a login page."`
- **THEN** the returned text SHALL be
  `` /opsx-propose 45-add-login-page\nWe need a login page. `` followed
  by the standing unattended-run block, with no `{{...}}` token remaining

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
