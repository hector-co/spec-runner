## 1. Core abstractions and models

- [x] 1.1 Add `EligibleFinalizeComment(int PrNumber, string PrHeadBranch, long CommentId, string Instructions)` to `SpecRunner.Core/Models`, matching `EligibleUpdateComment`'s shape.
- [x] 1.2 Add `IFinalizeWorkflowRunner` to `SpecRunner.Core/Abstractions` with a single `RunOnceAsync(CancellationToken)` operation, matching `IUpdateWorkflowRunner`.

## 2. GitHub GraphQL: real `MarkPrReadyForReviewAsync`

- [x] 2.1 In `SpecRunner.GitHub/GitHubService.cs`, replace the `MarkPrReadyForReviewAsync` `NotImplementedException` with a real implementation: resolve the PR's GraphQL `node_id` via `GET /repos/{owner}/{repo}/pulls/{prNumber}`, then POST the `markPullRequestReadyForReview` mutation (with that node id) to `https://api.github.com/graphql`.
- [x] 2.2 Ensure GraphQL failures (non-2xx response or a top-level `errors` array in the GraphQL response body) surface as the same typed exception this service already uses for REST failures, not an unstructured `HttpRequestException`.
- [x] 2.3 Add/extend `GitHubServiceTests.cs` covering: successful node-id resolution + mutation call, and a GraphQL error payload being reported as a typed failure.

## 3. `FinalizeWorkflowRunner`

- [x] 3.1 Add `SpecRunner.Console/FinalizeWorkflowRunner.cs` implementing `IFinalizeWorkflowRunner`, modeled directly on `UpdateWorkflowRunner.cs`:
  - `/finalize` trigger matching (exact match or followed by whitespace; mid-token matches like `/finalized` are not eligible).
  - Skip comments already bearing an `eyes`, `+1`, or `confused` reaction from the bot.
  - React `eyes` before any other action.
  - Look up the PR via `IStateStore.FindByPrNumberAsync`; if untracked, reply explaining no associated spec/change was found, react `confused`, and stop (no git/CLI-agent/state-store work).
  - Refresh the branch: `FetchAsync` → `SwitchBranchAsync` → `ResetHardAsync("origin/{branch}")`.
  - Start a CLI agent session with the archive prompt (see design.md's exact template), wrapped in escaped double quotes; await a terminal state.
  - On `Completed`: commit with message `"finalizing specs for #{issue-number}"`, push, then call `MarkPrReadyForReviewAsync`.
  - On success: react `+1`, post a confirmation reply, upsert the comment as `done` in the state store.
  - On error or `TaskTimeout` (stopping any in-flight session on timeout): react `confused`, post a human-readable failure reply, upsert the comment as `error` (when the PR is tracked), and continue to the next eligible comment.
  - Process eligible comments sequentially within a scan pass.
- [x] 3.2 Register `IFinalizeWorkflowRunner` → `FinalizeWorkflowRunner` as a singleton in `Program.cs`, alongside the other three workflow runners.
- [x] 3.3 Add a fourth scan pass for `IFinalizeWorkflowRunner` to `PollingLoop.RunAsync` (own try/catch per pass, matching the existing three), and update its call site in `Program.cs`.

## 4. Tests

- [x] 4.1 Add `SpecRunner.Tests/FinalizeWorkflowRunnerTests.cs` mirroring `UpdateWorkflowRunnerTests.cs`'s cases: trigger eligibility (exact/whitespace/mid-token), bot-reaction skipping, eyes-first ordering, untracked-PR handling, branch refresh sequence, exact CLI-agent prompt text, commit message and push, `MarkPrReadyForReviewAsync` being called after push and before success reporting, `+1`/reply/state-store on success, `confused`/reply/state-store on error, timeout handling (including session `StopAsync`), and sequential processing of multiple eligible comments.
- [x] 4.2 Update `Fakes/FakeGitHubService.cs` / `Fakes/RecordingGitHubService.cs` if the new tests need call tracking for `MarkPrReadyForReviewAsync` beyond the current no-op stub.

## 5. Verification

- [x] 5.1 Run `dotnet test` for `SpecRunner.Tests` and confirm all new and existing tests pass.
- [x] 5.2 Run `openspec validate implement-finalize-workflow --strict` and fix any reported issues.
