# finalize-workflow

## Purpose

TBD - defines the `/finalize` comment workflow: scanning open PRs for
eligible `/finalize` trigger comments, refreshing the PR's branch, running
the CLI coding agent to archive the associated spec/change, committing and
pushing the result, marking the PR ready for review, and reporting success
or failure back on the comment and in the state store.

## Requirements

### Requirement: A scan pass finds eligible `/finalize` comments once per invocation
`SpecRunner.Core` SHALL define an `IFinalizeWorkflowRunner` with a single
`RunOnceAsync` operation. `SpecRunner.Console` SHALL provide the
implementation, which lists open pull requests and their comments via
`IGitHubService`, and treats a comment as an eligible trigger when its
body, trimmed of leading/trailing whitespace, is exactly `/finalize` or
starts with `/finalize` followed by whitespace.

#### Scenario: Exact-match comment is eligible
- **WHEN** a scan pass finds an open-PR comment whose trimmed body is
  exactly `/finalize`
- **THEN** that comment SHALL be treated as an eligible trigger

#### Scenario: Comment with trailing text after the token is eligible
- **WHEN** a scan pass finds a comment whose trimmed body starts with
  `/finalize` followed by a space or newline
- **THEN** that comment SHALL be treated as an eligible trigger, with the
  text after the token (and its separating whitespace) available as the
  comment's instructions

#### Scenario: Comment that merely contains the token is not eligible
- **WHEN** a scan pass finds a comment whose body contains `/finalize`
  somewhere other than as its leading token (e.g. mid-sentence, or as
  `/finalized`)
- **THEN** that comment SHALL NOT be treated as an eligible trigger

### Requirement: Comments already reacted to by the bot are skipped
A scan pass SHALL skip an otherwise-eligible comment if it already carries
an `eyes`, `+1`, or `confused` reaction from the authenticated bot
identity, so re-running the scan never reprocesses a comment that is
in-progress, done, or already reported as errored. `+1` stands in for a
checkmark, since the GitHub REST API's reaction set has no literal
checkmark.

#### Scenario: Comment with an existing bot reaction is skipped
- **WHEN** an eligible comment already carries a `+1` reaction from the
  bot's own login
- **THEN** the scan pass SHALL NOT reprocess that comment

#### Scenario: Comment with only a human reaction is still eligible
- **WHEN** an eligible comment carries an `eyes` reaction from a login
  other than the bot's
- **THEN** the scan pass SHALL still process that comment

### Requirement: An eligible comment is marked in-progress before any other action
The workflow SHALL add an `eyes` reaction to a newly eligible comment via
`IGitHubService`, as the first action taken for that comment, before
performing any git, CLI-agent, or further GitHub operation for it.

#### Scenario: Eyes reaction precedes any other work
- **WHEN** the workflow begins processing a newly eligible comment
- **THEN** an `eyes` reaction SHALL be added to that comment before any
  git command, CLI-agent session, or additional GitHub call is made for it

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

### Requirement: The CLI coding agent is run with a natural-language archive instruction rendered from the `archive` command template
After refreshing the branch, the workflow SHALL render the `archive`
command template via `ICommandTemplateRenderer` with `spec_name` set to
the tracked record's spec/change name and `instructions` set to the
triggering comment's trimmed body with the leading `/finalize` token and
its separating whitespace removed, start a new CLI agent session via
`ICliAgentSessionFactory`, and send it the rendered template's content as
the initial prompt, wrapped in a literal pair of escaped double quotes
(`\"...\"`), matching `propose-workflow`, `implement-workflow`, and
`update-workflow`'s existing prompt-quoting convention. Like
`update-workflow`, the `archive` template's rendered content SHALL NOT be
an `/opsx-*` slash command. The workflow SHALL then await the session
reaching a terminal state (`Completed` or `Failed`). No part of the
prompt SHALL be built via C# string interpolation.

#### Scenario: Prompt combines the resolved spec name and stripped comment body, plus the standing unattended-run instruction
- **WHEN** the workflow runs the CLI agent for a tracked PR with spec name
  `"45-add-login-page"` and a triggering comment body of `"/finalize the
  export button task was implemented under a different name"`
- **THEN** the `archive` template SHALL be rendered with `spec_name` set
  to `"45-add-login-page"` and `instructions` set to `"the export button
  task was implemented under a different name"`, and the session SHALL be
  started with an initial prompt whose content is that rendered text —
  beginning `"Run \`openspec archive \"45-add-login-page\" --yes\`. Mark
  missing tasks as completed and continue.\nthe export button task was
  implemented under a different name"` and ending with the standing
  unattended-run instruction block — wrapped in a literal pair of double
  quotes

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

### Requirement: Comments are processed sequentially within a scan pass
`RunOnceAsync` SHALL process eligible comments one at a time, completing
each comment's full cycle (or recording its error/timeout) before
starting the next, since all comments in a scan pass share the same local
clone.

#### Scenario: A second eligible comment is not started until the first finishes
- **WHEN** a scan pass finds two eligible comments on different PRs
- **THEN** processing of the second comment SHALL NOT begin until the
  first comment has reached a terminal outcome (done or error)
