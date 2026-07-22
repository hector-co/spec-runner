## Context

Today the PR description is set exactly once, at `propose` time, to the raw
GitHub issue body (`ProposeWorkflowRunner`, via
`IGitHubService.CreateDraftPullRequestAsync`). `implement` and `finalize`
never touch the PR's title/body afterwards — `IGitHubService` has no
operation to update an existing PR at all; the only existing PR-mutation
member is `MarkPrReadyForReviewAsync` (GraphQL). Each workflow already knows
which on-disk change/spec folder it's working with:
`TrackedIssue.SpecName`, recovered via `IStateStore.FindByIssueNumberAsync`
(propose) or `FindByPrNumberAsync` (implement/finalize) — so no new
persistence is needed to locate `tasks.md`.

The one wrinkle: `finalize` runs `openspec archive "{spec-name}" --yes` as
part of its CLI-agent prompt, which moves the change directory from
`openspec/changes/{spec-name}/` to `openspec/changes/archive/{date}-{spec-name}/`
before the workflow does its own commit/push. By the time `finalize` wants
to read the final task list, `tasks.md` no longer lives at the pre-archive
path — the date prefix isn't known in advance (it's assigned by the
`openspec` CLI at archive time using its own clock).

## Goals / Non-Goals

**Goals:**
- `propose` seeds the draft PR body from the generated `tasks.md`, not the
  raw issue body.
- `implement` overwrites the PR body with the current `tasks.md` content
  after every successful push.
- `finalize` overwrites the PR body with the final `tasks.md` content and
  appends `Closes #{issue-number}`, after archive/push and before (or as
  part of) marking the PR ready for review.
- Add the missing `IGitHubService` member to update an existing PR's body.

**Non-Goals:**
- `update-workflow` is not changed. The user-facing request only covers
  propose/implement/finalize; syncing `update`'s PR description is a
  natural follow-up but left for a future change to avoid scope creep.
- No PR merge or GitHub-side issue-closing logic is added — `Closes #N` is
  plain text relying on GitHub's own merge-time auto-linking.
- No new state-store schema/columns — `SpecName` already resolves 1:1 to
  the on-disk folder name (pre- or post-archive).

## Decisions

### Read `tasks.md` through a small new Core abstraction, not raw `File.IO` in the runners
Every other side effect the runners perform (git, GitHub, CLI-agent) is
behind an interface in `SpecRunner.Core.Abstractions` so `*WorkflowRunnerTests`
can substitute fakes. Reading `tasks.md` is the first "read the working
tree's files" need in the codebase, so it gets its own narrow interface
rather than a general-purpose `IFileSystem`:

```csharp
public interface ITasksFileReader
{
    Task<string?> ReadCurrentAsync(string specName, CancellationToken ct = default);
    Task<string?> ReadArchivedAsync(string specName, CancellationToken ct = default);
}
```

- `ReadCurrentAsync` reads `{LocalRepositoryPath}/openspec/changes/{specName}/tasks.md`
  (used by `propose` and `implement`). Returns `null` if the file doesn't exist.
- `ReadArchivedAsync` globs
  `{LocalRepositoryPath}/openspec/changes/archive/*-{specName}/tasks.md`
  (used by `finalize`, called after the archive-and-commit step). If more
  than one directory matches the `-{specName}` suffix, the most recently
  modified `tasks.md` wins; if none match, returns `null`.
- Implementation lives in `SpecRunner.Console` (or a new small
  `SpecRunner.OpenSpec` project alongside `SpecRunner.Git`/`SpecRunner.GitHub`
  if a natural home doesn't already exist) and reads
  `SpecRunnerOptions.LocalRepositoryPath` directly with `System.IO`, mirroring
  how `GitService` reads the same option for its working directory.

Alternative considered: read the file inline with `File.ReadAllTextAsync`
directly inside each runner. Rejected — it would be untestable without
touching a real filesystem, breaking the existing all-fakes unit-test style
in `ProposeWorkflowRunnerTests`/`ImplementWorkflowRunnerTests`/`FinalizeWorkflowRunnerTests`.

### Add `IGitHubService.UpdatePullRequestDescriptionAsync`
```csharp
Task UpdatePullRequestDescriptionAsync(int prNumber, string body, CancellationToken cancellationToken = default);
```
Implemented in `SpecRunner.GitHub.GitHubService` as a REST
`PATCH /repos/{owner}/{repo}/pulls/{prNumber}` with `{ "body": body }`,
following the same `SendAsync`/`GitHubApiException` failure-reporting
convention as every other real `IGitHubService` member. REST is sufficient
here (unlike `MarkPrReadyForReviewAsync`, which needs GraphQL because REST
has no draft→ready endpoint) since PATCHing a PR's body is a plain REST
capability.

Test doubles (`RecordingGitHubService`, `FakeGitHubService`) get a new
tracked list (e.g. `UpdatedPullRequestDescriptions`) mirroring the existing
`MarkedReadyForReview` pattern.

### Per-workflow call sites
- **propose** (`ProposeWorkflowRunner`): after the CLI agent session reaches
  `Completed`, call `ReadCurrentAsync(specName)` before
  `CreateDraftPullRequestAsync`, and pass its result (or an empty string if
  `null`) as the PR body instead of `comment.IssueBody`. Commit/push order
  is unchanged.
- **implement** (`ImplementWorkflowRunner`): after the existing
  commit+push, call `ReadCurrentAsync(specName)` and
  `UpdatePullRequestDescriptionAsync(prNumber, content)` before the existing
  `WritePrCommentAsync` success report. If the file is missing, skip the
  update (log a warning) rather than blanking the PR body.
- **finalize** (`FinalizeWorkflowRunner`): after the existing commit+push,
  call `ReadArchivedAsync(specName)`, build
  `$"{content}\n\nCloses #{issueNumber}"`, and call
  `UpdatePullRequestDescriptionAsync(prNumber, finalBody)` **before**
  `MarkPrReadyForReviewAsync`, so the description is already final at the
  moment reviewers are notified the PR is ready. If the archived file is
  missing, still append `Closes #{issue-number}` to whatever body currently
  exists (skip only the task-content portion), so the issue-closing link is
  never silently dropped.

## Risks / Trade-offs

- **Archive folder resolution is a glob, not an exact path** → mitigated by
  matching the literal `-{specName}` suffix (spec names already embed the
  issue number, so collisions are effectively impossible) and by picking
  the most recently modified match if more than one somehow exists.
- **Full PR body replacement discards the original issue text** after the
  first `propose` run → this is the explicit intent of the change (tasks
  list becomes the source of truth for the description); acceptable since
  the issue itself remains linked via `Closes #N` and is still one click
  away.
- **Missing `tasks.md`** (e.g. an unusual manual edit removed it) → each
  call site degrades gracefully (empty body at propose time, skipped update
  at implement time, `Closes #N`-only body at finalize time) instead of
  throwing and aborting the whole workflow run.

## Migration Plan

Purely additive behavior change in existing runners plus one new
`IGitHubService` member; no data migration and no state-store schema
change. Roll out is just deploying the updated `SpecRunner.Console` binary.
Rollback is redeploying the previous binary — no persisted state depends on
the new behavior.

## Open Questions

- Should `update-workflow` also refresh the PR description after each
  `/update` push, for consistency with `implement`? Left out of scope per
  the proposal; worth a follow-up change if reviewers want it.
