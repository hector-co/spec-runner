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

### Requirement: A completed CLI-agent run is committed, pushed, and the PR is marked ready for review
When the CLI agent session reaches state `Completed`, the workflow SHALL
commit all resulting changes via `IGitService.CommitAsync`, push the
tracked record's `BranchName` via `IGitService.PushAsync`, read the
resolved spec name's archived `tasks.md` content via
`ITasksFileReader.ReadArchivedAsync`, build a final PR description from
that content (using an empty content prefix if no archived `tasks.md` is
found), update the PR's description via
`IGitHubService.UpdatePullRequestDescriptionAsync` with that final body, and
then mark the PR ready for review via
`IGitHubService.MarkPrReadyForReviewAsync`. The commit message SHALL be
`"finalizing specs for #{issue-number}"` when the tracked record has an
issue number, or `"finalizing specs for PR #{pr-number}"` when it does not.
When the tracked record has an issue number, the final PR description SHALL
have `"\n\nCloses #{issue-number}"` appended (so the closing link is always
present even when no archived `tasks.md` is found); when it does not, no
closing-link line SHALL be appended. The workflow SHALL NOT create a new
branch or a new pull request, since the PR already exists.

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

#### Scenario: A tracked record with no issue number commits with a PR-number message and no closing link
- **WHEN** the CLI agent session for a tracked PR with no issue number, PR
  number `12`, and tracked `BranchName` `"contributor/csv-export"` reaches
  state `Completed`, and
  `openspec/changes/archive/2026-07-21-add-csv-export/tasks.md` contains the
  final task list
- **THEN** the changes SHALL be committed with message `"finalizing specs
  for PR #12"`, the `"contributor/csv-export"` branch SHALL be pushed to
  `origin`, PR `12`'s description SHALL be updated to that `tasks.md`
  content with no closing-link line appended, and PR `12` SHALL then be
  marked ready for review

### Requirement: A completed run renames the PR title to reflect the finalized state
The workflow SHALL, after the existing commit-and-push step and the
existing description update, and before marking the PR ready for review,
derive `<issue-name>` and the new title based on whether the tracked record
has an issue number. When it does, `<issue-name>` is the text following the
literal substring `"#{issue-number}: "` in the PR's current title (or the
whole current title if that substring is not found), and the new title is
`"#{issue-number}: {issue-name}"`. When the tracked record has no issue
number, `<issue-name>` is the PR's current title unchanged, and the new
title is `<issue-name>` unchanged (no rename is performed beyond what the
description update already did). The workflow SHALL call
`IGitHubService.UpdatePullRequestTitleAsync` with the tracked PR number and
the resulting title whenever it differs from the PR's current title.

#### Scenario: PR title is renamed to its finalized form after archiving
- **WHEN** the workflow finalizes a tracked PR numbered `12` with issue
  number `45`, whose current title is `"Implementations for #45: Add login
  page"`
- **THEN** PR `12`'s title SHALL be updated to `"#45: Add login page"` via
  `UpdatePullRequestTitleAsync`, before the PR is marked ready for review

#### Scenario: A title with no recognizable "#issue-number:" segment falls back to the whole title
- **WHEN** the tracked PR's current title does not contain the literal
  substring `"#{issue-number}: "` (e.g. it was manually retitled)
- **THEN** the whole current title SHALL be used as `<issue-name>` in the
  new title `"#{issue-number}: {issue-name}"`, and the rename SHALL still
  be attempted rather than skipped or failing the run

#### Scenario: A tracked record with no issue number leaves the title as-is
- **WHEN** the workflow finalizes a tracked PR with no issue number and PR
  number `12`, whose current title is `"Implementations: Add CSV export"`
- **THEN** PR `12`'s title SHALL be left unchanged, and
  `UpdatePullRequestTitleAsync` SHALL NOT be called for the title rename
  step

### Requirement: A successful run reports back on the comment and in the state store
After marking the PR ready for review, the workflow SHALL add a `+1`
reaction to the triggering comment as a checkmark, post a reply comment
confirming the finalize, and upsert the state store with the comment's
processing status set to `done` under the tracked record's PR number.

#### Scenario: Successful outcome is reflected on GitHub and in the state store
- **WHEN** the workflow successfully marks PR `12` ready for review for a
  triggering comment
- **THEN** the triggering comment SHALL receive a `+1` reaction, a reply
  confirming the finalize SHALL be posted, and the state store SHALL
  record that comment's status as `done` under PR `12`

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
