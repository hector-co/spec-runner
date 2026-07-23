## Why

A PR's title never changes after `/propose` creates it, so it stays stuck at
`"Proposal for #<issue-number>: <issue-name>"` even once `/implement` has
pushed code against it or `/finalize` has archived the change and marked it
ready for review. The title no longer reflects the PR's actual state,
forcing reviewers to rely on comments/reactions instead of the title to
judge progress. Separately, `/update` already refreshes code on the tracked
branch but never refreshes the PR description with the current task list,
unlike `/implement`, which already does this — this change closes that gap
too.

## What Changes

- `/implement` workflow: after a successful push, rename the PR title to
  `"Implementations for #<issue-number>: <issue-name>"`.
- `/finalize` workflow: after a successful archive/push, rename the PR title
  to `"#<issue-number>: <issue-name>"`.
- `/implement` workflow already updates the PR description with the current
  `tasks.md` content after a successful push — confirmed as already
  implemented (`ImplementWorkflowRunner`); no change needed here.
- `/update` workflow: after a successful push, update the PR description
  with the current `tasks.md` content, mirroring `/implement`'s existing
  behavior — confirmed this is **not** currently implemented
  (`UpdateWorkflowRunner` never calls `UpdatePullRequestDescriptionAsync`).
- Add a new `IGitHubService.UpdatePullRequestTitleAsync` operation (PATCH
  `/repos/{owner}/{repo}/pulls/{prNumber}` with `{ "title": title }`),
  implemented in `SpecRunner.GitHub`, mirroring the existing
  `UpdatePullRequestDescriptionAsync` pattern.
- Derive `<issue-name>` for the renamed title from the PR's current title
  (set by `/propose` as `"Proposal for #<issue-number>: <issue-name>"`) by
  extracting the text following `"#<issue-number>: "`, rather than adding a
  new persisted field — no state-store schema change is needed.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `github-operations`: adds `UpdatePullRequestTitleAsync` to `IGitHubService`
  and its real `SpecRunner.GitHub` implementation.
- `implement-workflow`: adds a PR-title rename to
  `"Implementations for #<issue-number>: <issue-name>"` after a successful
  push.
- `finalize-workflow`: adds a PR-title rename to
  `"#<issue-number>: <issue-name>"` after a successful archive/push (before
  or alongside marking the PR ready for review).
- `update-workflow`: adds a PR-description refresh with current `tasks.md`
  content after a successful push, matching `implement-workflow`'s existing
  behavior.

## Impact

- `SpecRunner.Core.Abstractions.IGitHubService` gains
  `UpdatePullRequestTitleAsync`.
- `SpecRunner.GitHub.GitHubService` gains the real implementation.
- `SpecRunner.Console.ImplementWorkflowRunner`,
  `SpecRunner.Console.FinalizeWorkflowRunner`, and
  `SpecRunner.Console.UpdateWorkflowRunner` gain new post-push steps.
- Test fakes (`FakeGitHubService`, `RecordingGitHubService`) and existing
  workflow unit tests need updates to cover the new calls.

## Assumptions

- "issue-name" in the renamed title means the human-readable issue title
  text already embedded in the PR's title by `/propose` (the same text used
  in `"Proposal for #<issue-number>: <issue-name>"`), not the slugified
  `SpecName` used for folder names — parsing it from the current PR title
  avoids a state-store schema change.
- If the current PR title doesn't match the expected
  `"... #<issue-number>: <issue-name>"` shape (e.g. a human manually
  renamed it), the workflow falls back to using the whole current title
  as `<issue-name>` rather than failing the run.
- `/finalize` renames the PR title in the same post-push step that already
  updates the description and marks the PR ready for review, before the
  success reaction/reply is posted.
- No new GitHub REST endpoint beyond the standard PR-update PATCH is
  required; title and description continue to be updated via separate
  calls (matching the existing separation of concerns) rather than
  combining them into one request body.
- Task 6.1 ("run the full suite and confirm all tests pass") is treated as
  satisfied with 8 pre-existing failures unrelated to this change: they are
  all CRLF-vs-LF string-comparison mismatches in CLI-agent prompt
  assertions (e.g. `CommandTemplateRendererTests`,
  `ProposeWorkflowRunnerTests`), caused by this checkout's line-ending
  normalization, and reproduce identically on the base branch before any
  of this change's edits (162/170 passing on `main` vs. 167/175 passing
  after this change — the same 8 tests fail in both cases; all newly
  added/updated assertions pass).
