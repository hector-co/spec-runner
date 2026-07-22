## Why

Once a PR's implementation work is done, someone still has to manually run
`openspec archive`, tidy up any tasks the agent left unchecked, commit,
push, flip the PR out of draft, and report back. `propose-workflow`,
`implement-workflow`, and `update-workflow` already automate the
propose/implement/update legs of this loop from PR comments; there is no
equivalent for archiving a finished change and closing out the PR.

## What Changes

- Add a `/finalize` PR-comment trigger, scanned alongside `/update` and
  `/implement` on every polling pass.
- On an eligible comment: react `eyes` first, resolve the PR's tracked
  spec/change via the state store, refresh the local clone to the PR's
  branch (fetch, switch, hard-reset to `origin/{branch}`, matching
  `update-workflow`'s and `implement-workflow`'s existing refresh
  sequence), then run the CLI coding agent with a natural-language
  instruction (not an `/opsx-*` slash command, matching `update-workflow`'s
  convention) telling it to run `openspec archive "{spec-name}" --yes`,
  mark any missing tasks complete, and continue, followed by the comment's
  own instructions with the leading `/finalize` token stripped.
- On a successful agent run: commit, push to the PR's existing branch,
  mark the PR ready for review, react `+1` (checkmark) on the triggering
  comment, post a confirmation reply, and record the comment as `done` in
  the state store — mirroring `update-workflow`'s success reporting.
- On an untracked PR, agent failure, or timeout: report the same way
  `update-workflow` and `implement-workflow` already do (`confused`
  reaction, human-readable reply, `error` status), with no schema changes
  needed since the existing `TrackedIssue`/`TrackedComment` shape already
  captures everything this workflow needs to record.
- Implement `IGitHubService.MarkPrReadyForReviewAsync` for real. GitHub's
  REST API has no "mark ready for review" endpoint — flipping a PR out of
  draft requires the `markPullRequestReadyForReview` GraphQL mutation
  (which needs the PR's GraphQL node id, not its REST number), so this
  member's implementation looks different from this service's other,
  REST-only members.

## Capabilities

### New Capabilities
- `finalize-workflow`: scans open PRs for eligible `/finalize` comments,
  runs the archive-and-finalize CLI agent flow, and reports outcomes back
  on GitHub and in the state store.

### Modified Capabilities
- `github-operations`: `MarkPrReadyForReviewAsync` becomes a real
  implementation (via the GraphQL mutation) instead of a
  `NotImplementedException` placeholder.
- `state-store-schema`: its "implemented for the propose workflow"
  requirement still lists reading/writing PR comments and
  mark-ready-for-review as pending placeholders for "a future change
  that handles `/update`-style PR comments" — that change already
  landed for PR comments; this change is what that placeholder was
  actually waiting on for mark-ready-for-review, so the requirement is
  updated to match current + new reality (only non-draft
  `CreatePullRequestAsync` remains a placeholder).

## Impact

- `SpecRunner.Core`: new `IFinalizeWorkflowRunner` abstraction and
  `EligibleFinalizeComment` model.
- `SpecRunner.Console`: new `FinalizeWorkflowRunner` implementation,
  registered in `Program.cs`, and a third scan pass added to
  `PollingLoop.RunAsync`.
- `SpecRunner.GitHub`: `GitHubService.MarkPrReadyForReviewAsync` gains a
  real implementation using the GitHub GraphQL endpoint.
- `SpecRunner.Tests`: new tests for `FinalizeWorkflowRunner` mirroring
  `UpdateWorkflowRunnerTests`/`ImplementWorkflowRunnerTests`.
