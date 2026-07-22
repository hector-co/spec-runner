## 1. Update-workflow core abstractions (`SpecRunner.Core`)

- [x] 1.1 Add `IUpdateWorkflowRunner` to `SpecRunner.Core/Abstractions`
      (`SpecRunner/src/SpecRunner.Core/Abstractions/IUpdateWorkflowRunner.cs`),
      mirroring `IImplementWorkflowRunner` with a single
      `Task RunOnceAsync(CancellationToken)` member.
- [x] 1.2 Add a supporting model for an eligible `/update` comment
      (`SpecRunner/src/SpecRunner.Core/Models/EligibleUpdateComment.cs`),
      mirroring `EligibleImplementComment` (PR number, PR head branch,
      comment id, and the comment body with the `/update` token stripped).

## 2. Update-workflow orchestration (`SpecRunner.Console`)

- [x] 2.1 Add `UpdateWorkflowRunner`
      (`SpecRunner/src/SpecRunner.Console/UpdateWorkflowRunner.cs`)
      implementing `IUpdateWorkflowRunner`, composing `IGitHubService`,
      `IGitService`, `IStateStore`, `ICliAgentSessionFactory`, and
      `IOptions<SpecRunnerOptions>` — structured the same way as
      `ImplementWorkflowRunner`.
- [x] 2.2 Implement the eligible-comment scan: list open PRs, read each
      PR's comments via `ReadPrCommentsAsync`, filter to comments whose
      trimmed body is exactly `/update` or starts with `/update` followed
      by whitespace, and exclude comments already carrying an
      `eyes`/`+1`/`confused` reaction from the authenticated bot login.
- [x] 2.3 Implement sequential per-comment processing: for each eligible
      comment, add the `eyes` reaction first, then look up
      `IStateStore.FindByPrNumberAsync` for that comment's PR.
- [x] 2.4 Implement the untracked-PR path: post a reply explaining no
      associated spec/change was found for this PR, add the `confused`
      reaction, then return without any git operation, CLI-agent session,
      or state-store write.
- [x] 2.5 Implement the tracked-PR path: `FetchAsync` the PR's head
      branch, `SwitchBranchAsync` to it, `ResetHardAsync` it to
      `origin/{branch}`.
- [x] 2.6 Build the CLI-agent prompt as `Update the OpenSpec change
      "{spec-name}" to reflect the following new requirement/information:
      \n{instructions}` (comment body with the leading `/update` token and
      its separating whitespace removed; `{spec-name}` from the tracked
      record), sent as a single value wrapped in escaped double quotes
      (`\"...\"`) — not an `/opsx-*` slash command — and start a CLI agent
      session with it; await a terminal session state.
- [x] 2.7 On `Completed`: `CommitAsync` with message `"updating specs for
      #{issue-number}"`, `PushAsync` the PR's branch, then add the `+1`
      reaction and post a reply confirming the push; upsert the state
      store (comment status `done` under the tracked issue number).
- [x] 2.8 Wrap the per-comment cycle (branch refresh through push) with
      `SpecRunnerOptions.TaskTimeout`; on timeout, call `StopAsync` on any
      in-flight CLI agent session.
- [x] 2.9 On any thrown exception or timeout for a tracked PR: add the
      `confused` reaction, post a short human-readable error summary,
      upsert the state store with comment status `error` under the
      tracked issue number, and continue to the next eligible comment
      rather than aborting the scan pass.
- [x] 2.10 Register `IUpdateWorkflowRunner` in `SpecRunner.Console`'s DI
      container (`Program.cs`), alongside `IProposeWorkflowRunner` and
      `IImplementWorkflowRunner`.

## 3. Poll loop and entry point (`SpecRunner.Console`)

- [x] 3.1 Extend `PollingLoop.RunAsync` to accept `IUpdateWorkflowRunner`
      in addition to `IProposeWorkflowRunner` and
      `IImplementWorkflowRunner`, calling `propose-workflow`'s
      `RunOnceAsync()`, then `implement-workflow`'s `RunOnceAsync()`, then
      `update-workflow`'s `RunOnceAsync()` in order each cycle, each
      wrapped in its own try/catch so one workflow's unhandled exception
      doesn't prevent the others from running that cycle.
- [x] 3.2 Update `Program.cs` to resolve `IUpdateWorkflowRunner` and pass
      it to the updated `PollingLoop.RunAsync` alongside
      `IProposeWorkflowRunner` and `IImplementWorkflowRunner`.

## 4. Tests

- [x] 4.1 Add unit tests
      (`SpecRunner/tests/SpecRunner.Tests/UpdateWorkflowRunnerTests.cs`)
      for `/update` comment-eligibility matching (exact match, trailing
      text, mid-sentence/`/updated` non-match) and instructions extraction
      (token + whitespace stripped).
- [x] 4.2 Add unit tests for the already-reacted skip logic
      (`eyes`/`+1`/`confused` from the bot vs. a human reaction vs. no
      reaction).
- [x] 4.3 Add unit tests for the untracked-PR path (explanatory reply,
      `confused` reaction, no git/CLI/state-store activity).
- [x] 4.4 Add unit tests for the success path (branch fetch/switch/reset,
      CLI agent invocation with the expected natural-language prompt —
      including the literal quoting of `{spec-name}` inside the
      double-quote-wrapped prompt — commit message `"updating specs for
      #{n}"`, push with no new branch or PR, `+1` reaction + reply +
      state-store update).
- [x] 4.5 Add unit tests for the error path (thrown exception mid-cycle)
      and the timeout path (session `StopAsync` called, `confused`
      reaction posted), including that processing continues to the next
      comment.
- [x] 4.6 Add a unit test confirming two eligible comments in one scan
      pass are processed sequentially, not concurrently.
- [x] 4.7 Add a unit test confirming `propose-workflow`,
      `implement-workflow`, and `update-workflow` scan passes run
      sequentially within one poll cycle, and that an unhandled exception
      from one does not prevent the others from running that cycle.
- [x] 4.8 Add/update the DI smoke test to confirm `IUpdateWorkflowRunner`
      resolves from the container.
- [x] 4.9 Run `dotnet test SpecRunner/SpecRunner.sln` and confirm all
      tests pass.

## 5. Documentation

- [x] 5.1 Update `SpecRunner/README.md` to describe the `/update` comment
      workflow: trigger syntax, the reaction status protocol
      (`eyes`/`+1`/`confused`), the natural-language prompt sent to the
      CLI agent, the untracked-PR error case, and how it relates to
      `/propose` and `/implement`.

## 6. Verification

- [x] 6.1 Run `dotnet build SpecRunner/SpecRunner.sln` and confirm it
      succeeds with no errors.
- [x] 6.2 Run `dotnet test SpecRunner/SpecRunner.sln` and confirm all
      tests pass.
- [x] 6.3 If a test GitHub repository and PAT are available, manually
      post a `/update` comment on an open PR tracked by a prior `/propose`
      run, run `SpecRunner.Console`, and confirm: the `eyes` reaction
      appears immediately, the CLI agent runs against the PR's existing
      branch with the natural-language update prompt, a new commit is
      pushed on success, the `+1` reaction and reply comment appear, and
      the state store records the comment as `done`.
