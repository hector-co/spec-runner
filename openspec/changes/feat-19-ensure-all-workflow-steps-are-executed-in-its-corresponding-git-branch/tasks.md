## 1. Git service: branch existence check and non-destructive create

- [x] 1.1 Add `Task<bool> BranchExistsAsync(string branchName, CancellationToken cancellationToken = default)` to `IGitService` (`SpecRunner.Core/Abstractions/IGitService.cs`).
- [x] 1.2 Implement it in `SpecRunner.Git/GitService.cs`: run `git show-ref --verify --quiet refs/heads/{branchName}`; if that reports no match, run `git ls-remote --exit-code --heads origin {branchName}`; return `true` if either succeeds, `false` if both report no match (exit code 2/no output), without throwing `GitCommandException` for the "not found" case specifically.
- [x] 1.3 Change `GitService.CreateBranchAsync` from `git branch -f {name}` to `git branch {name}`, so it fails via the existing `GitCommandException` path if the name already exists locally instead of overwriting it.
- [x] 1.4 Add `BranchExistsAsync` to `SpecRunner.Tests/Fakes/FakeGitService.cs` (return a configurable bool, default `false`) and `SpecRunner.Tests/Fakes/RecordingGitService.cs` (record the call and return a configurable per-name result).
- [x] 1.5 Add `GitServiceTests` coverage for `BranchExistsAsync` (local hit, remote-only hit, no match) and for `CreateBranchAsync` now failing instead of overwriting an existing local branch.

## 2. State store: persist the branch name

- [x] 2.1 Add `BranchName` to `TrackedIssue` (`SpecRunner.Core/Models/TrackedIssue.cs`) as an `init`-only `string` property defaulting to `string.Empty`.
- [x] 2.2 In `SqliteStateStore.EnsureSchemaAsync` (`SpecRunner.State/SqliteStateStore.cs`), add `BranchName TEXT NOT NULL DEFAULT ''` to the `TrackedIssues` table's `CREATE TABLE IF NOT EXISTS` definition, and add a guarded `ALTER TABLE TrackedIssues ADD COLUMN BranchName TEXT NOT NULL DEFAULT ''` that only runs when `pragma_table_info(TrackedIssues)` shows no column named `BranchName`, so pre-existing database files are upgraded in place.
- [x] 2.3 Update `UpsertTrackedIssueAsync`'s SQL: include `BranchName` in the `INSERT` column list/values, and widen the `ON CONFLICT(IssueNumber) DO UPDATE SET` clause to also set `SpecName = excluded.SpecName` and `BranchName = excluded.BranchName` (in addition to the existing `PrNumber`/`UpdatedAtUtc`).
- [x] 2.4 Update `ReadTrackedIssue` to read the `BranchName` column and set it on the returned `TrackedIssue`.
- [x] 2.5 Update `SqliteStateStoreTests.cs`: cover round-tripping `BranchName`, a second upsert changing both `SpecName` and `BranchName`, and loading a database file seeded without the `BranchName` column (simulating a pre-migration file) to confirm the guarded `ALTER TABLE` runs correctly.

## 3. Propose workflow: clean base branch, unique branch name, early persistence

- [x] 3.1 In `ProposeWorkflowRunner.ProcessCommentAsync` (`SpecRunner.Console/ProposeWorkflowRunner.cs`), replace the current `_git.PullAsync()` → `_git.ResetHardAsync(_options.BaseBranchName)` pair with: `_git.ResetHardAsync("HEAD", ...)`, then `_git.SwitchBranchAsync(_options.BaseBranchName, ...)`, then `_git.PullAsync(...)`.
- [x] 3.2 Replace the fixed `var branchName = $"feature/{comment.IssueNumber}";` with a loop that starts from that candidate, and while `await _git.BranchExistsAsync(candidate, ...)` is `true`, appends `-2`, `-3`, ... (e.g. `$"feature/{comment.IssueNumber}-{suffix}"`) until an unused name is found.
- [x] 3.3 After `_git.CreateBranchAsync(branchName, ...)` and `_git.SwitchBranchAsync(branchName, ...)` succeed, and before rendering the `propose` prompt/starting the CLI agent session, upsert a `TrackedIssue` via `_stateStore.UpsertTrackedIssueAsync` with the issue number, the expected spec name (`_specNameResolver.Resolve(...)`, already computed at this point), and the new `BranchName` set to `branchName`.
- [x] 3.4 Confirm the existing `ReportSuccessAsync` upsert (issue number, resolved actual spec name, PR number) still runs unchanged — it now relies on the widened `ON CONFLICT` clause from task 2.3 to correct `SpecName` in place rather than being silently ignored.
- [x] 3.5 Update `ProposeWorkflowRunnerTests.cs`: assert the git call order via `RecordingGitService.Calls` is `ResetHard:HEAD`, `SwitchBranch:{base}`, `Pull`, ... ; add a case where the fake state store or `RecordingGitService`/a stubbed `BranchExistsAsync` reports `"feature/45"` as taken so the run creates/switches to `"feature/45-2"` instead; assert the state store holds `BranchName` immediately after branch creation (before the CLI agent session would run), and that the final record's `SpecName` reflects the resolved actual name while `BranchName` is unchanged.

## 4. Implement workflow: clean-then-switch and stored branch name

- [x] 4.1 In `ImplementWorkflowRunner.ProcessCommentAsync` (`SpecRunner.Console/ImplementWorkflowRunner.cs`), add `await _git.ResetHardAsync("HEAD", ...)` as the first git call, before the existing `_git.FetchAsync(...)` call.
- [x] 4.2 Change the `Fetch`/`Switch`/`ResetHard`/`Push` calls to use `trackedIssue.BranchName` instead of `comment.PrHeadBranch`.
- [x] 4.3 Update `ImplementWorkflowRunnerTests.cs`: assert the call order now starts with `ResetHard:HEAD`; assert `Fetch`/`SwitchBranch`/`ResetHard`/`Push` target the tracked record's `BranchName` even when it differs from `comment.PrHeadBranch` (add a test case where a fake tracked issue's `BranchName` differs from the PR's reported head branch, and assert the tracked value wins).

## 5. Update workflow: clean-then-switch and stored branch name

- [x] 5.1 In `UpdateWorkflowRunner.ProcessCommentAsync` (`SpecRunner.Console/UpdateWorkflowRunner.cs`), add `await _git.ResetHardAsync("HEAD", ...)` as the first git call, before the existing `_git.FetchAsync(...)` call.
- [x] 5.2 Change the `Fetch`/`Switch`/`ResetHard`/`Push` calls to use `trackedIssue.BranchName` instead of `comment.PrHeadBranch`.
- [x] 5.3 Update `UpdateWorkflowRunnerTests.cs` with the same call-order and stored-branch-name assertions as task 4.3.

## 6. Finalize workflow: clean-then-switch and stored branch name

- [x] 6.1 In `FinalizeWorkflowRunner.ProcessCommentAsync` (`SpecRunner.Console/FinalizeWorkflowRunner.cs`), add `await _git.ResetHardAsync("HEAD", ...)` as the first git call, before the existing `_git.FetchAsync(...)` call.
- [x] 6.2 Change the `Fetch`/`Switch`/`ResetHard`/`Push` calls to use `trackedIssue.BranchName` instead of `comment.PrHeadBranch`.
- [x] 6.3 Update `FinalizeWorkflowRunnerTests.cs` with the same call-order and stored-branch-name assertions as task 4.3.

## 7. Verification and spec sync

- [x] 7.1 Run `dotnet build` and `dotnet test` from `SpecRunner/` and confirm everything is green, including the new `GitServiceTests`, `SqliteStateStoreTests`, and the four `*WorkflowRunnerTests` cases added above.
- [x] 7.2 Run `openspec validate feat-19-ensure-all-workflow-steps-are-executed-in-its-corresponding-git-branch --type change --strict` and confirm it passes.
- [x] 7.3 After implementation and tests are green, sync the delta specs (`git-operations`, `state-store-schema`, `propose-workflow`, `implement-workflow`, `update-workflow`, `finalize-workflow`) into `openspec/specs/` per the normal archive flow.
