## Why

`SpecRunner` can turn an issue into a draft PR via `/propose`, but once
that PR exists there is no way to drive further work on it through
GitHub comments — someone has to run the CLI coding agent by hand against
the local clone. A `/implement` comment on the PR should trigger the same
kind of watch-react-run-report cycle `/propose` already provides for
issues, but scoped to an existing PR and its associated spec/change.

## What Changes

- Add a new `implement-workflow` orchestration: each poll pass scans open
  PRs for comments whose trimmed body is exactly `/implement` or starts
  with `/implement` followed by whitespace, skipping any comment that
  already carries an `eyes`, `+1`, or `confused` reaction from the bot.
- Mark a newly eligible comment with an `eyes` reaction before any other
  action, mirroring `/propose`'s in-progress signal.
- Resolve the spec/change name for the comment's PR via
  `IStateStore.FindByPrNumberAsync`; if the PR has no tracked record (it
  wasn't opened by `/propose`), report the comment as an error instead of
  guessing a spec name.
- Refresh the local clone to the PR's existing head branch (not
  `BaseBranchName`) and run the CLI coding agent with initial prompt
  `"/opsx-apply {spec-name} {comment body with the leading /implement
  token removed}"`.
- On a completed run, commit and push the changes to the PR's existing
  branch (no new branch or PR is created — the PR already exists), then
  add a `+1` reaction to the triggering comment and post a reply
  confirming the push. `+1` is the closest available GitHub reaction to a
  checkmark (GitHub reactions are limited to `+1`, `-1`, `laugh`,
  `hooray`, `confused`, `heart`, `rocket`, `eyes` — there is no literal
  check-mark reaction).
- On an error or a `SpecRunnerOptions.TaskTimeout` timeout, add a
  `confused` reaction and post a human-readable failure summary,
  mirroring `/propose`'s error handling, then continue to the next
  eligible comment rather than aborting the scan pass.
- Extend `IGitHubService` with listing open PRs and implement
  `ReadPrCommentsAsync`/`WritePrCommentAsync` for real (previously
  `NotImplementedException` placeholders).
- Extend `IGitService` with fetching an arbitrary remote branch, so the
  workflow can refresh a PR's existing branch instead of only
  `BaseBranchName`.
- Extend the console entry point's poll loop to run an `implement-workflow`
  scan pass every cycle, sequentially after the `propose-workflow` scan
  pass (both share the one local clone, so they cannot run concurrently).
- Remove the unused `TrackedPr` model — `TrackedIssue.PrNumber` plus
  `IStateStore.FindByPrNumberAsync` already cover PR-keyed lookup, and
  storage-model adjustments are free to make without retaining backward
  compatibility at this stage.

## Capabilities

### New Capabilities
- `implement-workflow`: the `/implement` PR-comment-triggered
  orchestration — scanning open PRs for eligible comments, resolving the
  associated spec via the state store, refreshing the PR's branch,
  running the CLI agent with an `/opsx-apply` prompt, committing/pushing,
  and reporting success/error outcomes on GitHub and in the state store.

### Modified Capabilities
- `github-operations`: adds an operation to list open pull requests, and
  implements `ReadPrCommentsAsync`/`WritePrCommentAsync` for real instead
  of throwing `NotImplementedException`.
- `git-operations`: adds an operation to fetch an arbitrary remote branch
  by name (not just `BaseBranchName`), so an existing PR branch can be
  refreshed without discarding and recreating it.
- `solution-layout`: the console entry point's poll loop requirement
  changes from "run one `propose-workflow` scan pass per cycle" to "run
  one `propose-workflow` scan pass, then one `implement-workflow` scan
  pass, per cycle."

## Impact

- `SpecRunner.Core`: `IGitHubService` gains a list-open-PRs member;
  `IGitService` gains a fetch-branch member; new
  `IImplementWorkflowRunner` abstraction and supporting model(s) for an
  eligible PR comment; `TrackedPr` model removed.
- `SpecRunner.GitHub`: `GitHubService` implements the new list-open-PRs
  member and the previously-stubbed `ReadPrCommentsAsync`/
  `WritePrCommentAsync`.
- `SpecRunner.Git`: `GitService` implements the new fetch-branch member.
- `SpecRunner.Console`: new `ImplementWorkflowRunner`; `Program.cs` and
  `PollingLoop` updated to run both workflows sequentially each poll
  cycle; DI registration for the new runner.
- `SpecRunner.State`: no schema changes required (`TrackedIssue.PrNumber`
  and `TrackedComment` with `CommentKind.PrIssueComment` already cover
  what's needed); `TrackedPr.cs` deleted.
- Out of scope: `/update`-style follow-up comments on a PR that isn't a
  `/implement` trigger; marking the PR ready for review;
  `CreatePullRequestAsync` (non-draft); PR review-comment (inline code
  comment) handling — only general PR conversation comments are in scope.
