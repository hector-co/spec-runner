## ADDED Requirements

### Requirement: A scan pass finds eligible `/cancel` comments once per invocation
`SpecRunner.Core` SHALL define an `ICancelWorkflowRunner` with a single
`RunOnceAsync` operation. `SpecRunner.Console` SHALL provide the
implementation, which lists open issues and their comments and open pull
requests and their conversation comments via `IGitHubService`, and treats a
comment from either source as an eligible trigger when its body, trimmed of
leading/trailing whitespace, is exactly `/cancel` or starts with `/cancel`
followed by whitespace. An eligible comment sourced from an open issue SHALL
be recorded with kind `CommentKind.IssueComment` and target the issue's
number; an eligible comment sourced from `ReadPrCommentsAsync` SHALL be
recorded with kind `CommentKind.PrIssueComment` and target the PR's number.
File-anchored PR review comments SHALL NOT be scanned for `/cancel`.

#### Scenario: Exact-match issue comment is eligible
- **WHEN** a scan pass finds an open-issue comment whose trimmed body is
  exactly `/cancel`
- **THEN** that comment SHALL be treated as an eligible trigger targeting
  that issue's number

#### Scenario: Exact-match PR comment is eligible
- **WHEN** a scan pass finds an open-PR conversation comment whose trimmed
  body is exactly `/cancel`
- **THEN** that comment SHALL be treated as an eligible trigger targeting
  that PR's number

#### Scenario: Comment that merely contains the token is not eligible
- **WHEN** a scan pass finds a comment whose body contains `/cancel`
  somewhere other than as its leading token (e.g. mid-sentence, or as
  `/cancelled`)
- **THEN** that comment SHALL NOT be treated as an eligible trigger

#### Scenario: A file-anchored PR review comment is never eligible
- **WHEN** a scan pass encounters a PR review comment (as returned by
  `ListPrReviewCommentsAsync`) whose trimmed body is exactly `/cancel`
- **THEN** that comment SHALL NOT be treated as an eligible trigger

### Requirement: Comments already reacted to by the bot are skipped
A scan pass SHALL skip an otherwise-eligible `/cancel` comment if it already
carries an `eyes`, `+1`, or `confused` reaction from the authenticated bot
identity, so re-running the scan never reprocesses a `/cancel` comment that
is in-progress, done, or already reported as errored.

#### Scenario: Comment with an existing bot reaction is skipped
- **WHEN** an eligible `/cancel` comment already carries a `+1` reaction
  from the bot's own login
- **THEN** the scan pass SHALL NOT reprocess that comment

### Requirement: An eligible `/cancel` comment is only processed if its author is authorized
The workflow SHALL call `CommentAuthorization.IsAuthorized` with the
triggering comment's author and author association when building the list
of eligible comments, in addition to trigger-token matching. A comment
whose trigger token matches but whose author is not authorized SHALL NOT be
added to the eligible-comments list; the workflow SHALL instead log a
warning identifying the comment id, issue or PR number, and author, and
SHALL NOT add any reaction to the comment, post any reply, request
cancellation of any active run, or perform any git or GitHub-write
operation for it.

#### Scenario: A `/cancel` comment from an unauthorized author is silently skipped
- **WHEN** a scan pass finds a comment whose trimmed body is exactly
  `/cancel`, posted by an author whose `author_association` is `"NONE"` and
  who is not present in `AllowedTriggerUsers`
- **THEN** that comment SHALL NOT be treated as an eligible trigger, no
  reaction or reply SHALL be posted for it, no active run SHALL be
  cancelled, and a warning log entry recording the comment id, issue/PR
  number, and author SHALL be emitted

### Requirement: An eligible comment is marked in-progress before any other action
The workflow SHALL add an `eyes` reaction to a newly eligible `/cancel`
comment, as the first action taken for that comment, before looking up or
requesting cancellation of any active run, or performing any git or further
GitHub operation for it.

#### Scenario: Eyes reaction precedes any other work
- **WHEN** the workflow begins processing a newly eligible `/cancel` comment
- **THEN** an `eyes` reaction SHALL be added to that comment before any
  active-run lookup, git command, or additional GitHub call is made for it

### Requirement: A matching active run has its CLI-agent session stopped and its processing cancelled
The workflow SHALL resolve the target issue or PR number of the eligible
`/cancel` comment to a run key and look it up via `IActiveRunRegistry`. If a
matching `ActiveRun` is found, the workflow SHALL cancel that run's
cancellation token source, then call `StopAsync` on its `ICliAgentSession`
if one has been assigned, then await that run's completion task, bounded by
a fixed grace period, before proceeding.

#### Scenario: A matching active run is stopped
- **WHEN** an eligible `/cancel` comment targets issue `45`, and
  `IActiveRunRegistry` has an `ActiveRun` registered for issue `45` with a
  running `ICliAgentSession`
- **THEN** that run's cancellation token source SHALL be cancelled and
  `StopAsync` SHALL be called on its `ICliAgentSession`

#### Scenario: No matching active run is a no-op for the stop step
- **WHEN** an eligible `/cancel` comment targets a PR number for which
  `IActiveRunRegistry` has no registered `ActiveRun`
- **THEN** no cancellation token source SHALL be cancelled and no
  `ICliAgentSession` SHALL be stopped as part of this step

### Requirement: Uncommitted changes are discarded only when it is safe to touch the shared clone
After the stop step, the workflow SHALL call
`IGitService.ResetHardAsync("HEAD")` if and only if `IActiveRunRegistry`
reports no active run at all remains registered (whether because the
matching run just finished unwinding, or because none was ever active for
any issue/PR). If a different issue's or PR's run remains active in the
registry, the workflow SHALL NOT call `ResetHardAsync` and SHALL instead
report that nothing was running for the targeted issue/PR.

#### Scenario: Reset runs after the only active run is stopped
- **WHEN** an eligible `/cancel` comment targets PR `12`, `IActiveRunRegistry`
  had exactly one active run registered for PR `12`, and that run's
  completion task completes (or the grace period elapses) after being
  stopped
- **THEN** `IGitService.ResetHardAsync("HEAD")` SHALL be called

#### Scenario: Reset runs when nothing was active
- **WHEN** an eligible `/cancel` comment targets an issue for which
  `IActiveRunRegistry` has no active run, and no other run is active for any
  other issue/PR
- **THEN** `IGitService.ResetHardAsync("HEAD")` SHALL still be called

#### Scenario: Reset is skipped when a different run is still active
- **WHEN** an eligible `/cancel` comment targets issue `45`, for which
  `IActiveRunRegistry` has no active run, but PR `12` has a different active
  run currently registered
- **THEN** `IGitService.ResetHardAsync("HEAD")` SHALL NOT be called, and the
  workflow SHALL report that nothing was running for issue `45`

### Requirement: A successful cancellation is reported on the comment and in the state store
The workflow SHALL, after the reset step (or after determining no reset was
needed), add a `+1` reaction to the triggering `/cancel` comment, post a
reply confirming the outcome (that a run was cancelled and changes were
discarded, or that nothing was running for that issue/PR), and, if a tracked
record exists for the targeted issue or PR, upsert the state store setting
the status of the comment that originally triggered the now-stopped run to
`CommentStatus.Canceled`.

#### Scenario: A cancelled run's original triggering comment is marked Canceled
- **WHEN** the workflow stops an active run for PR `12` whose triggering
  comment id `9001` has a tracked record
- **THEN** the `/cancel` comment SHALL receive a `+1` reaction, a reply
  confirming the cancellation SHALL be posted, and comment `9001`'s status
  SHALL be upserted to `CommentStatus.Canceled` under PR `12`

#### Scenario: Cancelling when nothing was running still gets a clear reply
- **WHEN** the workflow processes an eligible `/cancel` comment targeting an
  issue with no active run and no other run active elsewhere
- **THEN** the `/cancel` comment SHALL receive a `+1` reaction and a reply
  indicating nothing was currently running for that issue

### Requirement: Errors are reported on the comment, not left silent
The workflow SHALL add a `confused` reaction to the triggering `/cancel`
comment and post a reply comment with a short, human-readable summary of the
failure (not a raw stack trace or exception dump) whenever any step of
processing an eligible `/cancel` comment throws. Processing SHALL then
continue to the next eligible comment in the scan pass rather than aborting
the whole run.

#### Scenario: An error during processing is reported and processing continues
- **WHEN** an unhandled failure occurs while processing an eligible
  `/cancel` comment
- **THEN** that comment SHALL receive a `confused` reaction and a
  human-readable reply summarizing the failure, and the scan pass SHALL
  continue processing any remaining eligible comments

### Requirement: Comments are processed sequentially within a scan pass
`RunOnceAsync` SHALL process eligible `/cancel` comments one at a time,
regardless of whether each is sourced from an issue or a PR, completing each
comment's full cycle before starting the next.

#### Scenario: A second eligible comment is not started until the first finishes
- **WHEN** a scan pass finds two eligible `/cancel` comments targeting
  different issues/PRs
- **THEN** processing of the second comment SHALL NOT begin until the first
  comment has reached a terminal outcome

### Requirement: The active-run registry tracks in-flight comment processing across workflows
`SpecRunner.Core` SHALL define `IActiveRunRegistry` with operations to
register an `ActiveRun` under a run key (an issue number or a PR number),
deregister a run key, look up the `ActiveRun` currently registered for a run
key, and report whether any run is currently registered. `SpecRunner.Console`
SHALL provide a single in-memory, thread-safe implementation registered as a
singleton, shared by `ProposeWorkflowRunner`, `ImplementWorkflowRunner`,
`UpdateWorkflowRunner`, `FinalizeWorkflowRunner`, and `CancelWorkflowRunner`.
An `ActiveRun` SHALL expose the `CancellationTokenSource` that scopes the
registered comment's processing, the `ICliAgentSession` assigned to it once
one has been started (absent before then), and a `Task` that completes when
that comment's processing returns.

#### Scenario: A registered run is retrievable by its run key
- **WHEN** `ProposeWorkflowRunner` registers an `ActiveRun` for issue `45`
  while processing an eligible `/propose` comment
- **THEN** `IActiveRunRegistry`'s lookup for issue `45` SHALL return that
  same `ActiveRun` until it is deregistered

#### Scenario: A deregistered run is no longer retrievable
- **WHEN** an `ActiveRun` registered for PR `12` is deregistered after its
  comment's processing completes
- **THEN** `IActiveRunRegistry`'s lookup for PR `12` SHALL report no active
  run

#### Scenario: Reporting whether any run is active reflects the whole registry
- **WHEN** `IActiveRunRegistry` has no `ActiveRun` registered under any run
  key
- **THEN** its "is any run active" operation SHALL return `false`
