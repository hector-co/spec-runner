## MODIFIED Requirements

### Requirement: A tracked PR's branch is cleaned and refreshed from its recorded name before the CLI agent runs
For an eligible comment whose PR has a tracked state-store record, the
workflow SHALL, in order: discard any uncommitted or untracked changes on
whatever branch is currently checked out via
`IGitService.ResetHardAsync("HEAD")`, fetch the tracked record's
`BranchName` via `IGitService.FetchAsync`, switch to it via
`IGitService.SwitchBranchAsync`, and hard-reset it to
`origin/{BranchName}` via `IGitService.ResetHardAsync`, so the local
clone matches the PR's remote branch exactly before any change is made,
even if the clone was left dirty on an unrelated branch by a previous run.
The workflow SHALL use the tracked record's `BranchName` for this sequence
(and for the later push), not the PR's live head branch as reported by
GitHub.

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

### Requirement: A completed CLI-agent run is committed and pushed to the PR's existing branch
When the CLI agent session reaches state `Completed`, the workflow SHALL
commit all resulting changes via `IGitService.CommitAsync` with message
`"applying specs for #{issue-number}"` (using the issue number from the
tracked record) and push the tracked record's `BranchName` via
`IGitService.PushAsync`. The workflow SHALL NOT create a new branch or a
new pull request, since the PR already exists.

#### Scenario: Successful session results in a push to the existing branch
- **WHEN** the CLI agent session for a tracked PR with issue number `45`
  and tracked `BranchName` `"feature/45"` reaches state `Completed`
- **THEN** the changes SHALL be committed with message
  `"applying specs for #45"` and the `"feature/45"` branch SHALL be pushed
  to `origin`, with no new branch or pull request created
