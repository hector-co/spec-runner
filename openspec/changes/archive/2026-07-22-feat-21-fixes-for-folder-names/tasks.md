## 1. Spec name format

- [x] 1.1 Update `SpecNameResolver.Resolve` (`SpecRunner.Core/SpecNameResolver.cs`) to return `feat-{issueNumber}-{collapsed}` instead of `{issueNumber}-{collapsed}`.
- [x] 1.2 Update `SpecNameResolverTests` (`SpecRunner.Tests/SpecNameResolverTests.cs`) expected values to the `feat-{issue-number}-...` format.

## 2. Spec folder resolution abstraction

- [x] 2.1 Add `ISpecFolderResolver` to `SpecRunner.Core/Abstractions/` with `Task<string> ResolveAsync(string expectedSpecName, int issueNumber, CancellationToken cancellationToken = default)`.
- [x] 2.2 Implement `SpecFolderResolver` in `SpecRunner.Console/` (constructed from `SpecRunnerOptions.LocalRepositoryPath`, same pattern as `TasksFileReader`):
  - Returns `expectedSpecName` if `openspec/changes/{expectedSpecName}` exists.
  - Otherwise returns the first directory under `openspec/changes/` whose name starts with `feat-{issueNumber}-`.
  - Otherwise throws (e.g. `InvalidOperationException`) with a message identifying the issue number and expected spec name.
- [x] 2.3 Register `ISpecFolderResolver`/`SpecFolderResolver` as a singleton in `Program.cs`, alongside the other `SpecRunner.Console`-implemented singletons.
- [x] 2.4 Add unit tests for `SpecFolderResolver` covering: exact match, fallback prefix match, and the no-match error case.

## 3. Wire resolution into the propose workflow

- [x] 3.1 Inject `ISpecFolderResolver` into `ProposeWorkflowRunner`.
- [x] 3.2 In `ProcessCommentAsync`, right after the `session.State != CliAgentSessionState.Completed` check, call `ISpecFolderResolver.ResolveAsync` with the expected spec name (from `ISpecNameResolver`) and the issue number, and use its return value as `specName` for everything after (reading `tasks.md`, `CreateDraftPullRequestAsync`, and `ReportSuccessAsync`'s state-store upsert).
- [x] 3.3 Confirm that when `ResolveAsync` throws, the existing `catch (Exception)` block in `ProcessCommentAsync` handles it exactly like any other processing failure (no commit/push/PR — those steps come after resolution) and reports it via `ReportErrorAsync`.

## 4. Verification

- [x] 4.1 Run `dotnet test` for `SpecRunner.sln` and confirm all tests pass, including the updated `SpecNameResolverTests` and the new `SpecFolderResolver` tests.
- [x] 4.2 Re-read `openspec/specs/propose-workflow/spec.md` and `openspec/specs/state-store-schema/spec.md` deltas against the final implementation to confirm no scenario was missed.
