## 1. Spec name format

- [ ] 1.1 Update `SpecNameResolver.Resolve` (`SpecRunner.Core/SpecNameResolver.cs`) to return `feat-{issueNumber}-{collapsed}` instead of `{issueNumber}-{collapsed}`.
- [ ] 1.2 Update `SpecNameResolverTests` (`SpecRunner.Tests/SpecNameResolverTests.cs`) expected values to the `feat-{issue-number}-...` format.

## 2. Spec folder resolution abstraction

- [ ] 2.1 Add `ISpecFolderResolver` to `SpecRunner.Core/Abstractions/` with `Task<string> ResolveAsync(string expectedSpecName, int issueNumber, CancellationToken cancellationToken = default)`.
- [ ] 2.2 Implement `SpecFolderResolver` in `SpecRunner.Console/` (constructed from `SpecRunnerOptions.LocalRepositoryPath`, same pattern as `TasksFileReader`):
  - Returns `expectedSpecName` if `openspec/changes/{expectedSpecName}` exists.
  - Otherwise returns the first directory under `openspec/changes/` whose name starts with `feat-{issueNumber}-`.
  - Otherwise throws (e.g. `InvalidOperationException`) with a message identifying the issue number and expected spec name.
- [ ] 2.3 Register `ISpecFolderResolver`/`SpecFolderResolver` as a singleton in `Program.cs`, alongside the other `SpecRunner.Console`-implemented singletons.
- [ ] 2.4 Add unit tests for `SpecFolderResolver` covering: exact match, fallback prefix match, and the no-match error case.

## 3. Wire resolution into the propose workflow

- [ ] 3.1 Inject `ISpecFolderResolver` into `ProposeWorkflowRunner`.
- [ ] 3.2 In `ProcessCommentAsync`, right after the `session.State != CliAgentSessionState.Completed` check, call `ISpecFolderResolver.ResolveAsync` with the expected spec name (from `ISpecNameResolver`) and the issue number, and use its return value as `specName` for everything after (reading `tasks.md`, `CreateDraftPullRequestAsync`, and `ReportSuccessAsync`'s state-store upsert).
- [ ] 3.3 Confirm that when `ResolveAsync` throws, the existing `catch (Exception)` block in `ProcessCommentAsync` handles it exactly like any other processing failure (no commit/push/PR — those steps come after resolution) and reports it via `ReportErrorAsync`.

## 4. Verification

- [ ] 4.1 Run `dotnet test` for `SpecRunner.sln` and confirm all tests pass, including the updated `SpecNameResolverTests` and the new `SpecFolderResolver` tests.
- [ ] 4.2 Re-read `openspec/specs/propose-workflow/spec.md` and `openspec/specs/state-store-schema/spec.md` deltas against the final implementation to confirm no scenario was missed.
