## MODIFIED Requirements

### Requirement: A fresh proposal run resets the clone, lands on a clean base branch, and creates a uniquely-named issue branch
The workflow SHALL, in order, for an eligible comment whose issue has no
existing PR: discard any uncommitted or untracked changes on whatever
branch is currently checked out via `IGitService.ResetHardAsync("HEAD")`,
switch to `SpecRunnerOptions.BaseBranchName` via
`IGitService.SwitchBranchAsync`, and pull it via `IGitService.PullAsync`.
It SHALL then compute a candidate branch name of `feature/{issue-number}`
and, if `IGitService.BranchExistsAsync` reports that name already exists
(locally or on `origin`), append `-2`, `-3`, and so on until it finds a
name that does not exist, before creating and switching to that branch via
`IGitService`.

#### Scenario: Working tree is cleaned before switching to the base branch
- **WHEN** the workflow processes an eligible comment on issue `45` while
  the local clone has uncommitted changes left over from a previous run,
  checked out on an unrelated branch
- **THEN** those changes SHALL be discarded before the clone is switched to
  `BaseBranchName`, and no attempt to switch branches SHALL be made while
  the previous branch's changes are still present

#### Scenario: Branch is created from a freshly reset and pulled base branch
- **WHEN** the workflow processes an eligible comment on issue `45` with
  no existing PR
- **THEN** the local clone SHALL be switched to `BaseBranchName` and pulled
  before a branch named `"feature/45"` is created and checked out, as long
  as no branch named `"feature/45"` already exists

#### Scenario: A colliding branch name gets a numeric suffix
- **WHEN** the workflow processes an eligible comment on issue `45` and a
  branch named `"feature/45"` already exists (locally or on `origin`), but
  `"feature/45-2"` does not
- **THEN** the workflow SHALL create and check out a new branch named
  `"feature/45-2"` instead of `"feature/45"`

## ADDED Requirements

### Requirement: The created branch name is persisted before the CLI agent runs
Immediately after creating and switching to the issue branch, and before
starting the CLI agent session, the workflow SHALL upsert a `TrackedIssue`
record via `IStateStore.UpsertTrackedIssueAsync` carrying the issue number,
the expected spec name (as returned by `ISpecNameResolver.Resolve`), and
the branch name that was just created, so the branch is recoverable even
if the run is interrupted before it completes.

#### Scenario: Branch name is recorded ahead of the CLI agent session
- **WHEN** the workflow creates and checks out branch `"feature/45"` for
  issue `45`
- **THEN** `IStateStore.FindByIssueNumberAsync(45)` SHALL return a record
  whose branch name is `"feature/45"` before the CLI agent session is
  started

### Requirement: The final report corrects the tracked spec name without losing the recorded branch name
When a completed CLI-agent run's actual on-disk spec name is resolved,
the workflow's success upsert (issue number, resolved actual spec name,
PR number) SHALL update the existing tracked-issue record in place rather
than being ignored, leaving its previously recorded branch name intact.

#### Scenario: Successful outcome corrects the spec name and keeps the branch name
- **WHEN** the workflow's early upsert recorded issue `45` with expected
  spec name `"feat-45-add-login-page"` and branch name `"feature/45"`, and
  the CLI agent run later resolves the actual on-disk spec name to
  `"45-add-login-page"`
- **THEN** `IStateStore.FindByIssueNumberAsync(45)` SHALL, after the
  success report, return a record with spec name `"45-add-login-page"`
  and branch name still `"feature/45"`
