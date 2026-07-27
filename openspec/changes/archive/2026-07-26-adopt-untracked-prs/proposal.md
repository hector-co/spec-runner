## Why

`/implement`, `/update`, and `/finalize` only work on a PR that spec-runner's
own `/propose` flow created, because the state store's `TrackedIssues` row is
the only thing that tells those workflows which branch and spec/change folder
a PR belongs to. Any PR opened outside spec-runner — because the
issue-and-proposal phase happened by hand, or because the change has no
associated GitHub issue at all — is permanently rejected as "untracked," even
though everything those workflows need (the branch, the spec folder, and
optionally the issue) already exists and is discoverable from GitHub and the
git history. This change lets spec-runner adopt such a PR into tracking the
first time an eligible comment is processed, instead of refusing it forever.

## What Changes

- **BREAKING**: `TrackedIssue.IssueNumber` becomes nullable (`int?`); the
  `TrackedIssues.IssueNumber` SQLite column drops its `NOT NULL` constraint
  and its unique index becomes "unique where not null," matching the existing
  `PrNumber` column. Comment bookkeeping (`IStateStore.UpsertCommentAsync`)
  is keyed by PR number instead of issue number, since PR number is the one
  identifier guaranteed present on every tracked record (adopted or not).
- Add an adoption step to `implement-workflow`, `update-workflow`, and
  `finalize-workflow`: when an eligible trigger comment's PR has no tracked
  record, the workflow attempts to adopt the PR before falling back to
  today's refusal.
  - Discover the spec/change folder by diffing `openspec/changes/` between
    the PR's base and head branch. Exactly one added folder → adopt using
    that folder as the spec name. Zero or multiple added folders → refuse,
    with a specific reply naming the problem (no folder found, or which
    candidate folders are ambiguous), instead of the generic "opened by
    /propose" message.
  - Determine an optional associated issue via the GitHub GraphQL
    `closingIssuesReferences` field on the pull request. Zero linked issues
    is a valid outcome (adopt without an issue). Exactly one is adopted as
    the issue number. More than one → refuse, with a reply listing the
    candidate issue numbers.
  - The branch name is read directly from the PR's existing head branch
    (`GitHubPullRequest.HeadBranch`) — never reconstructed from commits or
    any naming convention.
  - On success, upsert a tracked record (branch name, spec name, PR number,
    and issue number if one was found) and continue processing the comment
    exactly as today's tracked-PR path does.
- Add `IGitHubService.ListClosingIssueNumbersAsync(prNumber)`, implemented via
  a GitHub GraphQL query for `closingIssuesReferences`, alongside the
  existing GraphQL mutation in `SpecRunner.GitHub`.
- Add `IGitService` support for discovering which top-level directories under
  `openspec/changes/` exist on a branch but not on the configured base
  branch (used for spec-folder discovery during adoption).
- For a tracked record with no issue number, every place that currently
  formats an issue number into a commit message, PR title, or PR body
  substitutes PR data instead:
  - Commit messages become `"applying/updating/finalizing specs for PR
    #{pr-number}"` instead of `"...for #{issue-number}"`.
  - PR title rewriting (`implement-workflow`'s `"Implementations for
    #{issue-number}: {issue-name}"` and `finalize-workflow`'s
    `"#{issue-number}: {issue-name}"`) uses the PR's own current title as
    `{issue-name}` unchanged, without the `#{issue-number}: ` prefix/marker
    logic.
  - `finalize-workflow`'s `"\n\nCloses #{issue-number}"` suffix on the final
    PR body is omitted entirely — there is no issue to close.
- A record adopted with an issue number behaves identically to one created by
  `/propose` from that point on; none of the null-issue substitutions above
  apply to it.

## Capabilities

### New Capabilities
- `pr-adoption`: discovering an untracked PR's spec folder and optional
  linked issue, and upserting a tracked record for it, shared by
  `implement-workflow`, `update-workflow`, and `finalize-workflow`.

### Modified Capabilities
- `state-store-schema`: `TrackedIssue.IssueNumber` becomes nullable, its
  uniqueness constraint becomes "unique where not null," and comment
  bookkeeping keys off PR number instead of issue number.
- `github-operations`: adds `ListClosingIssueNumbersAsync`, a GraphQL-backed
  operation returning the issue numbers a PR would close.
- `git-operations`: adds an operation to list `openspec/changes/` directories
  present on a branch but absent from the base branch.
- `implement-workflow`, `update-workflow`, `finalize-workflow`: the
  untracked-PR requirement changes from an unconditional refusal to an
  adoption attempt (falling back to refusal only when adoption fails), and
  the commit-message/PR-title/PR-body requirements gain a null-issue-number
  branch.

## Impact

- `SpecRunner.Core`: `TrackedIssue` model, `IStateStore`, `IGitHubService`,
  `IGitService` interface changes.
- `SpecRunner.State`: `SqliteStateStore` schema migration and query changes.
- `SpecRunner.GitHub`: new GraphQL query implementation.
- `SpecRunner.Git`: new branch-diff operation implementation.
- `SpecRunner.Console`: `ImplementWorkflowRunner`, `UpdateWorkflowRunner`,
  `FinalizeWorkflowRunner` gain adoption logic and null-issue-number
  formatting branches; `PullRequestTitles` gains a no-issue title path.
- Existing tracked-PR behavior (records created by `/propose`) is unchanged.

## Assumptions

- Archived via an unattended run. All artifacts and tasks were already
  complete at archive time, so no task checkboxes needed updating. Delta
  specs were unsynced against `openspec/specs/`, so — per the archive
  skill's recommended default — they were synced into the main specs before
  archiving rather than prompting for confirmation.
