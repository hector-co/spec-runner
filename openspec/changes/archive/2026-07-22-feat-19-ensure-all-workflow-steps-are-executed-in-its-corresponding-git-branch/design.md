## Context

All four workflow runners (`ProposeWorkflowRunner`, `ImplementWorkflowRunner`, `UpdateWorkflowRunner`, `FinalizeWorkflowRunner`, all in `SpecRunner.Console`) share one local clone at `SpecRunnerOptions.LocalRepositoryPath` and drive it through `IGitService` (`SpecRunner.Core/Abstractions/IGitService.cs`, real implementation `SpecRunner.Git/GitService.cs`).

Today:
- `GitService.PullAsync()` hardcodes `fetch origin {BaseBranchName}` → `checkout {BaseBranchName}` → `merge --ff-only origin/{BaseBranchName}`. It cannot target any other branch, and the checkout step is buried inside it rather than being an explicit, separately callable step.
- `ProposeWorkflowRunner.ProcessCommentAsync` calls `PullAsync()` → `ResetHardAsync(BaseBranchName)` → `CreateBranchAsync($"feature/{issueNumber}")` → `SwitchBranchAsync(...)`. `CreateBranchAsync` shells out to `git branch -f`, which force-overwrites any existing branch of that name instead of failing.
- `ImplementWorkflowRunner`, `UpdateWorkflowRunner`, and `FinalizeWorkflowRunner` all call `FetchAsync(comment.PrHeadBranch)` → `SwitchBranchAsync(comment.PrHeadBranch)` → `ResetHardAsync($"origin/{comment.PrHeadBranch}")`, where `comment.PrHeadBranch` comes from `GitHubPullRequest.HeadBranch`, re-read from the GitHub API on every poll.
- None of the four runners discard local changes before switching branches — `ResetHardAsync` is only ever called with a target *other than* the branch currently checked out.
- `TrackedIssue` (`SpecRunner.Core/Models/TrackedIssue.cs`) records `IssueNumber`, `SpecName`, `PrNumber`, timestamps, and comments — no branch name. `SqliteStateStore.UpsertTrackedIssueAsync`'s `ON CONFLICT` clause only updates `PrNumber` and `UpdatedAtUtc`; `SpecName` is fixed at first insert.

This matters because `git checkout` refuses to switch branches over conflicting uncommitted changes, and a `git reset --hard {other-branch}` while sitting on the wrong branch moves the *current* branch's tip rather than the intended one. A run interrupted mid-CLI-agent-session (timeout, crash, host restart) can leave exactly this kind of dirty, wrong-branch state for the next poll to trip over.

## Goals / Non-Goals

**Goals:**
- Every workflow step starts by discarding any uncommitted/untracked state on whatever branch is currently checked out, before it ever tries to switch branches.
- `propose` always ends up on `BaseBranchName`, pulled to `origin`'s tip, before it computes the new issue branch name.
- `propose` never silently overwrites an existing branch: it detects a name collision (local or `origin`) and appends a numeric suffix until the name is free.
- The branch name for an in-progress issue/PR is durably recorded in the state store as soon as it's known, and `implement`/`update`/`finalize` use that recorded value rather than re-deriving it from GitHub on every poll.

**Non-Goals:**
- No change to how eligible comments are discovered or reacted to (GitHub scanning/reaction logic is untouched).
- No general-purpose "resume an interrupted run" feature — this change only makes sure the *next* poll starts from a correct, clean branch state; it does not add checkpointing of CLI-agent progress itself.
- No change to `CommitAsync`/`PushAsync` semantics.
- `GitHubPullRequest.HeadBranch` / `EligiblePr*Comment.PrHeadBranch` are not removed from the model — they remain useful for messages and logging — only the *git call sites* switch to the stored `BranchName`.

## Decisions

### Add `IGitService.BranchExistsAsync`, checked against both local and `origin` refs
```csharp
Task<bool> BranchExistsAsync(string branchName, CancellationToken cancellationToken = default);
```
Real implementation runs `git show-ref --verify --quiet refs/heads/{branchName}` (local) and, if that misses, `git ls-remote --exit-code --heads origin {branchName}` (remote), returning `true` if either finds a match. Checking `origin` too, not just the local clone, matters because a previous run could have created and pushed `feature/45` and then crashed before recording it anywhere — a purely local check would miss that and recreate a colliding branch.

Alternative considered: only check local refs. Rejected — the whole point of this change is resilience to a run that got interrupted after partially completing git/GitHub work, and a pushed-but-unrecorded branch is exactly that scenario.

### Stop force-creating branches; make `CreateBranchAsync` fail on collision
`GitService.CreateBranchAsync` changes from `git branch -f {name}` to `git branch {name}`, which exits non-zero (surfaced as `GitCommandException`, per the existing `git-operations` failure-reporting requirement) if the name is already taken locally. Combined with the pre-check via `BranchExistsAsync` and the suffixing logic below, this is now a defense-in-depth assertion rather than the primary collision-avoidance mechanism, but it removes a footgun: any future caller that skips the pre-check gets a loud failure instead of silently rewriting history on an existing branch.

### Uniform "clean, then switch, then sync" sequencing across all four runners
Every runner's `ProcessCommentAsync` now starts its git sequence with `ResetHardAsync("HEAD")` — a discard-only reset that leaves the current branch pointer untouched but wipes uncommitted/untracked changes — before any `SwitchBranchAsync`/`FetchAsync` call. This directly implements the requested "`git reset --hard`, then move to the branch, then sync from `origin`" order:

- **propose**: `ResetHardAsync("HEAD")` → `SwitchBranchAsync(BaseBranchName)` → `PullAsync()`. The explicit `SwitchBranchAsync` call is new — today the only thing that ever checks out the base branch is `PullAsync`'s internal `checkout`. Making it an explicit, separately-ordered step means a test can assert the sequence directly (`RecordingGitService.Calls`), and means the checkout no longer depends on `PullAsync`'s internals staying as they are. The previously separate `ResetHardAsync(BaseBranchName)` call *after* `PullAsync` is dropped: the leading `ResetHardAsync("HEAD")` already guarantees a clean tree going in, and `PullAsync`'s `merge --ff-only` already fails loudly (rather than silently discarding anything) if the base branch can't fast-forward, so a second hard reset added nothing but an extra git invocation.
- **implement / update / finalize**: `ResetHardAsync("HEAD")` → `FetchAsync(branchName)` → `SwitchBranchAsync(branchName)` → `ResetHardAsync($"origin/{branchName}")`. The fetch+switch+reset-to-`origin/{branch}` sequence already existed and is left as-is (it's the "pull" for a branch `PullAsync` can't target); only the leading discard-only reset is new.

Alternative considered: give `IGitService` a branch-aware `PullAsync(string branchName)` overload replacing the fetch/switch/reset trio. Rejected for this change — it would touch `PullAsync`'s hardcoded contract and every existing caller/test for a cosmetic simplification; the fetch/switch/reset trio already behaves correctly and is well covered by existing tests, so it's left alone to keep this change focused on branch *safety*, not on collapsing that trio.

### Persist `BranchName` on `TrackedIssue`, and let `UpsertTrackedIssueAsync` update it (and `SpecName`) on conflict
```csharp
public record TrackedIssue(int IssueNumber, string SpecName)
{
    public string BranchName { get; init; } = string.Empty;
    // PrNumber, CreatedAtUtc, UpdatedAtUtc, Comments unchanged
}
```
`propose` upserts a `TrackedIssue` with the chosen branch name and the *expected* spec name immediately after `CreateBranchAsync`/`SwitchBranchAsync` succeed — before the CLI agent runs — so the branch survives a crash even if the run never reaches its current single upsert-at-success call. Once the CLI agent completes and the *actual* on-disk spec name is resolved (`ISpecFolderResolver`), the existing success path upserts again with the corrected `SpecName` and the `PrNumber`. For that second upsert to actually take effect, `SqliteStateStore`'s `ON CONFLICT` clause is widened from `SET PrNumber = excluded.PrNumber, UpdatedAtUtc = excluded.UpdatedAtUtc` to also `SET SpecName = excluded.SpecName, BranchName = excluded.BranchName` — i.e. upsert now updates every mutable field, not just `PrNumber`. `implement`, `update`, and `finalize` all look up `TrackedIssue` first thing (`FindByPrNumberAsync`) already; they now read `trackedIssue.BranchName` for their `Fetch`/`Switch`/`Reset`/`Push` calls instead of `comment.PrHeadBranch`.

Schema migration: `SqliteStateStore.EnsureSchemaAsync` only ever runs `CREATE TABLE IF NOT EXISTS`, so a `BranchName TEXT NOT NULL DEFAULT ''` column added to the `TrackedIssues` table definition takes effect for brand-new database files, but an existing `state.db` from before this change already has the table without the column. `EnsureSchemaAsync` additionally runs an idempotent `ALTER TABLE TrackedIssues ADD COLUMN BranchName TEXT NOT NULL DEFAULT ''`, guarded by first checking `pragma_table_info(TrackedIssues)` for a column named `BranchName` (SQLite's `ALTER TABLE ADD COLUMN` has no `IF NOT EXISTS` form and errors if the column already exists).

Alternative considered: keep deriving the branch name live from `pr.HeadBranch` and skip persistence entirely, relying on GitHub as the single source of truth. Rejected — this is the literal ask (a local, durable record to "return to the required branch"), and it also means `implement`/`update`/`finalize` no longer depend on the exact string GitHub reports matching what `propose` actually created (relevant once suffixing can change the name from the naive `feature/{issue-number}`).

### Unique branch name generation in `propose`
After landing cleanly on `BaseBranchName`, `ProposeWorkflowRunner` computes `feature/{issueNumber}`, then loops appending `-2`, `-3`, ... while `IGitService.BranchExistsAsync` reports a collision, before calling `CreateBranchAsync`/`SwitchBranchAsync` with the resolved name and persisting it.

## Risks / Trade-offs

- **Stored `BranchName` can drift from GitHub's live head branch** if a PR's branch is ever renamed on GitHub after creation → accepted risk; this is not a supported/expected action in this workflow, and `git-operations`'s existing failure reporting means a stale name simply surfaces as a fetch/switch failure on the next poll rather than silently operating on the wrong branch.
- **Two `git reset --hard` calls per feature-branch run** (`HEAD` then `origin/{branch}`) is slightly more process overhead than today's single reset → acceptable; each is a fast local operation, and the safety property (never switching branches over a dirty tree) is the point of the change.
- **Widening `UpsertTrackedIssueAsync`'s conflict clause to update `SpecName`** changes existing semantics (today a second upsert's `SpecName` is silently ignored) → reviewed as safe: the only caller that ever upserts a `SpecName` different from the first one is `propose`'s own corrected-name flow this change introduces; no other runner writes `SpecName`.
- **SQLite `ALTER TABLE ADD COLUMN` on existing databases** is a one-time, one-directional migration with no down-migration → acceptable for this project's simple `CREATE TABLE IF NOT EXISTS`-based schema management; the default value (`''`) keeps old rows (created before this change, with no recorded branch) loadable without error.

## Migration Plan

Additive schema change (new nullable-by-default column, applied via a guarded `ALTER TABLE` on existing `state.db` files) plus behavior changes in existing runners and `GitService`; no separate data backfill is needed since old `TrackedIssue` rows simply read back with `BranchName = ""` until they're next upserted. Rollout is deploying the updated `SpecRunner.Console` binary. Rollback is redeploying the previous binary against the same `state.db` — the added column is additive and ignored by the old code.

## Open Questions

- Should a stale/missing `BranchName` (e.g. a row written before this change, still mid-flight when it ships) fall back to `pr.HeadBranch` for one transitional run rather than failing? Left out of scope — in-flight PRs at deploy time are expected to be rare enough that a manual retry (which will then persist the correct value) is acceptable.
