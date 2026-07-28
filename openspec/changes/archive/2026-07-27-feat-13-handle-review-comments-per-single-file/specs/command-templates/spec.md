## MODIFIED Requirements

### Requirement: Each CLI-agent command is defined by its own template file
`SpecRunner.Console` SHALL ship one plain-text template file per
CLI-agent command under `CommandTemplates/` (`propose.txt`, `apply.txt`,
`update.txt`, `update-file.txt`, `archive.txt`), copied to the build output
directory. Each file SHALL contain the full, unquoted command text for
that command, including any `{{token}}` placeholders and the standing
unattended-run instruction block (see "Every command template ends with a
standing unattended-run instruction" below). No workflow runner SHALL
build this text via C# string interpolation.

#### Scenario: Template files exist for every current command
- **WHEN** the `SpecRunner.Console` output directory is inspected after a
  build
- **THEN** it SHALL contain `CommandTemplates/propose.txt`,
  `CommandTemplates/apply.txt`, `CommandTemplates/update.txt`,
  `CommandTemplates/update-file.txt`, and `CommandTemplates/archive.txt`

### Requirement: Every command template ends with a standing unattended-run instruction
Each of the five shipped command template files SHALL end with the
following fixed instruction block, verbatim:

```
This is an unattended run — do not ask for confirmation or clarification
at any step. If something is ambiguous, make the most reasonable
assumption, note it in proposal.md under a brief "Assumptions" note, and
continue.
```

#### Scenario: Rendered prompt always carries the unattended-run instruction
- **WHEN** any of the `propose`, `apply`, `update`, `update-file`, or
  `archive` templates is rendered with a valid set of replacement values
- **THEN** the returned text SHALL end with the standing unattended-run
  instruction block shown above

## ADDED Requirements

### Requirement: The `update-file` template includes a file line between the change name and the instructions
The `update-file` command template file (`CommandTemplates/update-file.txt`) SHALL contain a `{{spec_name}}` placeholder on its opening line, followed
by a blank line, a line reading `File: {{file_name}}`, and then the
`{{instructions}}` placeholder on its own line, mirroring `update.txt`'s
opening line and instructions placeholder but with the file line inserted
between them.

#### Scenario: Rendered file-anchored update prompt places the file line before the instructions
- **WHEN** the `update-file` template is rendered with `spec_name` set to
  `"45-add-login-page"`, `file_name` set to `"src/Login.cs"`, and
  `instructions` set to `"the login button must say Sign In"`
- **THEN** the returned text SHALL contain, in order, the rendered
  change-name line, a blank line, `"File: src/Login.cs"`, and then `"the
  login button must say Sign In"`, with no `{{...}}` token remaining
