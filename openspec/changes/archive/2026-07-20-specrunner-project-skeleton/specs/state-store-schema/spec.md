## ADDED Requirements

### Requirement: State store schema associates issues, PRs, comments, and specs
`SpecRunner.Core` SHALL define record types capturing, at minimum: an
issue number, an optional PR number, a spec/change name, and a collection
of tracked comments, where each tracked comment records a comment
identifier, a comment kind, and a processing status (e.g.
pending/working/done/error).

#### Scenario: Association record links issue, PR, and spec name
- **WHEN** a tracked-issue record is created for issue number `45` with
  spec name `45-add-login-page` and no PR yet
- **THEN** the record SHALL expose the issue number, the spec name, and a
  null/absent PR number, with an empty tracked-comments collection

#### Scenario: Comment status is tracked per comment
- **WHEN** a comment is added to a tracked-issue or tracked-PR record with
  status `working`
- **THEN** looking up that comment by its identifier SHALL return status
  `working` until the record is updated to a different status

### Requirement: State store persistence interface
`SpecRunner.Core` SHALL define an `IStateStore` interface exposing
asynchronous load and save operations, plus lookup of an association
record by issue number and by PR number. `SpecRunner.State` SHALL provide
a JSON-file-backed implementation of `IStateStore` using a configurable
file path.

#### Scenario: Save then load round-trips state
- **WHEN** an association record is saved via `IStateStore.SaveAsync` and
  then loaded via `IStateStore.LoadAsync` from the same file path
- **THEN** the loaded record SHALL be equivalent to the saved record

#### Scenario: Lookup by issue number finds the associated spec
- **WHEN** the state store contains a record for issue number `45` and
  `IStateStore` is queried by issue number `45`
- **THEN** the matching record SHALL be returned

#### Scenario: Lookup by PR number finds the associated issue
- **WHEN** the state store contains a record with PR number `12` and
  `IStateStore` is queried by PR number `12`
- **THEN** the matching record SHALL be returned

### Requirement: Spec name resolution from issue number and title
`SpecRunner.Core` SHALL define an `ISpecNameResolver` with a fully
implemented default that produces spec/change names in the format
`{issue-number}-{sanitized-issue-title}`, where the title is lower-cased,
whitespace runs are replaced with single dashes, and characters invalid in
a filesystem folder name are removed.

#### Scenario: Title with spaces and mixed case
- **WHEN** resolving a spec name for issue number `45` with title
  `"Add Login Page"`
- **THEN** the resolver SHALL return `"45-add-login-page"`

#### Scenario: Title with invalid folder-name characters
- **WHEN** resolving a spec name for issue number `7` with title
  `"Fix: crash on save/load?"`
- **THEN** the resolver SHALL return a value containing no characters that
  are invalid in a filesystem folder name, with the number `7` as the
  leading segment

### Requirement: Git and GitHub service contracts are defined but unimplemented
`SpecRunner.Core` SHALL define `IGitService` (covering create branch,
switch branch, commit, push, and pull) and `IGitHubService` (covering
create PR, create draft PR, read PR comments, write PR comments, and mark
PR ready for review) as interfaces only. `SpecRunner.Git` and
`SpecRunner.GitHub` SHALL each provide a placeholder implementation that
compiles and can be registered in dependency injection, but that throws
`NotImplementedException` when any member is invoked.

#### Scenario: Placeholder git service throws when invoked
- **WHEN** a method on the registered `IGitService` implementation is
  called
- **THEN** it SHALL throw `NotImplementedException` rather than performing
  any git operation

#### Scenario: Placeholder GitHub service throws when invoked
- **WHEN** a method on the registered `IGitHubService` implementation is
  called
- **THEN** it SHALL throw `NotImplementedException` rather than performing
  any GitHub API call
