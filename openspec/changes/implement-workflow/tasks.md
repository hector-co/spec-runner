## 1. `IGitService` extension (`SpecRunner.Git`)

- [ ] 1.1 Add `FetchAsync(string branchName, CancellationToken)` to
      `IGitService` in `SpecRunner.Core/Abstractions`, alongside the
      existing members.
- [ ] 1.2 Implement `FetchAsync`: `git fetch origin {branchName}` in the
      local clone, without checking out or merging.
- [ ] 1.3 Wrap the new command invocation with the same
      typed-failure/stderr-capture handling the other `GitService`
      methods already use.

## 2. `IGitHubService` extensions and real implementation (`SpecRunner.GitHub`)

- [ ] 2.1 Add a `GitHubPullRequest` model (`SpecRunner.Core/Models`)
      exposing number, title, body, and head branch name.
- [ ] 2.2 Add `ListOpenPullRequestsAsync(CancellationToken)` to
      `IGitHubService`, returning `IReadOnlyList<GitHubPullRequest>`.
- [ ] 2.3 Implement `ListOpenPullRequestsAsync` (`GET
      /repos/{owner}/{repo}/pulls` filtered to open), mapping each PR's
      number, title, body, and `head.ref` to `GitHubPullRequest`.
- [ ] 2.4 Implement `ReadPrCommentsAsync` for real (`GET
      /repos/{owner}/{repo}/issues/{prNumber}/comments`, same endpoint
      shape as issue comments), removing its `NotImplementedException`.
- [ ] 2.5 Implement `WritePrCommentAsync` for real (`POST
      /repos/{owner}/{repo}/issues/{prNumber}/comments`), removing its
      `NotImplementedException`.
- [ ] 2.6 Wrap both new/implemented calls with the same typed
      failure/exception handling `GitHubService`'s other members already
      use.
- [ ] 2.7 Leave `CreatePullRequestAsync` (non-draft) and
      `MarkPrReadyForReviewAsync` as `NotImplementedException`
      placeholders (unchanged).

## 3. Implement-workflow core abstractions (`SpecRunner.Core`)

- [ ] 3.1 Add `IImplementWorkflowRunner` to `SpecRunner.Core/Abstractions`
      with a single `Task RunOnceAsync(CancellationToken)` member.
- [ ] 3.2 Add a supporting model for an eligible `/implement` comment
      (PR number, PR head branch, comment id, and the comment body with
      the `/implement` token stripped).
- [ ] 3.3 Delete the unused `TrackedPr` model
      (`SpecRunner.Core/Models/TrackedPr.cs`).

## 4. Implement-workflow orchestration (`SpecRunner.Console`)

- [ ] 4.1 Add an `ImplementWorkflowRunner` implementing
      `IImplementWorkflowRunner` in `SpecRunner.Console`, composing
      `IGitHubService`, `IGitService`, `IStateStore`,
      `ICliAgentSessionFactory`, and `IOptions<SpecRunnerOptions>`.
- [ ] 4.2 Implement the eligible-comment scan: list open PRs, read each
      PR's comments via `ReadPrCommentsAsync`, filter to comments whose
      trimmed body is exactly `/implement` or starts with `/implement`
      followed by whitespace, and exclude comments already carrying an
      `eyes`/`+1`/`confused` reaction from the authenticated bot login.
- [ ] 4.3 Implement sequential per-comment processing: for each eligible
      comment, add the `eyes` reaction first, then look up
      `IStateStore.FindByPrNumberAsync` for that comment's PR.
- [ ] 4.4 Implement the untracked-PR path: if no state-store record is
      found, post the explanatory reply and add the `confused` reaction,
      then return without any git operation, CLI-agent session, or
      state-store write.
- [ ] 4.5 Implement the tracked-PR path: `FetchAsync` the PR's head
      branch, `SwitchBranchAsync` to it, `ResetHardAsync` it to
      `origin/{branch}`.
- [ ] 4.6 Build the instructions string (comment body with the leading
      `/implement` token and its separating whitespace removed) and start
      a CLI agent session with initial prompt `"/opsx-apply {spec-name}
      {instructions}"` using the tracked record's spec name; await a
      terminal session state.
- [ ] 4.7 On `Completed`: `CommitAsync` with message
      `"implementing #{issue-number}"`, `PushAsync` the PR's branch, then
      add the `+1` reaction and post a reply confirming the push; upsert
      the state store (comment status `done` under the tracked issue
      number).
- [ ] 4.8 Wrap the per-comment cycle (branch refresh through push) with
      `SpecRunnerOptions.TaskTimeout`; on timeout, call `StopAsync` on any
      in-flight CLI agent session.
- [ ] 4.9 On any thrown exception or timeout for a tracked PR: add the
      `confused` reaction, post a short human-readable error summary,
      upsert the state store with comment status `error` under the
      tracked issue number, and continue to the next eligible comment
      rather than aborting the scan pass.
- [ ] 4.10 Register `IImplementWorkflowRunner` in `SpecRunner.Console`'s DI
      container.

## 5. Poll loop and entry point (`SpecRunner.Console`)

- [ ] 5.1 Extend `PollingLoop.RunAsync` to accept both
      `IProposeWorkflowRunner` and `IImplementWorkflowRunner`, calling
      `propose-workflow`'s `RunOnceAsync()` then `implement-workflow`'s
      `RunOnceAsync()` in order each cycle, each wrapped in its own
      try/catch so one workflow's unhandled exception doesn't prevent the
      other from running that cycle.
- [ ] 5.2 Update `Program.cs` to resolve `IImplementWorkflowRunner` and
      pass it to the updated `PollingLoop.RunAsync` alongside
      `IProposeWorkflowRunner`.

## 6. Tests

- [ ] 6.1 Add unit tests for `IGitService.FetchAsync` against a throwaway
      local git repository fixture, covering success and a
      failing-command case.
- [ ] 6.2 Add unit tests for `ListOpenPullRequestsAsync`,
      `ReadPrCommentsAsync`, and `WritePrCommentAsync` against a
      fake/mocked HTTP handler, covering success and failure responses.
- [ ] 6.3 Add unit tests for `/implement` comment-eligibility matching
      (exact match, trailing text, mid-sentence/`/implemented`
      non-match) and instructions extraction (token + whitespace
      stripped).
- [ ] 6.4 Add unit tests for the already-reacted skip logic
      (`eyes`/`+1`/`confused` from the bot vs. a human reaction vs. no
      reaction).
- [ ] 6.5 Add unit tests for the untracked-PR path (explanatory reply,
      `confused` reaction, no git/CLI/state-store activity).
- [ ] 6.6 Add unit tests for the success path (branch fetch/switch/reset,
      CLI agent invocation with the expected `/opsx-apply` prompt,
      commit/push calls with no new branch or PR, `+1` reaction + reply +
      state-store update).
- [ ] 6.7 Add unit tests for the error path (thrown exception mid-cycle)
      and the timeout path (session `StopAsync` called, `confused`
      reaction posted), including that processing continues to the next
      comment.
- [ ] 6.8 Add a unit test confirming two eligible comments in one scan
      pass are processed sequentially, not concurrently.
- [ ] 6.9 Add a unit test confirming `propose-workflow` and
      `implement-workflow` scan passes run sequentially within one poll
      cycle, and that an unhandled exception from one does not prevent
      the other from running that cycle.
- [ ] 6.10 Add/update the DI smoke test to confirm
      `IImplementWorkflowRunner` resolves from the container.
- [ ] 6.11 Verify `dotnet test SpecRunner/SpecRunner.sln` passes.

## 7. Documentation

- [ ] 7.1 Update `SpecRunner/README.md` to describe the `/implement`
      comment workflow: trigger syntax, the reaction status protocol
      (`eyes`/`+1`/`confused`), the untracked-PR error case, and how it
      relates to `/propose`.

## 8. Verification

- [ ] 8.1 Run `dotnet build SpecRunner/SpecRunner.sln` and confirm it
      succeeds with no errors.
- [ ] 8.2 Run `dotnet test SpecRunner/SpecRunner.sln` and confirm all
      tests pass.
- [ ] 8.3 If a test GitHub repository and PAT are available, manually
      post a `/implement` comment on an open draft PR created by
      `/propose`, run `SpecRunner.Console`, and confirm: the `eyes`
      reaction appears immediately, the CLI agent runs against the PR's
      existing branch, a new commit is pushed on success, the `+1`
      reaction and reply comment appear, and the state store records the
      comment as `done`.
