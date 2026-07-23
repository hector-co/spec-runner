## 1. GitHub service: add title update operation

- [x] 1.1 Add `Task UpdatePullRequestTitleAsync(int prNumber, string title, CancellationToken cancellationToken = default)` to `SpecRunner.Core.Abstractions.IGitHubService`.
- [x] 1.2 Implement it in `SpecRunner.GitHub.GitHubService`, mirroring `UpdatePullRequestDescriptionAsync` (PATCH `/repos/{owner}/{repo}/pulls/{prNumber}` with `{ title }`).
- [x] 1.3 Implement the fake in `SpecRunner.Tests.Fakes.FakeGitHubService` (no-op, matching its sibling method).
- [x] 1.4 Implement and record calls in `SpecRunner.Tests.Fakes.RecordingGitHubService`, matching how `UpdatePullRequestDescriptionAsync` is recorded.
- [x] 1.5 Add a `GitHubServiceTests` case covering `UpdatePullRequestTitleAsync` performing the real PATCH call (mirroring the existing description-update test).

## 2. Shared PR-title parsing helper

- [x] 2.1 Add an internal static helper (e.g. `SpecRunner.Console.PullRequestTitles.ExtractIssueName(string currentTitle, int issueNumber)`) that returns the text following the literal substring `"#{issueNumber}: "` in `currentTitle`, or the whole `currentTitle` if that substring isn't present.
- [x] 2.2 Add unit tests for the helper: title in the expected `"... #N: name"` shape, and a title with no recognizable `"#N: "` segment (fallback to whole title).

## 3. `/implement` workflow: rename PR title after a push

- [x] 3.1 In `ImplementWorkflowRunner.ProcessCommentAsync`, after the commit/push step, derive `<issue-name>` from the PR's current title (already available via the `pr.Title` captured when scanning for eligible comments, or an extra lookup if not already threaded through) using the helper from Task 2.
- [x] 3.2 Call `IGitHubService.UpdatePullRequestTitleAsync(comment.PrNumber, $"Implementations for #{trackedIssue.IssueNumber}: {issueName}")` unconditionally (independent of whether `tasks.md` content was found for the description refresh).
- [x] 3.3 Update `ImplementWorkflowRunnerTests` to assert the new title-update call, including a case where `tasks.md` is missing but the rename still happens.

## 4. `/finalize` workflow: rename PR title after archiving

- [x] 4.1 In `FinalizeWorkflowRunner.ProcessCommentAsync`, after the existing `UpdatePullRequestDescriptionAsync` call and before `MarkPrReadyForReviewAsync`, derive `<issue-name>` from the PR's current title using the helper from Task 2.
- [x] 4.2 Call `IGitHubService.UpdatePullRequestTitleAsync(comment.PrNumber, $"#{trackedIssue.IssueNumber}: {issueName}")`.
- [x] 4.3 Update `FinalizeWorkflowRunnerTests` to assert the new title-update call happens after the description update and before marking the PR ready for review.

## 5. `/update` workflow: refresh PR description after a push

- [x] 5.1 Add `ITasksFileReader` to `UpdateWorkflowRunner`'s constructor and store it as a field, matching `ImplementWorkflowRunner`'s existing pattern.
- [x] 5.2 After the commit/push step in `ProcessCommentAsync`, call `ITasksFileReader.ReadCurrentAsync(trackedIssue.SpecName, ...)` and, if content is found, call `IGitHubService.UpdatePullRequestDescriptionAsync(comment.PrNumber, tasksContent, ...)`.
- [x] 5.3 Update the `UpdateWorkflowRunner` registration (DI/composition root) to supply `ITasksFileReader`.
- [x] 5.4 Update `UpdateWorkflowRunnerTests` to assert the description-update call happens on success, and that it's skipped when no `tasks.md` content is found.

## 6. Verification

- [x] 6.1 Run the full `SpecRunner.Tests` suite and confirm all tests pass.
- [x] 6.2 Re-read `implement-workflow`'s existing "PR description with current task list" requirement and confirm no code change was needed there (already implemented) — no separate task, just a sanity check during review.
