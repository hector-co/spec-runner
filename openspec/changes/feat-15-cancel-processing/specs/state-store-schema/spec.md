## MODIFIED Requirements

### Requirement: State store schema associates issues, PRs, comments, and specs
`SpecRunner.Core` SHALL define record types capturing, at minimum: an
optional issue number, an optional PR number, a spec/change name, the name
of the git branch associated with the record, creation and last-updated
timestamps, and a collection of tracked comments, where each tracked
comment records a comment identifier, a comment kind, a processing status
(`pending`, `working`, `done`, `error`, or `canceled`), and creation and
last-updated timestamps. Comment kind SHALL be a closed enumeration
distinguishing at least an issue comment, a PR issue comment, and a PR
review comment. A record's issue number SHALL be absent when the record was
adopted from a PR with no associated GitHub issue (see `pr-adoption`); every
record other than one adopted this way continues to have an issue number
present. The `canceled` status SHALL be used to record a comment whose
processing was stopped via the `/cancel` workflow, distinct from `error`,
and SHALL persist and round-trip like any other status value without
requiring a database migration (comment status is stored by its enum name).

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

#### Scenario: A comment can be recorded with status canceled
- **WHEN** `IStateStore.UpsertCommentAsync` is called for a comment
  identifier with status `CommentStatus.Canceled`
- **THEN** looking up that comment by its identifier SHALL return status
  `canceled`, distinct from `error`

#### Scenario: A database file created before the canceled status existed still loads
- **WHEN** `IStateStore` opens a database file created by a version of the
  application whose `TrackedComments.Status` column never contained the
  value `"Canceled"`
- **THEN** the table SHALL require no migration, and existing rows SHALL
  remain readable and unchanged, since `Canceled` is simply an additional
  valid value for the existing `TEXT` column
