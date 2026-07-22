# state-store-schema

## Purpose

TBD - defines the local state-store schema associating issues, PRs,
comments, and specs, the persistence interface, spec name resolution, and
the git/GitHub service contracts (implemented for the propose, implement,
update, and finalize workflows' needs; `CreatePullRequestAsync`
(non-draft) remains an unimplemented placeholder).

## Requirements

### Requirement: State store schema associates issues, PRs, comments, and specs
`SpecRunner.Core` SHALL define record types capturing, at minimum: an
issue number, an optional PR number, a spec/change name, the name of the
git branch associated with the issue, creation and last-updated
timestamps, and a collection of tracked comments, where each tracked
comment records a comment identifier, a comment kind, a processing status
(e.g. pending/working/done/error), and creation and last-updated
timestamps. Comment kind SHALL be a closed enumeration distinguishing at
least an issue comment, a PR issue comment, and a PR review comment.

#### Scenario: Association record links issue, PR, branch, and spec name
- **WHEN** a tracked-issue record is created for issue number `45` with
  spec name `45-add-login-page`, branch name `feature/45`, and no PR yet
- **THEN** the record SHALL expose the issue number, the spec name, the
  branch name, and a null/absent PR number, with an empty
  tracked-comments collection

#### Scenario: Comment status is tracked per comment
- **WHEN** a comment is added to a tracked-issue record with status
  `working`
- **THEN** looking up that comment by its identifier SHALL return status
  `working` until the record is updated to a different status

#### Scenario: Comment kind distinguishes issue and PR comment sources
- **WHEN** a tracked comment is created with kind `PrReviewComment`
- **THEN** the record SHALL expose that kind distinctly from
  `IssueComment` and `PrIssueComment`

#### Scenario: Timestamps are recorded on creation and update
- **WHEN** a tracked-issue record or tracked comment is first created
- **THEN** its creation and last-updated timestamps SHALL both be set,
  and the last-updated timestamp SHALL change on any subsequent update
  to that record

### Requirement: State store persistence interface
`SpecRunner.Core` SHALL define an `IStateStore` interface exposing:
lookup of a tracked-issue record by issue number, by PR number, and by
comment identifier; an upsert operation that inserts a new tracked issue
or updates the mutable fields (spec name, branch name, and PR number) of
an existing one keyed by issue number; and an upsert operation that
inserts a new tracked comment or updates the status of an existing one
keyed by comment identifier, under a given issue. `SpecRunner.State` SHALL
provide a SQLite-backed implementation of `IStateStore` using a
configurable database file path, with each operation performed as an
individual, atomic database operation rather than a whole-store
read/modify/write. The SQLite implementation SHALL add any new columns
required by this schema to a pre-existing database file (created by an
older version of the application) rather than requiring the file to be
recreated.

#### Scenario: Upsert then lookup round-trips a tracked issue
- **WHEN** a tracked-issue record is created via
  `IStateStore.UpsertTrackedIssueAsync` and then looked up via
  `IStateStore.FindByIssueNumberAsync` using the same database file path
- **THEN** the looked-up record SHALL be equivalent to the upserted
  record

#### Scenario: Lookup by issue number finds the associated spec
- **WHEN** the state store contains a record for issue number `45` and
  `IStateStore` is queried by issue number `45`
- **THEN** the matching record SHALL be returned

#### Scenario: Lookup by PR number finds the associated issue
- **WHEN** the state store contains a record with PR number `12` and
  `IStateStore` is queried by PR number `12`
- **THEN** the matching record SHALL be returned

#### Scenario: Lookup by comment id finds the owning tracked issue
- **WHEN** the state store contains a tracked comment with comment
  identifier `9001` under the record for issue number `45`, and
  `IStateStore` is queried by comment identifier `9001`
- **THEN** the record for issue number `45` SHALL be returned

#### Scenario: Upserting an existing tracked issue updates its mutable fields
- **WHEN** `IStateStore.UpsertTrackedIssueAsync` is called a second time
  for an issue number that already has a record, supplying a PR number
  and a different spec name than was originally recorded
- **THEN** the existing record SHALL be updated with the supplied PR
  number and the new spec name rather than a duplicate record being
  created, and its last-updated timestamp SHALL change

#### Scenario: Upserting an existing tracked issue updates its branch name
- **WHEN** `IStateStore.UpsertTrackedIssueAsync` is called a second time
  for an issue number that already has a record, supplying a different
  branch name than was originally recorded
- **THEN** the existing record SHALL be updated with the supplied branch
  name rather than a duplicate record being created

#### Scenario: Upserting an existing tracked comment updates its status
- **WHEN** `IStateStore.UpsertCommentAsync` is called a second time for a
  comment identifier that already exists, supplying status `done`
- **THEN** the existing comment record SHALL be updated to status `done`
  rather than a duplicate comment record being created

#### Scenario: A database file created before the branch-name column existed still loads
- **WHEN** `IStateStore` opens a database file created by a version of
  the application that predates the `BranchName` column
- **THEN** the `TrackedIssues` table SHALL be upgraded in place to include
  the `BranchName` column, and existing rows SHALL remain readable

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

### Requirement: Git and GitHub service contracts are implemented for the propose workflow
`SpecRunner.Core` SHALL define `IGitService` (covering create branch,
switch branch, commit, push, pull, and discarding local changes via a
hard reset) and `IGitHubService` (covering create PR, create draft PR,
read PR comments, write PR comments, mark PR ready for review, resolving
the authenticated bot identity, listing open issues with comments,
reading/adding comment reactions, and creating an issue comment) as
interfaces. `SpecRunner.Git` SHALL provide a real implementation of every
`IGitService` member (see `git-operations`). `SpecRunner.GitHub` SHALL
provide a real implementation of every `IGitHubService` member the
`propose-workflow`, `implement-workflow`, `update-workflow`, and
`finalize-workflow` capabilities depend on — authenticated identity
resolution, listing open issues with comments, reading/adding comment
reactions, creating an issue comment, creating a draft PR, listing open
pull requests, reading/writing PR comments, and marking a PR ready for
review (see `github-operations`) — while `CreatePullRequestAsync`
(non-draft) remains a `NotImplementedException` placeholder until a
future change needs it.

#### Scenario: Git service operations perform real git operations
- **WHEN** a method on the registered `IGitService` implementation is
  called
- **THEN** it SHALL perform the corresponding git operation against the
  local clone rather than throwing `NotImplementedException`

#### Scenario: Implemented GitHub service members perform real API calls
- **WHEN** `CreateDraftPullRequestAsync`, an issue-listing operation, a
  comment-reaction operation, `CreateIssueCommentAsync`, a PR-listing
  operation, a PR-comment read/write operation, or the
  mark-ready-for-review operation is called on the registered
  `IGitHubService` implementation
- **THEN** it SHALL perform the corresponding GitHub REST or GraphQL API
  call rather than throwing `NotImplementedException`

#### Scenario: Not-yet-needed GitHub service members remain placeholders
- **WHEN** `CreatePullRequestAsync` (non-draft) is called on the
  registered `IGitHubService` implementation
- **THEN** it SHALL still throw `NotImplementedException`, since no
  current capability depends on it yet
