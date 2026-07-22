## MODIFIED Requirements

### Requirement: A tracked PR's branch is cleaned and refreshed from its recorded name before the CLI agent runs
The workflow SHALL, for an eligible comment whose PR has a tracked
state-store record and in this order: discard any uncommitted or
untracked changes on whatever branch is currently checked out via
`IGitService.ResetHardAsync("HEAD")`, fetch the tracked record's
`BranchName` via `IGitService.FetchAsync`, switch to it via
`IGitService.SwitchBranchAsync`, and hard-reset it to
`origin/{BranchName}` via `IGitService.ResetHardAsync`, so the local
clone matches the PR's remote branch exactly, with any local changes
discarded, before any change is made, even if the clone was left dirty on
an unrelated branch by a previous run. The workflow SHALL use the tracked
record's `BranchName` for this sequence (and for the later push), not the
PR's live head branch as reported by GitHub.

#### Scenario: Working tree is cleaned before switching to the tracked branch
- **WHEN** the workflow processes an eligible comment on a tracked PR
  while the local clone has uncommitted changes left over from a previous
  run, checked out on an unrelated branch
- **THEN** those changes SHALL be discarded before the clone is switched
  to the tracked record's branch

#### Scenario: Branch is refreshed to match its remote tip using the recorded branch name
- **WHEN** the workflow processes an eligible comment on a tracked PR
  whose tracked record has `BranchName` `"feature/45"`
- **THEN** `"feature/45"` SHALL be fetched, checked out, and hard-reset to
  `"origin/feature/45"` before the CLI agent is started, regardless of
  what branch name the PR currently reports on GitHub

### Requirement: A completed CLI-agent run is committed, pushed, and the PR is marked ready for review
When the CLI agent session reaches state `Completed`, the workflow SHALL
commit all resulting changes via `IGitService.CommitAsync` with message
`"finalizing specs for #{issue-number}"` (using the issue number from the
tracked record), push the tracked record's `BranchName` via
`IGitService.PushAsync`, read the resolved spec name's archived
`tasks.md` content via `ITasksFileReader.ReadArchivedAsync`, build a final
PR description of that content with `\n\nCloses #{issue-number}` appended
(using an empty content prefix if no archived `tasks.md` is found, so the
`Closes #{issue-number}` line is still always added), update the PR's
description via `IGitHubService.UpdatePullRequestDescriptionAsync` with
that final body, and then mark the PR ready for review via
`IGitHubService.MarkPrReadyForReviewAsync`. The workflow SHALL NOT create
a new branch or a new pull request, since the PR already exists.

#### Scenario: Successful session results in a push, an updated description with a closing link, and a ready-for-review PR
- **WHEN** the CLI agent session for a tracked PR with issue number `45`,
  tracked `BranchName` `"feature/45"`, and PR number `12` reaches state
  `Completed`, and
  `openspec/changes/archive/2026-07-21-45-add-login-page/tasks.md`
  contains the final task list
- **THEN** the changes SHALL be committed with message `"finalizing specs
  for #45"`, the `"feature/45"` branch SHALL be pushed to `origin`, PR
  `12`'s description SHALL be updated to that `tasks.md` content followed
  by `"\n\nCloses #45"`, and PR `12` SHALL then be marked ready for
  review, with no new branch or pull request created

#### Scenario: Missing archived tasks.md still appends the closing link
- **WHEN** the CLI agent session for a tracked PR with issue number `45`
  reaches state `Completed` but no archived `tasks.md` can be found for
  the resolved spec name
- **THEN** PR `12`'s description SHALL still be updated to end with
  `"Closes #45"`, and the PR SHALL still be marked ready for review
