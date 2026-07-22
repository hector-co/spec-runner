## MODIFIED Requirements

### Requirement: Spec name resolution from issue number and title
`SpecRunner.Core` SHALL define an `ISpecNameResolver` with a fully
implemented default that produces spec/change names in the format
`feat-{issue-number}-{sanitized-issue-title}`, where the title is
lower-cased, whitespace runs are replaced with single dashes, and
characters invalid in a filesystem folder name are removed.

#### Scenario: Title with spaces and mixed case
- **WHEN** resolving a spec name for issue number `45` with title
  `"Add Login Page"`
- **THEN** the resolver SHALL return `"feat-45-add-login-page"`

#### Scenario: Title with invalid folder-name characters
- **WHEN** resolving a spec name for issue number `7` with title
  `"Fix: crash on save/load?"`
- **THEN** the resolver SHALL return a value containing no characters that
  are invalid in a filesystem folder name, starting with `"feat-7-"`
