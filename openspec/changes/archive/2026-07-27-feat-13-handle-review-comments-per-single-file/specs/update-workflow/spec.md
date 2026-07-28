## MODIFIED Requirements

### Requirement: A scan pass finds eligible `/update` comments once per invocation
`SpecRunner.Core` SHALL define an `IUpdateWorkflowRunner` with a single
`RunOnceAsync` operation. `SpecRunner.Console` SHALL provide the
implementation, which lists open pull requests via `IGitHubService`, and for
each one lists both its conversation comments (via `ReadPrCommentsAsync`)
and its review comments (via `ListPrReviewCommentsAsync`). The
implementation SHALL treat a comment from either source as an eligible
trigger when its body, trimmed of leading/trailing whitespace, is exactly
`/update` or starts with `/update` followed by whitespace, applying
identical token-matching to both sources. An eligible comment sourced from
`ReadPrCommentsAsync` SHALL be recorded with kind `CommentKind.PrIssueComment`
and no file name; an eligible comment sourced from
`ListPrReviewCommentsAsync` SHALL be recorded with kind
`CommentKind.PrReviewComment` and the file path GitHub reports for that
review comment.

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

#### Scenario: A file-anchored review comment matching the trigger token is eligible
- **WHEN** a scan pass finds a PR review comment anchored to file
  `"src/Login.cs"` whose trimmed body is `"/update the login button must
  say Sign In"`
- **THEN** that comment SHALL be treated as an eligible trigger with kind
  `CommentKind.PrReviewComment`, file name `"src/Login.cs"`, and
  instructions `"the login button must say Sign In"`

### Requirement: Comments already reacted to by the bot are skipped
A scan pass SHALL skip an otherwise-eligible comment if it already carries
an `eyes`, `+1`, or `confused` reaction from the authenticated bot identity,
so re-running the scan never reprocesses a comment that is in-progress,
done, or already reported as errored. `+1` stands in for a checkmark, since
the GitHub REST API's reaction set has no literal checkmark. For a comment
sourced from `ReadPrCommentsAsync` (kind `PrIssueComment`), reactions SHALL
be read via `ListCommentReactionsAsync`; for a comment sourced from
`ListPrReviewCommentsAsync` (kind `PrReviewComment`), reactions SHALL be read
via `ListReviewCommentReactionsAsync`.

#### Scenario: Comment with an existing bot reaction is skipped
- **WHEN** an eligible comment already carries a `+1` reaction from the
  bot's own login
- **THEN** the scan pass SHALL NOT reprocess that comment

#### Scenario: Comment with only a human reaction is still eligible
- **WHEN** an eligible comment carries an `eyes` reaction from a login
  other than the bot's
- **THEN** the scan pass SHALL still process that comment

#### Scenario: A review comment with an existing bot reaction is skipped
- **WHEN** an eligible review comment already carries a `+1` reaction from
  the bot's own login, as reported by `ListReviewCommentReactionsAsync`
- **THEN** the scan pass SHALL NOT reprocess that comment

### Requirement: An eligible `/update` comment is only processed if its author is authorized
The workflow SHALL call `CommentAuthorization.IsAuthorized` with the triggering comment's author and author association when building the list of eligible comments, in addition to trigger-token matching. A comment whose trigger token matches but whose author is not authorized SHALL NOT be added to the eligible-comments list; the workflow SHALL instead log a warning identifying the comment id, PR number, and author, and SHALL NOT add any reaction to the comment, post any reply, or perform any PR-adoption, git, CLI-agent, or GitHub-write operation for it.

#### Scenario: An `/update` comment from an unauthorized author is silently skipped
- **WHEN** a scan pass finds a PR comment whose trimmed body is exactly
  `/update`, posted by an author whose `author_association` is `"NONE"` and
  who is not present in `AllowedTriggerUsers`
- **THEN** that comment SHALL NOT be treated as an eligible trigger, no
  reaction or reply SHALL be posted for it, no PR-adoption attempt SHALL be
  made, and a warning log entry recording the comment id, PR number, and
  author SHALL be emitted

#### Scenario: An `/update` comment from an authorized author is processed as before
- **WHEN** a scan pass finds a PR comment whose trimmed body is exactly
  `/update`, posted by an author whose `author_association` is `"MEMBER"`
- **THEN** that comment SHALL be treated as an eligible trigger and
  processed following the existing `/update` workflow, unchanged by this
  requirement

### Requirement: An eligible comment is marked in-progress before any other action
The workflow SHALL add an `eyes` reaction to a newly eligible comment before
performing any git, CLI-agent, or further GitHub operation for it. For a
comment of kind `PrIssueComment` this SHALL use
`IGitHubService.AddCommentReactionAsync`; for a comment of kind
`PrReviewComment` this SHALL use `IGitHubService.AddReviewCommentReactionAsync`.

#### Scenario: Eyes reaction precedes any other work
- **WHEN** the workflow begins processing a newly eligible comment
- **THEN** an `eyes` reaction SHALL be added to that comment before any git
  command, CLI-agent session, or additional GitHub call is made for it

#### Scenario: A file-anchored comment receives its eyes reaction via the review-comment endpoint
- **WHEN** the workflow begins processing a newly eligible comment of kind
  `PrReviewComment`
- **THEN** an `eyes` reaction SHALL be added to that comment via
  `AddReviewCommentReactionAsync`, not `AddCommentReactionAsync`

### Requirement: A comment on an untracked PR triggers an adoption attempt
The workflow SHALL look up the triggering comment's PR via
`IStateStore.FindByPrNumberAsync`; if no record is found, it SHALL attempt
to adopt the PR as defined by the `pr-adoption` capability. If adoption
succeeds, the workflow SHALL continue processing the comment using the
newly upserted record exactly as it does for an already-tracked PR. If
adoption fails, the workflow SHALL add a `confused` reaction to the
triggering comment and reply to it with the adoption failure's specific
explanation, and SHALL NOT perform any git operation, CLI-agent session, or
state-store write for that comment. For a comment of kind
`PrIssueComment`, the reply SHALL be posted via `WritePrCommentAsync`; for
a comment of kind `PrReviewComment`, the reply SHALL be posted via
`ReplyToReviewCommentAsync` so it threads under the triggering review
comment.

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

#### Scenario: Untracked PR that fails adoption for a file-anchored comment gets a threaded reply
- **WHEN** the workflow processes an eligible review comment of kind
  `PrReviewComment` on PR `12`, the state store has no record with PR number
  `12`, and adoption fails because no spec/change folder could be found
- **THEN** the adoption failure's explanation SHALL be posted via
  `ReplyToReviewCommentAsync` so it threads under the triggering comment,
  that comment SHALL receive a `confused` reaction via
  `AddReviewCommentReactionAsync`, and no git operation, CLI-agent session,
  or state-store write SHALL occur for it

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

### Requirement: The CLI coding agent is run with a natural-language update instruction rendered from the `update` or `update-file` command template
After refreshing the branch, the workflow SHALL render a command template
via `ICommandTemplateRenderer` with `spec_name` set to the tracked record's
spec/change name and `instructions` set to the triggering comment's trimmed
body with the leading `/update` token and its separating whitespace
removed. For a comment of kind `PrIssueComment`, the workflow SHALL render
the `update` template. For a comment of kind `PrReviewComment`, the
workflow SHALL render the `update-file` template, additionally supplying
`file_name` set to the comment's recorded file path. The workflow SHALL
then start a new CLI agent session via `ICliAgentSessionFactory` and send
it the rendered template's content as the initial prompt, wrapped in a
literal pair of escaped double quotes (`\"...\"`), matching
`propose-workflow` and `implement-workflow`'s existing prompt-quoting
convention. Unlike `propose-workflow` and `implement-workflow`, neither
template's rendered content SHALL be an `/opsx-*` slash command. The
workflow SHALL then await the session reaching a terminal state
(`Completed` or `Failed`). No part of the prompt SHALL be built via C#
string interpolation.

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

#### Scenario: Prompt for a file-anchored comment includes the commented-on file
- **WHEN** the workflow runs the CLI agent for a tracked PR with spec name
  `"45-add-login-page"` and a triggering review comment anchored to file
  `"src/Login.cs"` with body `"/update the login button must say Sign
  In"`
- **THEN** the `update-file` template SHALL be rendered with `spec_name`
  set to `"45-add-login-page"`, `file_name` set to `"src/Login.cs"`, and
  `instructions` set to `"the login button must say Sign In"`, and the
  rendered text SHALL contain a `File: src/Login.cs` line between the
  change-name line and the instructions

### Requirement: A completed CLI-agent run is committed and pushed to the PR's existing branch
When the CLI agent session reaches state `Completed`, the workflow SHALL
commit all resulting changes via `IGitService.CommitAsync` and push the
tracked record's `BranchName` via `IGitService.PushAsync`. The commit
message SHALL be `"updating specs for #{issue-number}"` when the tracked
record has an issue number, or `"updating specs for PR #{pr-number}"` when
it does not. The workflow SHALL NOT create a new branch or a new pull
request, since the PR already exists.

#### Scenario: Successful session results in a push to the existing branch
- **WHEN** the CLI agent session for a tracked PR with issue number `45`
  and tracked `BranchName` `"feature/45"` reaches state `Completed`
- **THEN** the changes SHALL be committed with message `"updating specs
  for #45"` and the `"feature/45"` branch SHALL be pushed to `origin`,
  with no new branch or pull request created

#### Scenario: A tracked record with no issue number commits with a PR-number message
- **WHEN** the CLI agent session for a tracked PR with no issue number, PR
  number `12`, and tracked `BranchName` `"contributor/csv-export"` reaches
  state `Completed`
- **THEN** the changes SHALL be committed with message
  `"updating specs for PR #12"` and the `"contributor/csv-export"` branch
  SHALL be pushed to `origin`

### Requirement: A completed run refreshes the PR description with current task list content
After the existing commit-and-push step completes, the workflow SHALL read
the tracked record's spec name's current `tasks.md` content via
`ITasksFileReader.ReadCurrentAsync` and, if content is found, call
`IGitHubService.UpdatePullRequestDescriptionAsync` with the tracked PR
number and that content, replacing the PR's existing description. If no
`tasks.md` content is found, the workflow SHALL skip the description update
rather than clearing the PR body, matching `implement-workflow`'s existing
behavior for this same case.

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
comment as a checkmark, post a reply confirming the push, and upsert the
state store with the comment's processing status set to `done` under the
tracked record's PR number, recording the comment's own kind
(`CommentKind.PrIssueComment` or `CommentKind.PrReviewComment`). For a
comment of kind `PrIssueComment`, the reaction SHALL be added via
`AddCommentReactionAsync` and the reply via `WritePrCommentAsync`; for a
comment of kind `PrReviewComment`, the reaction SHALL be added via
`AddReviewCommentReactionAsync` and the reply via
`ReplyToReviewCommentAsync`.

#### Scenario: Successful outcome is reflected on GitHub and in the state store
- **WHEN** the workflow successfully pushes changes for a triggering
  comment on a tracked PR with PR number `12`
- **THEN** the triggering comment SHALL receive a `+1` reaction, a reply
  confirming the push SHALL be posted, and the state store SHALL record
  that comment's status as `done` under PR `12`

#### Scenario: Successful outcome for a file-anchored comment is reported via the review-comment endpoints
- **WHEN** the workflow successfully pushes changes for a triggering
  comment of kind `PrReviewComment` on a tracked PR with PR number `12`
- **THEN** the triggering comment SHALL receive a `+1` reaction via
  `AddReviewCommentReactionAsync`, a reply confirming the push SHALL be
  posted via `ReplyToReviewCommentAsync`, and the state store SHALL record
  that comment's status as `done` with kind `CommentKind.PrReviewComment`
  under PR `12`

### Requirement: Errors and timeouts are reported on the comment, not left silent
The workflow SHALL add a `confused` reaction to the triggering comment, post
a reply with a short, human-readable summary of the failure (not a raw
stack trace or exception dump), and, for a comment on a tracked PR, upsert
the state store recording the comment's processing status as `error` under
the tracked record's PR number and the comment's own kind, whenever any
step of processing an eligible comment throws or the whole per-comment
cycle exceeds `SpecRunnerOptions.TaskTimeout` (in the timeout case, any
in-flight CLI agent session SHALL also be stopped via `StopAsync`). For a
comment of kind `PrIssueComment`, the reaction and reply SHALL use
`AddCommentReactionAsync`/`WritePrCommentAsync`; for a comment of kind
`PrReviewComment`, they SHALL use
`AddReviewCommentReactionAsync`/`ReplyToReviewCommentAsync`. Processing
SHALL then continue to the next eligible comment in the scan pass rather
than aborting the whole run.

#### Scenario: An error during processing is reported and processing continues
- **WHEN** an unhandled failure occurs while processing an eligible comment
  on a tracked PR with PR number `12`
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

#### Scenario: An error processing a file-anchored comment is reported via the review-comment endpoints
- **WHEN** an unhandled failure occurs while processing an eligible comment
  of kind `PrReviewComment` on a tracked PR with PR number `12`
- **THEN** that comment SHALL receive a `confused` reaction via
  `AddReviewCommentReactionAsync` and a human-readable reply via
  `ReplyToReviewCommentAsync`, and its state-store status SHALL be `error`
  with kind `CommentKind.PrReviewComment` under PR `12`

### Requirement: Comments are processed sequentially within a scan pass
`RunOnceAsync` SHALL process eligible comments one at a time, regardless of
whether each is sourced from PR conversation comments or PR review
comments, completing each comment's full cycle (or recording its
error/timeout) before starting the next, since all comments in a scan pass
share the same local clone.

#### Scenario: A second eligible comment is not started until the first finishes
- **WHEN** a scan pass finds two eligible comments on different PRs
- **THEN** processing of the second comment SHALL NOT begin until the first
  comment has reached a terminal outcome (done or error)
