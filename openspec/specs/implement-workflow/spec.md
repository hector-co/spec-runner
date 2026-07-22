# implement-workflow

## Purpose

TBD - defines the `implement-workflow` capability: scanning open pull
requests for eligible `/implement` comments, refreshing the PR's branch,
running the CLI coding agent with an `/opsx-apply` prompt, and reporting
success or failure back on the comment and in the state store.

## Requirements

### Requirement: A scan pass finds eligible `/implement` comments once per invocation
`SpecRunner.Core` SHALL define an `IImplementWorkflowRunner` with a single
`RunOnceAsync` operation. `SpecRunner.Console` SHALL provide the
implementation, which lists open pull requests and their comments via
`IGitHubService`, and treats a comment as an eligible trigger when its
body, trimmed of leading/trailing whitespace, is exactly `/implement` or
starts with `/implement` followed by whitespace.

#### Scenario: Exact-match comment is eligible
- **WHEN** a scan pass finds an open-PR comment whose trimmed body is
  exactly `/implement`
- **THEN** that comment SHALL be treated as an eligible trigger

#### Scenario: Comment with trailing text after the token is eligible
- **WHEN** a scan pass finds a comment whose trimmed body starts with
  `/implement` followed by a space or newline
- **THEN** that comment SHALL be treated as an eligible trigger, with the
  text after the token (and its separating whitespace) available as the
  comment's instructions

#### Scenario: Comment that merely contains the token is not eligible
- **WHEN** a scan pass finds a comment whose body contains `/implement`
  somewhere other than as its leading token (e.g. mid-sentence, or as
  `/implemented`)
- **THEN** that comment SHALL NOT be treated as an eligible trigger

### Requirement: Comments already reacted to by the bot are skipped
A scan pass SHALL skip an otherwise-eligible comment if it already carries
an `eyes`, `+1`, or `confused` reaction from the authenticated bot
identity, so re-running the scan never reprocesses a comment that is
in-progress, done, or already reported as errored.

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

### Requirement: A comment on an untracked PR is reported as an error
The workflow SHALL look up the triggering comment's PR via
`IStateStore.FindByPrNumberAsync`; if no record is found, it SHALL reply
on the PR with a message explaining that no associated spec/change was
found for this PR, add a `confused` reaction to the triggering comment,
and SHALL NOT perform any git operation, CLI-agent session, or
state-store write for that comment.

#### Scenario: Untracked PR gets an explanatory reply and no further work
- **WHEN** the workflow processes an eligible comment on PR `12` and the
  state store has no record with PR number `12`
- **THEN** a reply comment explaining that no associated spec/change was
  found SHALL be posted, the triggering comment SHALL receive a
  `confused` reaction, and no git operation, CLI-agent session, or
  state-store write SHALL occur for that comment

### Requirement: A tracked PR's branch is refreshed before the CLI agent runs
For an eligible comment whose PR has a tracked state-store record, the
workflow SHALL, in order: fetch the PR's head branch via
`IGitService.FetchAsync`, switch to it via `IGitService.SwitchBranchAsync`,
and hard-reset it to `origin/{branch}` via `IGitService.ResetHardAsync`,
so the local clone matches the PR's remote branch exactly before any
change is made.

#### Scenario: Branch is refreshed to match its remote tip
- **WHEN** the workflow processes an eligible comment on a tracked PR
  whose head branch is `"feature/45"`
- **THEN** `"feature/45"` SHALL be fetched, checked out, and hard-reset to
  `"origin/feature/45"` before the CLI agent is started

### Requirement: The CLI coding agent is run with an `/opsx-apply` prompt built from the resolved spec and comment instructions
After refreshing the branch, the workflow SHALL start a new CLI agent
session via `ICliAgentSessionFactory` and send it an initial prompt of
`/opsx-apply {spec-name} {instructions}`, where `spec-name` is the
tracked record's spec/change name and `instructions` is the triggering
comment's trimmed body with the leading `/implement` token and its
separating whitespace removed, sent as a single value wrapped in escaped
double quotes (`\"...\"`), matching `propose-workflow`'s existing
prompt-quoting convention, then await the session reaching a terminal
state (`Completed` or `Failed`).

#### Scenario: Prompt combines the resolved spec name and stripped comment body
- **WHEN** the workflow runs the CLI agent for a tracked PR with spec name
  `"45-add-login-page"` and a triggering comment body of
  `"/implement add validation for the email field"`
- **THEN** the session SHALL be started with initial prompt
  `"/opsx-apply 45-add-login-page add validation for the email field"`
  (the entire prompt wrapped in a literal pair of double quotes)

### Requirement: A completed CLI-agent run is committed and pushed to the PR's existing branch
When the CLI agent session reaches state `Completed`, the workflow SHALL
commit all resulting changes via `IGitService.CommitAsync` with message
`"applying specs for #{issue-number}"` (using the issue number from the
tracked record) and push the branch via `IGitService.PushAsync`. The
workflow SHALL NOT create a new branch or a new pull request, since the
PR already exists.

#### Scenario: Successful session results in a push to the existing branch
- **WHEN** the CLI agent session for a tracked PR with issue number `45`
  on branch `"feature/45"` reaches state `Completed`
- **THEN** the changes SHALL be committed with message
  `"applying specs for #45"` and the `"feature/45"` branch SHALL be pushed
  to `origin`, with no new branch or pull request created

### Requirement: A completed run refreshes the PR description with current task list content
After the existing commit-and-push step completes, the workflow SHALL read
the tracked record's spec name's current `tasks.md` content via
`ITasksFileReader.ReadCurrentAsync` and, if content is found, call
`IGitHubService.UpdatePullRequestDescriptionAsync` with the tracked PR
number and that content, replacing the PR's existing description. If no
`tasks.md` content is found, the workflow SHALL skip the description update
rather than clearing the PR body.

#### Scenario: PR description is replaced with the current task list after a push
- **WHEN** the workflow pushes changes for a tracked PR numbered `12` whose
  spec name's `tasks.md` currently contains an updated task list
- **THEN** PR `12`'s description SHALL be replaced with that `tasks.md`
  content via `UpdatePullRequestDescriptionAsync`

#### Scenario: Missing tasks.md leaves the existing PR description untouched
- **WHEN** the workflow pushes changes for a tracked PR whose resolved spec
  name has no `tasks.md` on disk
- **THEN** `UpdatePullRequestDescriptionAsync` SHALL NOT be called, and the
  PR's existing description SHALL be left unchanged

### Requirement: A successful run reports back on the comment and in the state store
After pushing, the workflow SHALL add a `+1` reaction to the triggering
comment, post a reply comment confirming the push, and upsert the state
store with the comment's processing status set to `done` under the
tracked record's issue number.

#### Scenario: Successful outcome is reflected on GitHub and in the state store
- **WHEN** the workflow successfully pushes changes for a triggering
  comment on a tracked PR with issue number `45`
- **THEN** the triggering comment SHALL receive a `+1` reaction, a reply
  confirming the push SHALL be posted, and the state store SHALL record
  that comment's status as `done` under issue `45`

### Requirement: Errors and timeouts are reported on the comment, not left silent
The workflow SHALL add a `confused` reaction to the triggering comment,
post a reply comment with a short, human-readable summary of the failure
(not a raw stack trace or exception dump), and, for a comment on a tracked
PR, upsert the state store recording the comment's processing status as
`error` under the tracked record's issue number, whenever any step of
processing an eligible comment throws or the whole per-comment cycle
exceeds `SpecRunnerOptions.TaskTimeout` (in the timeout case, any
in-flight CLI agent session SHALL also be stopped via `StopAsync`).
Processing SHALL then continue to the next eligible comment in the scan
pass rather than aborting the whole run.

#### Scenario: An error during processing is reported and processing continues
- **WHEN** an unhandled failure occurs while processing an eligible
  comment on a tracked PR with issue number `45`
- **THEN** that comment SHALL receive a `confused` reaction and a
  human-readable reply summarizing the failure, its state-store status
  SHALL be `error` under issue `45`, and the scan pass SHALL continue
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
