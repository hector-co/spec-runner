## MODIFIED Requirements

### Requirement: A comment on an untracked PR triggers an adoption attempt
The workflow SHALL look up the triggering comment's PR via
`IStateStore.FindByPrNumberAsync`; if no record is found, it SHALL attempt
to adopt the PR as defined by the `pr-adoption` capability. If adoption
succeeds, the workflow SHALL continue processing the comment using the
newly upserted record exactly as it does for an already-tracked PR. If
adoption fails, the workflow SHALL reply on the PR with the adoption
failure's specific explanation, add a `confused` reaction to the triggering
comment, and SHALL NOT perform any git operation, CLI-agent session, or
state-store write for that comment.

#### Scenario: Untracked PR that adopts successfully proceeds like a tracked PR
- **WHEN** the workflow processes an eligible comment on PR `12`, the state
  store has no record with PR number `12`, and adoption resolves spec name
  `"add-csv-export"` and branch `"contributor/csv-export"` for it
- **THEN** a tracked record SHALL be upserted for PR `12` and the workflow
  SHALL proceed to refresh its branch and run the CLI agent exactly as for a
  previously tracked PR

#### Scenario: Untracked PR that fails adoption gets an explanatory reply and no further work
- **WHEN** the workflow processes an eligible comment on PR `12`, the state
  store has no record with PR number `12`, and adoption fails because no
  spec/change folder could be found
- **THEN** a reply comment explaining the adoption failure SHALL be posted,
  the triggering comment SHALL receive a `confused` reaction, and no git
  operation, CLI-agent session, or state-store write SHALL occur for that
  comment

### Requirement: A completed CLI-agent run is committed and pushed to the PR's existing branch
When the CLI agent session reaches state `Completed`, the workflow SHALL
commit all resulting changes via `IGitService.CommitAsync` and push the
tracked record's `BranchName` via `IGitService.PushAsync`. The commit
message SHALL be `"applying specs for #{issue-number}"` when the tracked
record has an issue number, or `"applying specs for PR #{pr-number}"` when
it does not. The workflow SHALL NOT create a new branch or a new pull
request, since the PR already exists.

#### Scenario: Successful session results in a push to the existing branch
- **WHEN** the CLI agent session for a tracked PR with issue number `45`
  and tracked `BranchName` `"feature/45"` reaches state `Completed`
- **THEN** the changes SHALL be committed with message
  `"applying specs for #45"` and the `"feature/45"` branch SHALL be pushed
  to `origin`, with no new branch or pull request created

#### Scenario: A tracked record with no issue number commits with a PR-number message
- **WHEN** the CLI agent session for a tracked PR with no issue number, PR
  number `12`, and tracked `BranchName` `"contributor/csv-export"` reaches
  state `Completed`
- **THEN** the changes SHALL be committed with message
  `"applying specs for PR #12"` and the `"contributor/csv-export"` branch
  SHALL be pushed to `origin`

### Requirement: A completed run renames the PR title to reflect implementation progress
After the existing commit-and-push step completes, the workflow SHALL
derive `<issue-name>` and the new title based on whether the tracked record
has an issue number. When it does, `<issue-name>` is the text following the
literal substring `"#{issue-number}: "` in the PR's current title (or the
whole current title if that substring is not found), and the new title is
`"Implementations for #{issue-number}: {issue-name}"`. When the tracked
record has no issue number, `<issue-name>` is the PR's current title
unchanged, and the new title is `"Implementations: {issue-name}"`. The
workflow SHALL call `IGitHubService.UpdatePullRequestTitleAsync` with the
tracked PR number and the resulting title, replacing the PR's existing
title. This rename SHALL happen regardless of whether `tasks.md` content was
found for the description refresh.

#### Scenario: PR title is renamed to reflect implementation after a push
- **WHEN** the workflow pushes changes for a tracked PR numbered `12` with
  issue number `45`, whose current title is `"Proposal for #45: Add login
  page"`
- **THEN** PR `12`'s title SHALL be updated to `"Implementations for #45:
  Add login page"` via `UpdatePullRequestTitleAsync`

#### Scenario: Rename still happens when no tasks.md content is found
- **WHEN** the workflow pushes changes for a tracked PR whose resolved spec
  name has no `tasks.md` on disk
- **THEN** the PR's title SHALL still be renamed, even though the
  description update is skipped

#### Scenario: A title with no recognizable "#issue-number:" segment falls back to the whole title
- **WHEN** the tracked PR's current title does not contain the literal
  substring `"#{issue-number}: "` (e.g. it was manually retitled)
- **THEN** the whole current title SHALL be used as `<issue-name>` in the
  new title, and the rename SHALL still be attempted rather than skipped
  or failing the run

#### Scenario: A tracked record with no issue number renames without a number prefix
- **WHEN** the workflow pushes changes for a tracked PR with no issue
  number, PR number `12`, whose current title is `"Add CSV export"`
- **THEN** PR `12`'s title SHALL be updated to `"Implementations: Add CSV
  export"` via `UpdatePullRequestTitleAsync`

### Requirement: A successful run reports back on the comment and in the state store
After pushing, the workflow SHALL add a `+1` reaction to the triggering
comment, post a reply comment confirming the push, and upsert the state
store with the comment's processing status set to `done` under the tracked
record's PR number.

#### Scenario: Successful outcome is reflected on GitHub and in the state store
- **WHEN** the workflow successfully pushes changes for a triggering
  comment on a tracked PR with PR number `12`
- **THEN** the triggering comment SHALL receive a `+1` reaction, a reply
  confirming the push SHALL be posted, and the state store SHALL record
  that comment's status as `done` under PR `12`

### Requirement: Errors and timeouts are reported on the comment, not left silent
The workflow SHALL add a `confused` reaction to the triggering comment,
post a reply comment with a short, human-readable summary of the failure
(not a raw stack trace or exception dump), and, for a comment on a tracked
PR, upsert the state store recording the comment's processing status as
`error` under the tracked record's PR number, whenever any step of
processing an eligible comment throws or the whole per-comment cycle
exceeds `SpecRunnerOptions.TaskTimeout` (in the timeout case, any
in-flight CLI agent session SHALL also be stopped via `StopAsync`).
Processing SHALL then continue to the next eligible comment in the scan
pass rather than aborting the whole run.

#### Scenario: An error during processing is reported and processing continues
- **WHEN** an unhandled failure occurs while processing an eligible
  comment on a tracked PR with PR number `12`
- **THEN** that comment SHALL receive a `confused` reaction and a
  human-readable reply summarizing the failure, its state-store status
  SHALL be `error` under PR `12`, and the scan pass SHALL continue
  processing any remaining eligible comments

#### Scenario: Exceeding the task timeout stops the agent and reports a timeout
- **WHEN** processing an eligible comment exceeds
  `SpecRunnerOptions.TaskTimeout` while a CLI agent session is running
- **THEN** the session SHALL be stopped via `StopAsync`, the comment SHALL
  receive a `confused` reaction, and the reply comment SHALL indicate that
  processing timed out
