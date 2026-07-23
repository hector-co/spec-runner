## Context

Four workflow runners (`ProposeWorkflowRunner`, `ImplementWorkflowRunner`,
`UpdateWorkflowRunner`, `FinalizeWorkflowRunner`) already share the same
per-comment shape: react `eyes`, resolve the tracked issue, refresh the
branch, run a CLI agent, commit/push, then report back via a reaction and a
comment. `/propose` sets the PR's initial title to `"Proposal for
#<issue-number>: <issue-name>"` when it creates the draft PR
(`ProposeWorkflowRunner.cs:165`). Nothing renames it afterward, so a PR
still reads "Proposal for..." long after `/implement` has pushed real code
or `/finalize` has archived the change and marked it ready for review.

`ImplementWorkflowRunner` already refreshes the PR description with the
current `tasks.md` content after a push
(`ImplementWorkflowRunner.cs:147-151`), using `ITasksFileReader
.ReadCurrentAsync` + `IGitHubService.UpdatePullRequestDescriptionAsync`.
`UpdateWorkflowRunner` has no equivalent step today — it commits and pushes
but never touches the PR description.

`TrackedIssue` (the state-store record) does not carry the issue's title
text today, only `IssueNumber`, `SpecName`, `BranchName`, `PrNumber`, and
comments. `GitHubPullRequest` (returned by `ListOpenPullRequestsAsync`,
which every one of these workflows already calls to find eligible comments)
does carry `Title`.

## Goals / Non-Goals

**Goals:**
- `/implement` renames the PR title to `"Implementations for
  #<issue-number>: <issue-name>"` after a successful push.
- `/finalize` renames the PR title to `"#<issue-number>: <issue-name>"`
  after a successful archive/push.
- `/update` refreshes the PR description with current `tasks.md` content
  after a successful push, matching `/implement`'s existing behavior.
- Add the missing `IGitHubService` primitive to rename a PR title.

**Non-Goals:**
- No change to `/propose`'s initial title format or its PR-creation flow.
- No state-store schema migration (no new persisted column).
- No change to how `/implement` already updates the PR description (only
  confirming it already works as specced).
- No retitling on error/timeout paths — only on a fully successful run,
  consistent with how the existing description/ready-for-review updates
  are also success-only.

## Decisions

### Derive `<issue-name>` from the PR's current title, not a new stored field
Every workflow that needs to rename the title already calls
`ListOpenPullRequestsAsync` and has the PR's current `Title` in hand before
it ever looks up the tracked issue. Since `/propose` always sets the title
to `"Proposal for #<issue-number>: <issue-name>"`, the issue name can be
recovered by locating the literal substring `"#<issue-number>: "` in the
current title and taking everything after it — no new `IssueTitle` field on
`TrackedIssue`, no state-store migration, no extra GitHub call.

Alternative considered: add `IssueTitle` to `TrackedIssue` and populate it
in `ProposeWorkflowRunner`. Rejected because it requires a schema change
for data that's already available for free on the PR object being renamed,
and because a renamed PR (e.g. by `/implement`) becomes the new source of
truth for the next rename (`/finalize` extracts from `/implement`'s title,
not the original `/propose` title) without any extra plumbing.

Fallback: if `"#<issue-number>: "` isn't found in the current title (e.g. a
human hand-edited it into some other shape), use the whole current title
as `<issue-name>` rather than throwing, so a rename never blocks the rest
of a successful run.

### Add `IGitHubService.UpdatePullRequestTitleAsync`
Mirrors the existing `UpdatePullRequestDescriptionAsync` exactly: PATCH
`/repos/{owner}/{repo}/pulls/{prNumber}` with `{ "title": title }`. Kept as
a separate call from the description update (not merged into one PATCH
body) to match the existing separation of concerns — each setter changes
exactly one field and callers compose them as needed.

### Where each workflow calls the new rename
- `ImplementWorkflowRunner`: after commit+push, alongside the existing
  `tasks.md` description refresh — call
  `UpdatePullRequestTitleAsync(prNumber, "Implementations for
  #{issueNumber}: {issueName}")` unconditionally (not gated on `tasks.md`
  being found, since renaming doesn't depend on task-list content).
- `FinalizeWorkflowRunner`: after commit+push and the existing description
  update, immediately before `MarkPrReadyForReviewAsync`, call
  `UpdatePullRequestTitleAsync(prNumber, "#{issueNumber}: {issueName}")`.
  Grouping the two PR-metadata updates before the ready-for-review
  transition keeps the "finalize" step's PR mutations together.
- `UpdateWorkflowRunner`: after commit+push, add the same
  `ITasksFileReader.ReadCurrentAsync` +
  `UpdatePullRequestDescriptionAsync` pair `ImplementWorkflowRunner`
  already uses, skipping the update if no `tasks.md` content is found
  (matching `implement-workflow`'s existing scenario for a missing file).
  This requires adding `ITasksFileReader` to `UpdateWorkflowRunner`'s
  constructor.

### Title-name extraction lives as a small shared helper, not inline duplication
Both `ImplementWorkflowRunner` and `FinalizeWorkflowRunner` need the same
"extract issue name from `#<issue-number>: ...` title" logic. It's added as
a small internal static helper (e.g. `PullRequestTitles.ExtractIssueName`)
in `SpecRunner.Console` rather than copy-pasted twice, and unit-tested
directly since it has a non-trivial fallback branch.

## Risks / Trade-offs

- [Risk] A human manually retitles a PR in a way that doesn't contain
  `"#<issue-number>: "` → Mitigation: fallback uses the whole current title
  as `<issue-name>`; the rename still succeeds, just with a less precise
  name, and no run is blocked.
- [Risk] `/implement` runs multiple times against the same PR → Mitigation:
  each run re-derives `<issue-name>` from whatever the current title is at
  that moment (which after the first `/implement` run is already
  `"Implementations for #N: <issue-name>"`, itself containing `"#N:
  <issue-name>"`), so repeated runs are idempotent and don't accumulate
  prefixes.
- [Risk] `/finalize` runs on a PR that was never touched by `/implement` →
  Mitigation: the same extraction works against the original `/propose`
  title (`"Proposal for #N: <issue-name>"`), since it also contains the
  `"#N: <issue-name>"` substring.

## Migration Plan

No data migration. Purely additive interface member plus new logic in
existing workflow runners; existing PRs simply get retitled/redescribed the
next time their tracked PR completes a `/implement`, `/update`, or
`/finalize` run.

## Open Questions

None — the unattended-run instruction directs reasonable assumptions where
ambiguous; see `proposal.md`'s Assumptions note.
