## 1. Config knobs

- [ ] 1.1 Add `AllowedAuthorAssociations` (default `["OWNER", "MEMBER", "COLLABORATOR"]`) and `AllowedTriggerUsers` (default empty) to `SpecRunner/src/SpecRunner.Core/Configuration/SpecRunnerOptions.cs`

## 2. Surface `author_association` from the GitHub API

- [ ] 2.1 Add `AuthorAssociation` to `SpecRunner/src/SpecRunner.Core/Models/PrComment.cs`
- [ ] 2.2 Add `AuthorAssociation` to `SpecRunner/src/SpecRunner.Core/Models/GitHubIssueComment.cs`
- [ ] 2.3 In `SpecRunner/src/SpecRunner.GitHub/GitHubService.cs`'s `ReadPrCommentsAsync`, parse `commentElement.GetProperty("author_association").GetString()` (defaulting to `"NONE"` if absent) and pass it into the constructed `PrComment`
- [ ] 2.4 In `SpecRunner/src/SpecRunner.GitHub/GitHubService.cs`'s `ListIssueCommentsAsync`, parse `author_association` the same way and pass it into the constructed `GitHubIssueComment`

## 3. Shared authorization helper

- [ ] 3.1 Create `SpecRunner/src/SpecRunner.Core/CommentAuthorization.cs` with a static `IsAuthorized(string author, string authorAssociation, SpecRunnerOptions options)` method: `true` if `author` case-insensitively matches an entry in `options.AllowedTriggerUsers`, OR `authorAssociation` case-insensitively matches an entry in `options.AllowedAuthorAssociations`

## 4. Thread `Author`/`AuthorAssociation` into the `Eligible*Comment` records

- [ ] 4.1 Add `Author` and `AuthorAssociation` fields to `EligibleProposeComment.cs`
- [ ] 4.2 Add `Author` and `AuthorAssociation` fields to `EligibleImplementComment.cs`
- [ ] 4.3 Add `Author` and `AuthorAssociation` fields to `EligibleUpdateComment.cs`
- [ ] 4.4 Add `Author` and `AuthorAssociation` fields to `EligibleFinalizeComment.cs`

## 5. Enforce authorization in each runner

- [ ] 5.1 In `ProposeWorkflowRunner.RunOnceAsync`, after trigger-token matching succeeds, call `CommentAuthorization.IsAuthorized(comment.Author, comment.AuthorAssociation, _options)`; if unauthorized, `_logger.LogWarning` with comment id, issue number, and author, and do not add to `eligibleComments`; if authorized, construct `EligibleProposeComment` with `Author`/`AuthorAssociation` included
- [ ] 5.2 Mirror the same change into `ImplementWorkflowRunner.RunOnceAsync` (issue → PR number in the log)
- [ ] 5.3 Mirror the same change into `UpdateWorkflowRunner.RunOnceAsync`
- [ ] 5.4 Mirror the same change into `FinalizeWorkflowRunner.RunOnceAsync`

## 6. Tests

- [ ] 6.1 Add `SpecRunner/tests/SpecRunner.Tests/CommentAuthorizationTests.cs` covering: allowed association authorizes, disallowed association without allowlist match does not authorize, explicit `AllowedTriggerUsers` entry authorizes despite a disallowed association, and both author and association comparisons are case-insensitive
- [ ] 6.2 Update the `Comment(...)` test helper in `ProposeWorkflowRunnerTests.cs` to accept an `authorAssociation` parameter defaulting to an allowed value (e.g. `"OWNER"`), and add a case where a `/propose` comment from `author: "rando"`, `authorAssociation: "NONE"` is ignored (no reaction added, no git/CLI/PR calls made)
- [ ] 6.3 Apply the equivalent `Comment(...)` helper update and unauthorized-author test case to `ImplementWorkflowRunnerTests.cs`
- [ ] 6.4 Apply the equivalent `Comment(...)` helper update and unauthorized-author test case to `UpdateWorkflowRunnerTests.cs`
- [ ] 6.5 Apply the equivalent `Comment(...)` helper update and unauthorized-author test case to `FinalizeWorkflowRunnerTests.cs`
- [ ] 6.6 Check `SpecRunner.Tests/Fakes/` for any other `GitHubIssueComment`/`PrComment` construction sites and update them for the new `AuthorAssociation` field

## 7. Verification

- [ ] 7.1 Run `dotnet test` in `SpecRunner/` and confirm all existing tests pass plus the new authorization tests are green
- [ ] 7.2 Manually sanity-check `CommentAuthorization.IsAuthorized` against the association values GitHub actually sends (`OWNER`, `MEMBER`, `COLLABORATOR`, `CONTRIBUTOR`, `FIRST_TIME_CONTRIBUTOR`, `FIRST_TIMER`, `NONE`) to confirm the default allowlist authorizes only `OWNER`/`MEMBER`/`COLLABORATOR`
