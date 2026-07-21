## ADDED Requirements

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

### Requirement: A fresh proposal run resets the clone and creates the issue branch
The workflow SHALL, in order, for an eligible comment whose issue has no
existing PR: pull `SpecRunnerOptions.BaseBranchName` via
`IGitService.PullAsync`, discard local changes via
`IGitService.ResetHardAsync` against that branch, then create and switch
to a branch named `feature/{issue-number}` via `IGitService`.

#### Scenario: Branch is created from a freshly reset base branch
- **WHEN** the workflow processes an eligible comment on issue `45` with
  no existing PR
- **THEN** the local clone SHALL be pulled and hard-reset to
  `BaseBranchName` before a branch named `"feature/45"` is created and
  checked out

### Requirement: The CLI coding agent is run with an `/opsx-propose` prompt built from the issue
After creating the issue branch, the workflow SHALL resolve the spec name
via `ISpecNameResolver` from the issue number and title, start a new CLI
agent session via `ICliAgentSessionFactory`, and send it an initial prompt
of `"/opsx-propose {spec-name}\n{issue-body}"`, then await the session
reaching a terminal state (`Completed` or `Failed`).

#### Scenario: Prompt combines the resolved spec name and issue body
- **WHEN** the workflow runs the CLI agent for issue `45` titled
  `"Add Login Page"` with body `"We need a login page."`
- **THEN** the session SHALL be started with initial prompt
  `"/opsx-propose 45-add-login-page\nWe need a login page."`

### Requirement: A completed CLI-agent run is committed, pushed, and opened as a draft PR
When the CLI agent session reaches state `Completed`, the workflow SHALL
commit all resulting changes via `IGitService.CommitAsync` with message
`"adding specs for #{issue-number}"`, push the branch via
`IGitService.PushAsync`, and create a draft PR via
`IGitHubService.CreateDraftPullRequestAsync` targeting
`SpecRunnerOptions.BaseBranchName`.

#### Scenario: Successful session results in a published branch and draft PR
- **WHEN** the CLI agent session for issue `45` reaches state `Completed`
- **THEN** the changes SHALL be committed with message `"adding specs for
  #45"`, the `feature/45` branch SHALL be pushed to `origin`, and a draft
  PR targeting `BaseBranchName` SHALL be created

### Requirement: A successful run reports the created PR back on the comment and in the state store
After a draft PR is created, the workflow SHALL add a `rocket` reaction to
the triggering comment, post a reply comment with body `"Created Draft PR
#{pr-number} for this issue."`, and upsert the state store with the
issue number, resolved spec name, PR number, and the comment's processing
status set to `done`.

#### Scenario: Successful outcome is reflected on GitHub and in the state store
- **WHEN** a draft PR numbered `12` is created for issue `45`
- **THEN** the triggering comment SHALL receive a `rocket` reaction, a
  reply `"Created Draft PR #12 for this issue."` SHALL be posted, and the
  state store SHALL record issue `45` with PR `12` and the comment's
  status as `done`

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
