## 1. Core abstractions

- [ ] 1.1 Add `SpecRunner.Core.Models.RunKey` (a struct/union distinguishing
      `Issue(int)` from `Pr(int)`, with value equality) for use as the
      active-run registry's dictionary key.
- [ ] 1.2 Add `SpecRunner.Core.Models.ActiveRun` exposing the
      externally-triggered `CancellationTokenSource` for a registered run, a
      nullable `ICliAgentSession` (settable after the session starts), and a
      `Task` that completes when the registered comment's processing returns.
- [ ] 1.3 Add `SpecRunner.Core.Abstractions.IActiveRunRegistry` with
      `Register(RunKey, ActiveRun)`, `Deregister(RunKey)`,
      `TryGet(RunKey, out ActiveRun)`, and `IsAnyActive()`.
- [ ] 1.4 Add `SpecRunner.Core.Abstractions.ICancelWorkflowRunner` with a
      single `RunOnceAsync(CancellationToken)` method, mirroring the other
      four workflow-runner interfaces.
- [ ] 1.5 Add `Canceled` to `SpecRunner.Core.Models.CommentStatus`.

## 2. Active-run registry implementation

- [ ] 2.1 Implement `SpecRunner.Console.ActiveRunRegistry` (or a suitable
      home in `SpecRunner.Core`) backed by a `ConcurrentDictionary<RunKey,
      ActiveRun>`, implementing `IActiveRunRegistry`.
- [ ] 2.2 Register `IActiveRunRegistry` as a singleton in DI setup
      (`Program.cs`).
- [ ] 2.3 Add unit tests for `ActiveRunRegistry`: register/lookup round-trip,
      deregister removes the entry, `IsAnyActive()` reflects whether any key
      is registered, and concurrent register/deregister from multiple
      threads does not corrupt state.

## 3. Wire the active-run registry into the four existing workflow runners

- [ ] 3.1 In `ProposeWorkflowRunner.ProcessCommentAsync`, introduce a
      separate `cancelRequestCts` linked into the existing `timeoutCts`
      construction; register an `ActiveRun` for `RunKey.Issue(issueNumber)`
      right after creating it, update the registered `ActiveRun`'s session
      once `session` is assigned, and deregister in the existing `finally`
      block.
- [ ] 3.2 Split `ProposeWorkflowRunner`'s
      `catch (OperationCanceledException) when (timeoutCts...)` into two
      arms: one for `cancelRequestCts.IsCancellationRequested` (stop the
      session via `StopAsync`, then return without any reaction/reply/state
      upsert) and the existing one for a genuine `TaskTimeout` expiry
      (unchanged).
- [ ] 3.3 Repeat 3.1-3.2 for `ImplementWorkflowRunner`, keyed by
      `RunKey.Pr(prNumber)`.
- [ ] 3.4 Repeat 3.1-3.2 for `UpdateWorkflowRunner`, keyed by
      `RunKey.Pr(prNumber)`, for both `PrIssueComment` and `PrReviewComment`
      code paths.
- [ ] 3.5 Repeat 3.1-3.2 for `FinalizeWorkflowRunner`, keyed by
      `RunKey.Pr(prNumber)`.
- [ ] 3.6 Update existing tests for all four runners as needed so the
      already-passing timeout/error scenarios keep passing unmodified with
      the added `cancelRequestCts` in place.
- [ ] 3.7 Add a new test per runner covering the added
      cancel-vs-timeout branch: an externally cancelled `cancelRequestCts`
      stops the session without posting any reaction, reply, or state-store
      upsert.

## 4. `CancelWorkflowRunner`

- [ ] 4.1 Implement `SpecRunner.Console.CancelWorkflowRunner : ICancelWorkflowRunner`,
      scanning open issues' comments and open PRs' conversation comments
      (via `IGitHubService`) for a trimmed body of exactly `/cancel` or
      `/cancel` followed by whitespace, excluding PR review comments.
- [ ] 4.2 Apply the existing already-handled-reaction skip (`eyes`/`+1`/
      `confused` from the bot login) and `CommentAuthorization.IsAuthorized`
      filtering, logging a warning and skipping unauthorized comments exactly
      like the other four runners.
- [ ] 4.3 For each eligible/authorized `/cancel` comment: add an `eyes`
      reaction first, resolve the target `RunKey` from the comment's issue
      or PR number, and look it up via `IActiveRunRegistry.TryGet`.
- [ ] 4.4 If a matching `ActiveRun` is found: cancel its
      `CancellationTokenSource`, call `StopAsync` on its `ICliAgentSession`
      if assigned, then `await` its completion `Task` bounded by a fixed
      grace period (e.g. 30s), logging a warning if the grace period elapses
      first.
- [ ] 4.5 After the stop step, call `IGitService.ResetHardAsync("HEAD")` only
      if `IActiveRunRegistry.IsAnyActive()` is `false`; otherwise skip the
      reset and prepare a "nothing running for this issue/PR" reply.
- [ ] 4.6 If a tracked record exists for the targeted issue/PR (via
      `IStateStore.FindByIssueNumberAsync`/`FindByPrNumberAsync`), upsert the
      status of the comment that originally triggered the now-stopped run
      (if resolvable) to `CommentStatus.Canceled`.
- [ ] 4.7 React `+1` to the `/cancel` comment and post a reply confirming
      either the cancellation (changes discarded) or that nothing was
      running for that issue/PR.
- [ ] 4.8 Wrap per-comment processing in a try/catch that reacts `confused`
      and posts a human-readable failure reply on unhandled exceptions, then
      continues to the next eligible comment, matching the other runners'
      error-handling convention.
- [ ] 4.9 Ensure `RunOnceAsync` processes its eligible comments sequentially,
      one at a time.

## 5. Wiring and concurrency

- [ ] 5.1 Register `ICancelWorkflowRunner`/`CancelWorkflowRunner` in DI
      setup (`Program.cs`).
- [ ] 5.2 In `Program.cs`, start a second loop that calls
      `cancelWorkflowRunner.RunOnceAsync` on `options.PollingInterval`,
      running concurrently with (not inside) the existing
      `PollingLoop.RunAsync` call, both awaited together and both observing
      the shared shutdown token.
- [ ] 5.3 Confirm (by code review or a targeted integration-style test) that
      the two loops share the same `IGitService`/`IGitHubService`/
      `IStateStore`/`IActiveRunRegistry` singleton instances, so the registry
      and clone-safety checks are meaningful across both loops.

## 6. Tests for `CancelWorkflowRunner`

- [ ] 6.1 Eligible-trigger matching: exact `/cancel`, `/cancel` plus
      whitespace/instructions, mid-sentence `/cancel`/`/cancelled` not
      eligible, and PR review comments never eligible.
- [ ] 6.2 Already-handled skip and authorization skip (mirroring the other
      four runners' existing test patterns).
- [ ] 6.3 Stopping a matching active run: cancels its token source, calls
      `StopAsync` on its session, awaits its completion task, then performs
      the reset.
- [ ] 6.4 No matching active run but registry otherwise empty: reset still
      runs.
- [ ] 6.5 No matching active run but a different run is active elsewhere:
      reset is skipped and the reply says nothing was running for the
      targeted issue/PR.
- [ ] 6.6 Successful cancellation upserts `CommentStatus.Canceled` for the
      original triggering comment's tracked record.
- [ ] 6.7 Unhandled exception during processing reacts `confused`, posts a
      reply, and processing continues to the next eligible comment.
- [ ] 6.8 Two eligible `/cancel` comments in one scan pass are processed
      sequentially.

## 7. Documentation and spec sync

- [ ] 7.1 Run the project's existing test suite (`dotnet test`) to confirm
      no regressions in the four modified workflow runners.
- [ ] 7.2 Update `README.md` (or equivalent user-facing docs) to document the
      `/cancel` comment trigger alongside the existing `/propose`,
      `/implement`, `/update`, and `/finalize` triggers, if such
      documentation already lists them.
