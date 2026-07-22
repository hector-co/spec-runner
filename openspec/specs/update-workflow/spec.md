# update-workflow

## Purpose

Defines the `/update` comment workflow: how `SpecRunner.Console` detects an
eligible `/update` comment on an open pull request, refreshes that PR's
branch, runs the CLI coding agent with a natural-language update
instruction built from the tracked spec/change and the comment body,
commits and pushes the result, and reports success or failure back on the
comment and in the state store.

## Requirements

### Requirement: A scan pass finds eligible `/update` comments once per invocation
`SpecRunner.Core` SHALL define an `IUpdateWorkflowRunner` with a single
`RunOnceAsync` operation. `SpecRunner.Console` SHALL provide the
implementation, which lists open pull requests and their comments via
`IGitHubService`, and treats a comment as an eligible trigger when its body,
trimmed of leading/trailing whitespace, is exactly `/update` or starts with
`/update` followed by whitespace.

#### Scenario: Exact-match comment is eligible
- **WHEN** a scan pass finds an open-PR comment whose trimmed body is
  exactly `/update`
- **THEN** that comment SHALL be treated as an eligible trigger

#### Scenario: Comment with trailing text after the token is eligible
- **WHEN** a scan pass finds a comment whose trimmed body starts with
  `/update` followed by a space or newline
- **THEN** that comment SHALL be treated as an eligible trigger, with the
  text after the token (and its separating whitespace) available as the
  comment's instructions

#### Scenario: Comment that merely contains the token is not eligible
- **WHEN** a scan pass finds a comment whose body contains `/update`
  somewhere other than as its leading token (e.g. mid-sentence, or as
  `/updated`)
- **THEN** that comment SHALL NOT be treated as an eligible trigger

### Requirement: Comments already reacted to by the bot are skipped
A scan pass SHALL skip an otherwise-eligible comment if it already carries
an `eyes`, `+1`, or `confused` reaction from the authenticated bot identity,
so re-running the scan never reprocesses a comment that is in-progress,
done, or already reported as errored. `+1` stands in for a checkmark, since
the GitHub REST API's reaction set has no literal checkmark.

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
- **THEN** an `eyes` reaction SHALL be added to that comment before any git
  command, CLI-agent session, or additional GitHub call is made for it

### Requirement: A comment on an untracked PR is reported as an error
The workflow SHALL look up the triggering comment's PR via
`IStateStore.FindByPrNumberAsync`; if no record is found, it SHALL reply on
the PR with a message explaining that no associated spec/change was found
for this PR, add a `confused` reaction to the triggering comment, and SHALL
NOT perform any git operation, CLI-agent session, or state-store write for
that comment.

#### Scenario: Untracked PR gets an explanatory reply and no further work
- **WHEN** the workflow processes an eligible comment on PR `12` and the
  state store has no record with PR number `12`
- **THEN** a reply comment explaining that no associated spec/change was
  found SHALL be posted, the triggering comment SHALL receive a `confused`
  reaction, and no git operation, CLI-agent session, or state-store write
  SHALL occur for that comment

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

### Requirement: The CLI coding agent is run with a natural-language update instruction rendered from the `update` command template
After refreshing the branch, the workflow SHALL render the `update`
command template via `ICommandTemplateRenderer` with `spec_name` set to
the tracked record's spec/change name and `instructions` set to the
triggering comment's trimmed body with the leading `/update` token and
its separating whitespace removed, start a new CLI agent session via
`ICliAgentSessionFactory`, and send it the rendered template's content as
the initial prompt, wrapped in a literal pair of escaped double quotes
(`\"...\"`), matching `propose-workflow` and `implement-workflow`'s
existing prompt-quoting convention. Unlike `propose-workflow` and
`implement-workflow`, the `update` template's rendered content SHALL NOT
be an `/opsx-*` slash command. The workflow SHALL then await the session
reaching a terminal state (`Completed` or `Failed`). No part of the
prompt SHALL be built via C# string interpolation.

#### Scenario: Prompt combines the resolved spec name and stripped comment body, plus the standing unattended-run instruction
- **WHEN** the workflow runs the CLI agent for a tracked PR with spec name
  `"45-add-login-page"` and a triggering comment body of `"/update the
  export button must also support CSV"`
- **THEN** the `update` template SHALL be rendered with `spec_name` set
  to `"45-add-login-page"` and `instructions` set to `"the export button
  must also support CSV"`, and the session SHALL be started with an
  initial prompt whose content is that rendered text — beginning
  `"Update the OpenSpec change \"45-add-login-page\" to reflect the
  following new requirement/information:\nthe export button must also
  support CSV"` and ending with the standing unattended-run instruction
  block — wrapped in a literal pair of double quotes

### Requirement: A completed CLI-agent run is committed and pushed to the PR's existing branch
When the CLI agent session reaches state `Completed`, the workflow SHALL
commit all resulting changes via `IGitService.CommitAsync` with message
`"updating specs for #{issue-number}"` (using the issue number from the
tracked record) and push the tracked record's `BranchName` via
`IGitService.PushAsync`. The workflow SHALL NOT create a new branch or a
new pull request, since the PR already exists.

#### Scenario: Successful session results in a push to the existing branch
- **WHEN** the CLI agent session for a tracked PR with issue number `45`
  and tracked `BranchName` `"feature/45"` reaches state `Completed`
- **THEN** the changes SHALL be committed with message `"updating specs
  for #45"` and the `"feature/45"` branch SHALL be pushed to `origin`,
  with no new branch or pull request created

### Requirement: A successful run reports back on the comment and in the state store
After pushing, the workflow SHALL add a `+1` reaction to the triggering
comment as a checkmark, post a reply comment confirming the push, and
upsert the state store with the comment's processing status set to `done`
under the tracked record's issue number.

#### Scenario: Successful outcome is reflected on GitHub and in the state store
- **WHEN** the workflow successfully pushes changes for a triggering
  comment on a tracked PR with issue number `45`
- **THEN** the triggering comment SHALL receive a `+1` reaction, a reply
  confirming the push SHALL be posted, and the state store SHALL record
  that comment's status as `done` under issue `45`

### Requirement: Errors and timeouts are reported on the comment, not left silent
The workflow SHALL add a `confused` reaction to the triggering comment, post
a reply comment with a short, human-readable summary of the failure (not a
raw stack trace or exception dump), and, for a comment on a tracked PR,
upsert the state store recording the comment's processing status as `error`
under the tracked record's issue number, whenever any step of processing an
eligible comment throws or the whole per-comment cycle exceeds
`SpecRunnerOptions.TaskTimeout` (in the timeout case, any in-flight CLI
agent session SHALL also be stopped via `StopAsync`). Processing SHALL then
continue to the next eligible comment in the scan pass rather than aborting
the whole run.

#### Scenario: An error during processing is reported and processing continues
- **WHEN** an unhandled failure occurs while processing an eligible comment
  on a tracked PR with issue number `45`
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
each comment's full cycle (or recording its error/timeout) before starting
the next, since all comments in a scan pass share the same local clone.

#### Scenario: A second eligible comment is not started until the first finishes
- **WHEN** a scan pass finds two eligible comments on different PRs
- **THEN** processing of the second comment SHALL NOT begin until the first
  comment has reached a terminal outcome (done or error)
