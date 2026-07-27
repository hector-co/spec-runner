## 1. State store: nullable issue number, PR-number-keyed comments

- [ ] 1.1 Change `TrackedIssue.IssueNumber` (`SpecRunner.Core/Models/TrackedIssue.cs`) from `int` to `int?`, keeping it a positional record parameter.
- [ ] 1.2 Update `IStateStore.UpsertCommentAsync` (`SpecRunner.Core/Abstractions/IStateStore.cs`) to take a PR number instead of an issue number as its join-key parameter.
- [ ] 1.3 In `SqliteStateStore` (`SpecRunner.State/SqliteStateStore.cs`): change `TrackedIssues.IssueNumber` handling to allow `DBNull`, replace the column's unique index with a partial unique index (unique where not null), and update `ReadTrackedIssue`/parameter binding for the nullable column.
- [ ] 1.4 Update `SqliteStateStore.UpsertTrackedIssueAsync`'s upsert key/lookup: continue keying inserts/updates by issue number when present, but support upserting a record whose issue number is null (keyed by PR number in that case).
- [ ] 1.5 Update `SqliteStateStore.UpsertCommentAsync` to look up the parent `TrackedIssues.Id` by PR number instead of issue number.
- [ ] 1.6 Add an `EnsureIssueNumberNullableAsync`-style migration step (matching the existing `EnsureBranchNameColumnAsync` pattern) that rebuilds the `TrackedIssues` table to drop the `NOT NULL` constraint on `IssueNumber` for a pre-existing database file, preserving all existing rows.
- [ ] 1.7 Update every existing call site of `UpsertCommentAsync` across the four workflow runners to pass a PR number instead of an issue number.

## 2. Git operations: added-folder discovery

- [ ] 2.1 Add `ListAddedSpecFolderNamesAsync(string baseBranch, string headBranch, CancellationToken)` to `IGitService` (`SpecRunner.Core/Abstractions/IGitService.cs`).
- [ ] 2.2 Implement it in `GitService` (`SpecRunner.Git/GitService.cs`) using a git diff of `openspec/changes/` between the two refs (e.g. `git diff --name-only <base>...<head> -- openspec/changes`), reduced to distinct top-level directory names, without checking out or otherwise changing the currently checked-out branch.
- [ ] 2.3 Surface failures through the existing `GitCommandException` pattern used by other `IGitService` members.

## 3. GitHub operations: closing-issue discovery

- [ ] 3.1 Add `ListClosingIssueNumbersAsync(int prNumber, CancellationToken)` to `IGitHubService` (`SpecRunner.Core/Abstractions/IGitHubService.cs`).
- [ ] 3.2 Implement it in `GitHubService` (`SpecRunner.GitHub/GitHubService.cs`) as a GraphQL query for `repository(owner:, name:) { pullRequest(number: $number) { closingIssuesReferences { nodes { number } } } }`, reusing the existing GraphQL request plumbing from `MarkPrReadyForReviewAsync`.
- [ ] 3.3 Surface a failing GraphQL call through `GitHubApiException`, matching the existing pattern.

## 4. Shared PR-adoption logic

- [ ] 4.1 Define an `IPrAdoptionService` (or similar) in `SpecRunner.Core` with a single operation that, given a PR (number, head branch), attempts adoption and returns either a resolved `TrackedIssue` ready to upsert or a typed failure describing which discovery step failed and why (no folder found / multiple folders found / multiple issues found), including any candidate names/numbers for the failure message.
- [ ] 4.2 Implement it in `SpecRunner.Console`, composing `IGitService.FetchAsync` + `ListAddedSpecFolderNamesAsync` for folder discovery and `IGitHubService.ListClosingIssueNumbersAsync` for issue discovery, per the `pr-adoption` capability's rules (zero/one/many for each).
- [ ] 4.3 On successful discovery, build the `TrackedIssue` from the PR's `HeadBranch`, the discovered spec name, the PR number, and the discovered issue number (nullable).
- [ ] 4.4 Add unit tests for the adoption service covering: one folder + no issue, one folder + one issue, zero folders, multiple folders, multiple issues.

## 5. Implement-workflow integration

- [ ] 5.1 In `ImplementWorkflowRunner.ProcessCommentAsync`, when `FindByPrNumberAsync` returns null, call the adoption service before falling back to `ReportUntrackedPrAsync`; on success, upsert the returned record via `IStateStore.UpsertTrackedIssueAsync` and continue with the existing tracked-PR flow.
- [ ] 5.2 On adoption failure, post the failure's specific message (not the generic "opened by /propose" text) and add the `confused` reaction, reusing the existing refusal mechanics.
- [ ] 5.3 Update the commit-message construction to branch on `trackedIssue.IssueNumber is null`, using `"applying specs for PR #{PrNumber}"` in that case.
- [ ] 5.4 Update the PR title rewrite to branch on `trackedIssue.IssueNumber is null`: skip the `#{issue}: ` marker logic and use `"Implementations: {currentTitle}"` in that case. Adjust or extend `PullRequestTitles` (`SpecRunner.Console/PullRequestTitles.cs`) if it needs a no-issue code path.
- [ ] 5.5 Update `ReportSuccessAsync`/`ReportTimeoutAsync`/`ReportErrorAsync`/`RecordCommentStatusAsync` to call `UpsertCommentAsync` with the tracked record's PR number instead of issue number.

## 6. Update-workflow integration

- [ ] 6.1 Apply the same adoption-before-refusal change as 5.1/5.2 to `UpdateWorkflowRunner.ProcessCommentAsync`.
- [ ] 6.2 Update the commit-message construction to branch on `trackedIssue.IssueNumber is null`, using `"updating specs for PR #{PrNumber}"` in that case.
- [ ] 6.3 Update `ReportSuccessAsync`/`ReportTimeoutAsync`/`ReportErrorAsync`/`RecordCommentStatusAsync` to call `UpsertCommentAsync` with the tracked record's PR number instead of issue number.

## 7. Finalize-workflow integration

- [ ] 7.1 Apply the same adoption-before-refusal change as 5.1/5.2 to `FinalizeWorkflowRunner.ProcessCommentAsync`.
- [ ] 7.2 Update the commit-message construction to branch on `trackedIssue.IssueNumber is null`, using `"finalizing specs for PR #{PrNumber}"` in that case.
- [ ] 7.3 Update the final PR body construction to omit the `"\n\nCloses #{IssueNumber}"` suffix entirely when `trackedIssue.IssueNumber is null`.
- [ ] 7.4 Update the PR title rewrite to branch on `trackedIssue.IssueNumber is null`: when null, leave the title unchanged (skip calling `UpdatePullRequestTitleAsync` for the rename step).
- [ ] 7.5 Update `ReportSuccessAsync`/`ReportTimeoutAsync`/`ReportErrorAsync`/`RecordCommentStatusAsync` to call `UpsertCommentAsync` with the tracked record's PR number instead of issue number.

## 8. Tests

- [ ] 8.1 Update `SqliteStateStore` tests for a nullable `IssueNumber`: round-trip a record with no issue number, upsert comments keyed by PR number, and migrate a pre-existing `NOT NULL` database file in place.
- [ ] 8.2 Add `GitService` tests for `ListAddedSpecFolderNamesAsync` covering zero/one/multiple added folders and that the checked-out branch is left unchanged.
- [ ] 8.3 Add `GitHubService` tests (or fakes) for `ListClosingIssueNumbersAsync` covering zero/one/multiple linked issues and a failing GraphQL response.
- [ ] 8.4 Add `ImplementWorkflowRunner`/`UpdateWorkflowRunner`/`FinalizeWorkflowRunner` tests covering: successful adoption with an issue, successful adoption without an issue, adoption refusal for no folder found, and adoption refusal for multiple folders/issues found.
- [ ] 8.5 Add regression tests confirming a `/propose`-created (already-tracked) PR's commit messages, PR title, and PR body are byte-for-byte unchanged by this change.

## 9. Documentation

- [ ] 9.1 Update `SpecRunner/README.md`'s "Untracked PR" sections for `implement-workflow`, `update-workflow`, and `finalize-workflow` to describe the adoption attempt and its possible outcomes, replacing the current "the PR wasn't opened by /propose" framing.
