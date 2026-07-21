## 1. `IGitService` real implementation (`SpecRunner.Git`)

- [x] 1.1 Extend `IGitService` in `SpecRunner.Core/Abstractions` with
      `ResetHardAsync(string targetRef, CancellationToken)`, alongside
      the existing `PullAsync`, `CreateBranchAsync`, `SwitchBranchAsync`,
      `CommitAsync`, `PushAsync` signatures.
- [x] 1.2 Implement `PullAsync`: fetch and fast-forward
      `SpecRunnerOptions.BaseBranchName` from `origin` in the clone at
      `SpecRunnerOptions.LocalRepositoryPath`.
- [x] 1.3 Implement `ResetHardAsync`: `git reset --hard {targetRef}` plus
      `git clean -fd` to remove untracked files, in the local clone.
- [x] 1.4 Implement `CreateBranchAsync`/`SwitchBranchAsync` against the
      local clone.
- [x] 1.5 Implement `CommitAsync`: stage all pending changes and commit
      with the supplied message.
- [x] 1.6 Implement `PushAsync`: push the current branch to `origin`,
      setting upstream tracking if not already set.
- [x] 1.7 Wrap each git command invocation so a non-zero exit surfaces as
      a typed result/exception carrying captured stderr, rather than an
      unhandled process exception.
- [x] 1.8 Remove the placeholder `NotImplementedException` `IGitService`
      implementation.

## 2. `IGitHubService` extensions and real implementation (`SpecRunner.GitHub`)

- [x] 2.1 Extend `IGitHubService` in `SpecRunner.Core/Abstractions` with:
      resolving the authenticated login, listing open issues with
      comments, listing reactions on an issue comment, adding a reaction
      to an issue comment, creating an issue comment, alongside the
      existing `CreateDraftPullRequestAsync` (and the still-unimplemented
      `CreatePullRequestAsync`/PR-comment/mark-ready-for-review members).
- [x] 2.2 Implement authenticated-login resolution (`GET /user`) with
      in-process caching for the lifetime of one run.
- [x] 2.3 Implement listing open issues with comments (`GET
      /repos/{owner}/{repo}/issues` filtered to open, plus each issue's
      comments).
- [x] 2.4 Implement listing and adding reactions on an issue comment
      (`GET`/`POST
      /repos/{owner}/{repo}/issues/comments/{id}/reactions`).
- [x] 2.5 Implement creating an issue comment (`POST
      /repos/{owner}/{repo}/issues/{number}/comments`).
- [x] 2.6 Implement `CreateDraftPullRequestAsync` for real (`POST
      /repos/{owner}/{repo}/pulls` with `draft: true`, head branch, and
      `SpecRunnerOptions.BaseBranchName` as base), returning the created
      PR number.
- [x] 2.7 Wrap each new GitHub REST API call so a non-2xx response or
      transport failure surfaces as a typed result/exception, rather than
      an unhandled `HttpRequestException`.
- [x] 2.8 Leave `CreatePullRequestAsync`, PR-comment read/write, and
      mark-ready-for-review as `NotImplementedException` placeholders
      (unchanged from the current implementation).

## 3. Propose-workflow core abstractions (`SpecRunner.Core`)

- [x] 3.1 Add `IProposeWorkflowRunner` to `SpecRunner.Core/Abstractions`
      with a single `Task RunOnceAsync(CancellationToken)` member.
- [x] 3.2 Add any supporting model(s) needed to represent an eligible
      comment/issue pair as it flows through the workflow (e.g. issue
      number, issue title/body, comment id).

## 4. Propose-workflow orchestration (`SpecRunner.Console`)

- [x] 4.1 Add a `ProposeWorkflowRunner` implementing
      `IProposeWorkflowRunner` in `SpecRunner.Console`, composing
      `IGitHubService`, `IGitService`, `IStateStore`,
      `ICliAgentSessionFactory`, `ISpecNameResolver`, and
      `IOptions<SpecRunnerOptions>`.
- [x] 4.2 Implement the eligible-comment scan: list open issues with
      comments, filter to comments whose trimmed body is exactly
      `/propose` or starts with `/propose` followed by whitespace, and
      exclude comments already carrying an `eyes`/`rocket`/`confused`
      reaction from the authenticated bot login.
- [x] 4.3 Implement sequential per-comment processing: for each eligible
      comment, add the `eyes` reaction first, then branch into the
      already-has-PR path or the fresh-proposal path.
- [x] 4.4 Implement the already-has-PR path: look up
      `IStateStore.FindByIssueNumberAsync`; if a PR number is present,
      post the `"This issue already has an active Draft PR: #{pr}.
      Please add /update to the PR instead."` reply and add the `rocket`
      reaction.
- [x] 4.5 Implement the fresh-proposal path: `PullAsync` base branch,
      `ResetHardAsync` to it, `CreateBranchAsync`/`SwitchBranchAsync` to
      `feature/{issue-number}`.
- [x] 4.6 Resolve the spec name via `ISpecNameResolver` and start a CLI
      agent session with initial prompt `"/opsx-propose {spec-name}\n
      {issue-body}"`; await a terminal session state.
- [x] 4.7 On `Completed`: `CommitAsync` with message `"adding specs for
      #{issue-number}"`, `PushAsync`, `CreateDraftPullRequestAsync`, then
      add the `rocket` reaction and post `"Created Draft PR #{pr} for
      this issue."`; upsert the state store (issue number, spec name, PR
      number, comment status `done`).
- [x] 4.8 Wrap the per-comment cycle (branch setup through PR creation)
      with `SpecRunnerOptions.TaskTimeout`; on timeout, call
      `StopAsync` on any in-flight CLI agent session.
- [x] 4.9 On any thrown exception or timeout: add the `confused`
      reaction, post a short human-readable error summary (not a raw
      exception dump), upsert the state store with comment status
      `error`, and continue to the next eligible comment rather than
      aborting the scan pass.
- [x] 4.10 Register `IProposeWorkflowRunner` in `SpecRunner.Console`'s DI
      container.
- [x] 4.11 Update the console entry point (`Program.cs`) to call
      `IProposeWorkflowRunner.RunOnceAsync` after a `Connected` repository
      connection result, before exiting, and to keep exiting non-zero
      only for a failed connection test or an unhandled exception from
      the scan pass itself.

## 5. Tests

- [x] 5.1 Add unit tests for `IGitService` operations (pull, reset-hard,
      create/switch branch, commit, push) against a throwaway local git
      repository fixture, covering both success and a failing-command
      case.
- [x] 5.2 Add unit tests for the new `IGitHubService` operations
      (identity resolution/caching, issue+comment listing, reaction
      list/add, issue comment creation, draft PR creation) against a
      fake/mocked HTTP handler, covering success and failure responses.
- [x] 5.3 Add unit tests for comment-eligibility matching (exact match,
      trailing text, mid-sentence/`/proposed` non-match).
- [x] 5.4 Add unit tests for the already-reacted skip logic (bot
      reaction vs. human reaction vs. no reaction).
- [x] 5.5 Add unit tests for the already-has-PR short-circuit path.
- [x] 5.6 Add unit tests for the success path (branch reset/create, CLI
      agent invocation with the expected prompt, commit/push/draft-PR
      calls, reaction + reply + state-store update).
- [x] 5.7 Add unit tests for the error path (thrown exception mid-cycle)
      and the timeout path (session `StopAsync` called, `confused`
      reaction posted), including that processing continues to the next
      comment.
- [x] 5.8 Add a unit test confirming two eligible comments in one scan
      pass are processed sequentially, not concurrently.
- [x] 5.9 Add/update the DI smoke test to confirm `IProposeWorkflowRunner`
      resolves from the container.
- [x] 5.10 Verify `dotnet test SpecRunner/SpecRunner.sln` passes.

## 6. Documentation

- [x] 6.1 Update `SpecRunner/README.md` to describe the `/propose`
      comment workflow: trigger syntax, the reaction status protocol
      (`eyes`/`rocket`/`confused`), and the single-scan-per-invocation
      model.

## 7. Verification

- [x] 7.1 Run `dotnet build SpecRunner/SpecRunner.sln` and confirm it
      succeeds with no errors.
- [x] 7.2 Run `dotnet test SpecRunner/SpecRunner.sln` and confirm all
      tests pass.
- [ ] 7.3 If a test GitHub repository and PAT are available, manually
      post a `/propose` comment on an open issue, run
      `SpecRunner.Console`, and confirm: the `eyes` reaction appears
      immediately, a `feature/{issue}` branch and draft PR are created on
      success, the `rocket` reaction and reply comment appear, and the
      state store records the association.
