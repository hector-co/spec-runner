## MODIFIED Requirements

### Requirement: State store schema associates issues, PRs, comments, and specs
`SpecRunner.Core` SHALL define record types capturing, at minimum: an
optional issue number, an optional PR number, a spec/change name, the name
of the git branch associated with the record, creation and last-updated
timestamps, and a collection of tracked comments, where each tracked
comment records a comment identifier, a comment kind, a processing status
(e.g. pending/working/done/error), and creation and last-updated
timestamps. Comment kind SHALL be a closed enumeration distinguishing at
least an issue comment, a PR issue comment, and a PR review comment. A
record's issue number SHALL be absent when the record was adopted from a PR
with no associated GitHub issue (see `pr-adoption`); every record other than
one adopted this way continues to have an issue number present.

#### Scenario: Association record links issue, PR, branch, and spec name
- **WHEN** a tracked-issue record is created for issue number `45` with
  spec name `45-add-login-page`, branch name `feature/45`, and no PR yet
- **THEN** the record SHALL expose the issue number, the spec name, the
  branch name, and a null/absent PR number, with an empty
  tracked-comments collection

#### Scenario: Association record supports no linked issue
- **WHEN** a tracked-issue record is created via adoption for PR number `12`
  with spec name `"add-csv-export"` and branch name
  `"contributor/csv-export"`, with no discovered issue
- **THEN** the record SHALL expose a null/absent issue number alongside the
  spec name, branch name, and PR number `12`

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
`SpecRunner.Core` SHALL define an `IStateStore` interface exposing: lookup
of a tracked-issue record by issue number, by PR number, and by comment
identifier; an upsert operation that inserts a new tracked issue or updates
the mutable fields (spec name, branch name, issue number, and PR number) of
an existing one keyed by PR number when a PR number is supplied, or by issue
number otherwise; and an upsert operation that inserts a new tracked comment
or updates the status of an existing one keyed by comment identifier, under
a given PR number. `SpecRunner.State` SHALL provide a SQLite-backed
implementation of `IStateStore` using a configurable database file path,
with each operation performed as an individual, atomic database operation
rather than a whole-store read/modify/write. The `TrackedIssues` table's
`IssueNumber` column SHALL allow null values, with uniqueness enforced only
among non-null values (matching the existing `PrNumber` column's
uniqueness constraint), so a record adopted from an issue-less PR does not
collide with, or get confused for, any genuinely tracked issue. The SQLite
implementation SHALL add any new columns or relax any existing constraints
required by this schema on a pre-existing database file (created by an
older version of the application) rather than requiring the file to be
recreated, preserving all existing rows.

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

#### Scenario: A tracked comment is upserted for a record with no issue number
- **WHEN** `IStateStore.UpsertCommentAsync` is called for PR number `12`,
  which has a tracked record with no issue number, supplying a new comment
  identifier and status `working`
- **THEN** a new tracked comment SHALL be created under that record, keyed
  by PR number `12` rather than by any issue number

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

#### Scenario: A database file created before IssueNumber was nullable still loads
- **WHEN** `IStateStore` opens a database file created by a version of the
  application whose `TrackedIssues.IssueNumber` column was `NOT NULL`
- **THEN** the table SHALL be upgraded in place so `IssueNumber` allows
  null values, and all existing rows (which all have a non-null issue
  number) SHALL remain readable and unchanged
