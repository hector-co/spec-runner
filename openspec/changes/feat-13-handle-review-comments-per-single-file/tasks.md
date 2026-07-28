## 1. GitHub review-comment endpoints

- [ ] 1.1 Add a `PrReviewComment` record to `SpecRunner.Core.Models` (comment id, file path, author, author association, body, creation time)
- [ ] 1.2 Add `ListPrReviewCommentsAsync`, `ListReviewCommentReactionsAsync`, `AddReviewCommentReactionAsync`, and `ReplyToReviewCommentAsync` to `IGitHubService`
- [ ] 1.3 Implement `ListPrReviewCommentsAsync` in `SpecRunner.GitHub/GitHubService.cs` against `GET /repos/{owner}/{repo}/pulls/{prNumber}/comments`, defaulting a missing `author_association` to `"NONE"`
- [ ] 1.4 Implement `ListReviewCommentReactionsAsync`/`AddReviewCommentReactionAsync` against `/pulls/comments/{commentId}/reactions`
- [ ] 1.5 Implement `ReplyToReviewCommentAsync` against `POST /pulls/{prNumber}/comments` with `in_reply_to`, reporting failures via a typed result/exception rather than an unstructured `HttpRequestException`

## 2. Command template

- [ ] 2.1 Add `SpecRunner.Console/CommandTemplates/update-file.txt` with `{{spec_name}}`, a blank line, `File: {{file_name}}`, `{{instructions}}`, and the standing unattended-run instruction block
- [ ] 2.2 Confirm the template is copied to the build output directory alongside the existing four templates

## 3. `UpdateWorkflowRunner` scan and dispatch

- [ ] 3.1 Extend `EligibleUpdateComment` with `CommentKind Kind` and `string? FileName`
- [ ] 3.2 In `RunOnceAsync`, in addition to `ReadPrCommentsAsync`, call `ListPrReviewCommentsAsync` per open PR and add matching `/update` review comments to the eligible list with `Kind = CommentKind.PrReviewComment` and `FileName` set from the comment's path
- [ ] 3.3 Route the "already reacted" check to `ListReviewCommentReactionsAsync` when `Kind == PrReviewComment`, and to `ListCommentReactionsAsync` otherwise
- [ ] 3.4 Route the initial `eyes` reaction to `AddReviewCommentReactionAsync`/`AddCommentReactionAsync` based on `Kind`
- [ ] 3.5 Route adoption-failure reporting to `ReplyToReviewCommentAsync`/`WritePrCommentAsync` (and the matching reaction call) based on `Kind`
- [ ] 3.6 Render `update-file` (with `file_name`) instead of `update` when `Kind == PrReviewComment`
- [ ] 3.7 Route success reporting (`+1` reaction, reply, state-store upsert with the comment's own `CommentKind`) based on `Kind`
- [ ] 3.8 Route error/timeout reporting (`confused` reaction, reply, state-store upsert with the comment's own `CommentKind`) based on `Kind`

## 4. Tests

- [ ] 4.1 Extend `SpecRunner.Tests`' fake GitHub service(s) under `Fakes/` to support review comments, their reactions, and threaded replies
- [ ] 4.2 Add `UpdateWorkflowRunnerTests` cases covering: an eligible file-anchored `/update` review comment is detected with its file name; an already-bot-reacted review comment is skipped; an unauthorized review-comment author is silently skipped; a review comment on an untracked PR that fails adoption gets a threaded reply and `confused` reaction with no further work; a successful run on a review comment posts `+1`/reply via the review-comment endpoints and records `CommentKind.PrReviewComment`; an error/timeout on a review comment reports via the review-comment endpoints
- [ ] 4.3 Add `CommandTemplateRendererTests` cases for `update-file.txt` covering placeholder substitution and the standing unattended-run instruction block
- [ ] 4.4 Run the full `SpecRunner.Tests` suite and confirm it passes

## 5. Verification

- [ ] 5.1 Run `openspec validate feat-13-handle-review-comments-per-single-file --strict` and confirm it passes
