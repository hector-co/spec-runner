## Why

GitHub lets a reviewer leave comments anchored to a specific file (or several files) as part of one PR review, separate from the PR's general conversation thread. Today `UpdateWorkflowRunner` only scans conversation-style PR comments via `ReadPrCommentsAsync` (the GitHub "issue comments" endpoint); a `/update` left directly on a changed file is invisible to it and gets no response. This change closes that gap for the single-file case, so reviewers can request an OpenSpec update from the exact line/file they're looking at instead of switching to the Conversation tab.

## What Changes

- `IGitHubService` gains operations to list a PR's review comments (each with its file path, author, author association, and body) and to read/add reactions and post a threaded reply on a review comment, since GitHub review comments live under a distinct `/pulls/comments/{id}` endpoint family from the issue-comment endpoints already used for conversation comments.
- `UpdateWorkflowRunner`'s scan pass also treats an eligible `/update` review comment anchored to exactly one file as a trigger, applying the same authorization check, already-reacted skip check, and PR-tracking/adoption handling as the existing conversation-comment path.
- The rendered CLI-agent prompt for a file-anchored trigger includes the commented-on file's path (`File: <filename>`) between the change-name line and the instructions, via a new template variant; the instructions text is the comment body with the leading `/update` token and its separating whitespace removed, matching the existing stripping rule.
- Review-comment-sourced tracked comments are recorded with `CommentKind.PrReviewComment` (already defined in the enum, previously unused by any runner).
- **Out of scope**: a review's top-level summary body, and reviews whose comments span more than one file, are not handled by this change — only a review comment anchored to a single file. Multi-file handling is left for a follow-up.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `update-workflow`: the scan pass also considers PR review comments anchored to a single file as `/update` triggers, alongside existing conversation comments, and renders the prompt with the commented file's path included.
- `github-operations`: adds operations to list a PR's review comments (with file path), read/add reactions on a review comment, and post a threaded reply on a review comment.
- `command-templates`: adds a template variant for the `update` prompt that includes a `File:` line, used when the triggering comment is anchored to a file.

## Impact

- `SpecRunner.Core`: `IGitHubService` interface, a new review-comment model (or an extended `PrComment`/`EligibleUpdateComment` carrying an optional file path), `CommentKind.PrReviewComment` now used in production code.
- `SpecRunner.GitHub`: `GitHubService.cs` gains calls to the `GET /repos/{owner}/{repo}/pulls/{prNumber}/comments` endpoint and the `/pulls/comments/{commentId}` reaction/reply endpoints.
- `SpecRunner.Console`: `UpdateWorkflowRunner.cs` scan/process logic, a new `CommandTemplates/*.txt` file.
- `SpecRunner.Tests`: fake GitHub service(s) under `Fakes/` gain review-comment support; new/extended tests for `UpdateWorkflowRunner` and `CommandTemplateRenderer`.
- No breaking changes; existing conversation-comment `/update` behavior is unchanged.

## Assumptions

- A review comment is in scope only when it is anchored to exactly one file (GitHub's normal file-diff review comment always has one `path`). A review's top-level summary body and reviews whose file comments span multiple files are explicitly out of scope, per this change's "per single file" scope — handling those is left for a later change.
- Replies to a file-anchored trigger use GitHub's threaded review-comment reply (`in_reply_to` the triggering comment), so the response lands in that file's review thread, mirroring how the existing conversation path replies on the PR's Conversation tab.
- The `File:` line uses the file path GitHub currently reports for that review comment.
- All other `/update` rules (authorization via `CommentAuthorization.IsAuthorized`, skipping comments already reacted to by the bot, tracked/untracked-PR handling via `IStateStore`/`PrAdoptionService`, commit/push, PR-description refresh, success/error/timeout reporting) apply unchanged, just sourced from review comments in addition to conversation comments.
