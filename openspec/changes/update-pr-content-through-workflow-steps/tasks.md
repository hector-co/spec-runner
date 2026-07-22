## 1. Tasks-file reader abstraction

- [ ] 1.1 Add `ITasksFileReader` to `SpecRunner.Core/Abstractions/` with
      `ReadCurrentAsync(string specName, CancellationToken)` and
      `ReadArchivedAsync(string specName, CancellationToken)`, both
      returning `Task<string?>`.
- [ ] 1.2 Implement `ReadCurrentAsync` to read
      `{SpecRunnerOptions.LocalRepositoryPath}/openspec/changes/{specName}/tasks.md`,
      returning `null` if the file does not exist.
- [ ] 1.3 Implement `ReadArchivedAsync` to glob
      `{SpecRunnerOptions.LocalRepositoryPath}/openspec/changes/archive/*-{specName}/tasks.md`,
      returning the content of the most recently modified match, or `null`
      if none match.
- [ ] 1.4 Register the implementation in DI alongside the other
      `SpecRunner.Console` service registrations (`IGitService`,
      `IGitHubService`, `IStateStore`, etc.).
- [ ] 1.5 Add a `FakeTasksFileReader` (or equivalent) test double under
      `SpecRunner.Tests/Fakes/` that returns configurable current/archived
      content per spec name.

## 2. GitHub PR-description update

- [ ] 2.1 Add `UpdatePullRequestDescriptionAsync(int prNumber, string body, CancellationToken)`
      to `IGitHubService` (`SpecRunner.Core/Abstractions/IGitHubService.cs`).
- [ ] 2.2 Implement it in `SpecRunner.GitHub/GitHubService.cs` as
      `PATCH /repos/{owner}/{repo}/pulls/{prNumber}` with `{ "body": body }`,
      using the existing `SendAsync`/`GitHubApiException` failure-reporting
      pattern.
- [ ] 2.3 Add an `UpdatedPullRequestDescriptions` tracking list (PR number +
      body) to `RecordingGitHubService` and the corresponding stub behavior
      to `FakeGitHubService` (`SpecRunner.Tests/Fakes/`).
- [ ] 2.4 Add `GitHubServiceTests` coverage for
      `UpdatePullRequestDescriptionAsync`, mirroring the existing
      `MarkPrReadyForReviewAsync`/`CreateDraftPullRequestAsync` test style
      (success case and a failing-response case).

## 3. Propose workflow: seed PR body from tasks.md

- [ ] 3.1 In `ProposeWorkflowRunner.ProcessCommentAsync`, after the CLI
      agent session reaches `Completed` and before
      `CreateDraftPullRequestAsync`, call
      `ITasksFileReader.ReadCurrentAsync(specName)` and use its result (or
      `""` if `null`) as the PR body instead of `comment.IssueBody`.
- [ ] 3.2 Update `ProposeWorkflowRunnerTests` to assert the created draft
      PR's body comes from the fake tasks-file reader's stubbed content,
      including a case where the file is missing (empty body, no failure).

## 4. Implement workflow: refresh PR description after push

- [ ] 4.1 In `ImplementWorkflowRunner`, after the existing commit+push,
      read `ITasksFileReader.ReadCurrentAsync(specName)` and, if non-null,
      call `IGitHubService.UpdatePullRequestDescriptionAsync(prNumber, content)`
      before the existing `WritePrCommentAsync` success report; skip the
      call entirely if the content is `null`.
- [ ] 4.2 Update `ImplementWorkflowRunnerTests` to assert
      `UpdatePullRequestDescriptionAsync` is called with the stubbed
      `tasks.md` content after a successful push, and that it is NOT called
      when the reader returns `null`.

## 5. Finalize workflow: refresh PR description and close the issue

- [ ] 5.1 In `FinalizeWorkflowRunner`, after the existing commit+push and
      before `MarkPrReadyForReviewAsync`, read
      `ITasksFileReader.ReadArchivedAsync(specName)`, build
      `$"{content ?? string.Empty}\n\nCloses #{issueNumber}"`, and call
      `IGitHubService.UpdatePullRequestDescriptionAsync(prNumber, finalBody)`.
- [ ] 5.2 Update `FinalizeWorkflowRunnerTests` to assert
      `UpdatePullRequestDescriptionAsync` is called with the archived
      `tasks.md` content followed by `Closes #{issue-number}` before the PR
      is marked ready for review, including a case where the archived file
      is missing (body is just `Closes #{issue-number}`).

## 6. Spec sync

- [ ] 6.1 Run `openspec validate update-pr-content-through-workflow-steps --type change`
      and confirm it passes.
- [ ] 6.2 After implementation and tests are green, sync the delta specs
      (`propose-workflow`, `implement-workflow`, `finalize-workflow`,
      `github-operations`, `tasks-file-access`) into `openspec/specs/` per
      the normal archive flow.
