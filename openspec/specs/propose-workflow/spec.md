# propose-workflow

## Purpose

TBD - defines the `/propose` comment-triggered orchestration: scanning
open issues for eligible comments, marking them in-progress, running the
CLI coding agent to produce specs on a fresh issue branch, and reporting
success/error outcomes back on GitHub and in the state store.

## Requirements

### Requirement: A scan pass finds eligible `/propose` comments once per invocation
`SpecRunner.Core` SHALL define an `IProposeWorkflowRunner` with a single
`RunOnceAsync` operation. `SpecRunner.Console` SHALL provide the
implementation, which lists open issues and their comments via
`IGitHubService`, and treats a comment as an eligible trigger when its
body, trimmed of leading/trailing whitespace, is exactly `/propose` or
starts with `/propose` followed by whitespace.

#### Scenario: Exact-match comment is eligible
- **WHEN** a scan pass finds an open-issue comment whose trimmed body is
  exactly `/propose`
- **THEN** that comment SHALL be treated as an eligible trigger

#### Scenario: Comment with trailing text after the token is eligible
- **WHEN** a scan pass finds a comment whose trimmed body starts with
  `/propose` followed by a space or newline
- **THEN** that comment SHALL be treated as an eligible trigger

#### Scenario: Comment that merely contains the token is not eligible
- **WHEN** a scan pass finds a comment whose body contains `/propose`
  somewhere other than as its leading token (e.g. mid-sentence, or as
  `/proposed`)
- **THEN** that comment SHALL NOT be treated as an eligible trigger

### Requirement: Comments already reacted to by the bot are skipped
A scan pass SHALL skip an otherwise-eligible comment if it already carries
an `eyes`, `rocket`, or `confused` reaction from the authenticated bot
identity, so re-running the scan never reprocesses a comment that is
in-progress, done, or already reported as errored.

#### Scenario: Comment with an existing bot reaction is skipped
- **WHEN** an eligible comment already carries a `rocket` reaction from
  the bot's own login
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

### Requirement: An issue that already has an associated PR short-circuits the workflow
The workflow SHALL NOT create a branch or run the CLI agent for an
eligible comment if `IStateStore.FindByIssueNumberAsync` returns an
existing record for that comment's issue with a non-null PR number; it
SHALL instead reply on the issue with `"This issue already has an active
Draft PR: #{pr-number}. Please add /update to the PR instead."` and mark
the triggering comment with a `rocket` reaction.

#### Scenario: Issue with an existing PR gets a redirect reply
- **WHEN** the workflow processes an eligible comment on issue `45` and
  the state store already has a record for issue `45` with PR number `12`
- **THEN** a reply comment with body `"This issue already has an active
  Draft PR: #12. Please add /update to the PR instead."` SHALL be posted,
  the triggering comment SHALL receive a `rocket` reaction, and no branch
  or CLI-agent session SHALL be created

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

### Requirement: The CLI coding agent is run with an `/opsx-propose` prompt rendered from the `propose` command template
After creating the issue branch, the workflow SHALL resolve the expected
spec name via `ISpecNameResolver` from the issue number and title, render
the `propose` command template via `ICommandTemplateRenderer` with
`spec_name` set to the resolved expected spec name and `issue_body` set to
the triggering issue's body, start a new CLI agent session via
`ICliAgentSessionFactory`, and send it the rendered template's content as
the initial prompt, wrapped in a literal pair of escaped double quotes
(`\"...\"`), then await the session reaching a terminal state
(`Completed` or `Failed`). No part of the prompt SHALL be built via C#
string interpolation.

#### Scenario: Prompt combines the resolved spec name and issue body, plus the standing unattended-run instruction
- **WHEN** the workflow runs the CLI agent for issue `45` titled
  `"Add Login Page"` with body `"We need a login page."`
- **THEN** the `propose` template SHALL be rendered with `spec_name` set
  to `"feat-45-add-login-page"` and `issue_body` set to `"We need a login
  page."`, and the session SHALL be started with an initial prompt whose
  content is that rendered text — beginning
  `"/opsx-propose feat-45-add-login-page\nWe need a login page."` and
  ending with the standing unattended-run instruction block — wrapped in a
  literal pair of double quotes

### Requirement: A completed CLI-agent run is committed, pushed, and opened as a draft PR
When the CLI agent session reaches state `Completed`, the workflow SHALL
resolve the actual on-disk spec name (per the `spec-folder-resolution`
capability's `ISpecFolderResolver.ResolveAsync`, using the expected spec
name and issue number), then commit all resulting changes via
`IGitService.CommitAsync` with message `"adding specs for #{issue-number}"`,
push the branch via `IGitService.PushAsync`, read the resolved actual spec
name's current `tasks.md` content via `ITasksFileReader.ReadCurrentAsync`
(using an empty string if no `tasks.md` is found), and create a draft PR
via `IGitHubService.CreateDraftPullRequestAsync` targeting
`SpecRunnerOptions.BaseBranchName` with that `tasks.md` content as the PR
body, instead of the triggering issue's body. The resolved actual spec name
(not the originally expected one) SHALL be used for all of these steps.

#### Scenario: Successful session results in a published branch and draft PR sourced from tasks.md
- **WHEN** the CLI agent session for issue `45` reaches state `Completed`
  and `openspec/changes/feat-45-add-login-page/tasks.md` contains a task
  list
- **THEN** the changes SHALL be committed with message `"adding specs for
  #45"`, the `feature/45` branch SHALL be pushed to `origin`, and a draft
  PR targeting `BaseBranchName` SHALL be created whose body is that
  `tasks.md` content (not the issue body)

#### Scenario: Missing tasks.md results in an empty PR body rather than a failure
- **WHEN** the CLI agent session for issue `45` reaches state `Completed`
  but no `tasks.md` exists for the resolved actual spec name
- **THEN** the draft PR SHALL still be created, with an empty body, and the
  workflow SHALL NOT fail or skip PR creation

### Requirement: A successful run reports the created PR back on the comment and in the state store
After a draft PR is created, the workflow SHALL add a `rocket` reaction to
the triggering comment, post a reply comment with body `"Created Draft PR
#{pr-number} for this issue."`, and upsert the state store with the
issue number, the resolved actual spec name (as returned by
`ISpecFolderResolver.ResolveAsync`, not the originally expected one), PR
number, and the comment's processing status set to `done`.

#### Scenario: Successful outcome is reflected on GitHub and in the state store
- **WHEN** a draft PR numbered `12` is created for issue `45`
- **THEN** the triggering comment SHALL receive a `rocket` reaction, a
  reply `"Created Draft PR #12 for this issue."` SHALL be posted, and the
  state store SHALL record issue `45` with PR `12` and the comment's
  status as `done`

### Requirement: An unresolvable spec folder halts the run before any commit, push, or PR
If, after the CLI agent session reaches state `Completed`,
`ISpecFolderResolver.ResolveAsync` cannot find a matching spec folder on
disk for the expected spec name and issue number (per
`spec-folder-resolution`), the workflow SHALL NOT commit, push, or create a
draft PR for that comment. This failure SHALL be reported through the same
error-reporting path as any other unhandled failure during comment
processing: a `confused` reaction, a human-readable reply, and a
state-store status of `error`.

#### Scenario: No matching spec folder stops the run before any git or GitHub write
- **WHEN** the CLI agent session for issue `45` reaches state `Completed`
  but no directory under `openspec/changes/` matches either the expected
  spec name or the `feat-45-` prefix
- **THEN** no commit, push, or draft PR SHALL be created for issue `45`,
  the triggering comment SHALL receive a `confused` reaction and a
  human-readable reply summarizing the failure, and the state-store status
  for that comment SHALL be `error`

### Requirement: Errors and timeouts are reported on the comment, not left silent
The workflow SHALL add a `confused` reaction to the triggering comment,
post a reply comment with a short, human-readable summary of the failure
(not a raw stack trace or exception dump), and upsert the state store
recording the comment's processing status as `error`, whenever any step
of processing an eligible comment throws or the whole per-comment cycle
exceeds `SpecRunnerOptions.TaskTimeout` (in the timeout case, any
in-flight CLI agent session SHALL also be stopped via `StopAsync`).
Processing SHALL then continue to the next eligible comment in the scan
pass rather than aborting the whole run.

#### Scenario: An error during processing is reported and processing continues
- **WHEN** an unhandled failure occurs while processing an eligible
  comment on issue `45`
- **THEN** that comment SHALL receive a `confused` reaction and a
  human-readable reply summarizing the failure, its state-store status
  SHALL be `error`, and the scan pass SHALL continue processing any
  remaining eligible comments

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
- **WHEN** a scan pass finds two eligible comments on different issues
- **THEN** processing of the second comment SHALL NOT begin until the
  first comment has reached a terminal outcome (done or error)
