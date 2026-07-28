## Context

`UpdateWorkflowRunner.RunOnceAsync` (`SpecRunner.Console/UpdateWorkflowRunner.cs`) currently builds its eligible-comment list from `IGitHubService.ReadPrCommentsAsync`, which calls GitHub's issue-comments endpoint (`GET /repos/{owner}/{repo}/issues/{prNumber}/comments`) — the PR's general Conversation tab. GitHub review comments (left on a specific line of a specific file, via `GET /repos/{owner}/{repo}/pulls/{prNumber}/comments`) are a distinct resource with their own id space and their own reaction/reply endpoints (`/pulls/comments/{commentId}/...`, not `/issues/comments/{commentId}/...`). Every comment returned by that endpoint already belongs to exactly one file (GitHub attaches a `path` to each one individually), so "per single file" here is simply "the existing per-comment `/update` model, applied to this second comment source" — there is no batching-by-review to reason about. What is genuinely out of scope is a review's own top-level summary body (`GET /pulls/{prNumber}/reviews`, the `body` field on the review object itself), a third, still-untouched comment source that isn't fetched by either endpoint this change adds.

`CommentKind.PrReviewComment` already exists in `SpecRunner.Core.Models.CommentKind` and is exercised in `SqliteStateStoreTests`, but no production runner has ever constructed one — this change is its first real use.

## Goals / Non-Goals

**Goals:**
- `/update` triggers work identically whether the triggering comment lives in the PR conversation or is anchored to a file via a review comment.
- Reuse the existing eligibility (`TryGetInstructions`), authorization (`CommentAuthorization.IsAuthorized`), already-reacted skip, and tracked/untracked-PR (adoption) handling unchanged — only the comment *source*, and the endpoints used to react/reply to a found comment, differ by kind.
- The rendered prompt makes the commented-on file explicit, so the CLI agent's OpenSpec update reflects file-specific context.

**Non-Goals:**
- Handling a review's top-level summary comment (the `body` on a `pull_request_review`, submitted alongside file comments or on its own) — a different GitHub resource, left for a later change.
- Any change to `/propose`, `/implement`, or `/finalize` — this only touches the `/update` workflow.
- Batching multiple review comments from the same review submission into one CLI-agent run — each eligible review comment is still processed as its own independent trigger, exactly like today's per-conversation-comment model.

## Decisions

**1. Add explicit, endpoint-specific `IGitHubService` members rather than parameterizing the existing ones.**
`ListCommentReactionsAsync`/`AddCommentReactionAsync`/`WritePrCommentAsync` are hard-wired to the issue-comment endpoints and are also used by the propose/implement/finalize workflows against genuine issue comments — changing their behavior based on a flag would risk silently breaking those call sites. Instead, add:
- `ListPrReviewCommentsAsync(int prNumber)` → `GET /pulls/{prNumber}/comments`, returning a new `PrReviewComment` record (`CommentId`, `Path`, `Author`, `AuthorAssociation`, `Body`, `CreatedAt`).
- `ListReviewCommentReactionsAsync(long commentId)` / `AddReviewCommentReactionAsync(long commentId, string reactionType)` → `/pulls/comments/{commentId}/reactions`.
- `ReplyToReviewCommentAsync(int prNumber, long commentId, string body)` → `POST /pulls/{prNumber}/comments` with `{ body, in_reply_to: commentId }`, which threads the reply under the triggering comment without needing a `commit_id`/`path`/`line` (those are only required when starting a new, non-reply review comment).
This mirrors the existing convention of one interface member per concrete endpoint (e.g. `CreateIssueCommentAsync` vs `WritePrCommentAsync` already exist separately despite both "posting a comment").

**2. Extend `EligibleUpdateComment` with `CommentKind Kind` and `string? FileName` rather than introducing a parallel `EligibleReviewComment` type.**
`ProcessCommentAsync` and its adoption/timeout/error/success helpers are ~150 lines of shared sequencing (branch refresh, template render, CLI session, commit/push, PR-description refresh) that must run identically regardless of comment source. A second near-duplicate type would fork that logic. Adding two fields lets one `EligibleUpdateComment` list represent both sources, with the handful of GitHub-facing calls (react, list-reactions, reply, state-store kind) branching on `Kind` at the point of the call. `FileName` is `null` for conversation comments and non-null for review comments.

**3. A new template file, `update-file.txt`, rather than composing a "File:" line in C#.**
`command-templates`'s existing rule is that CLI-agent prompt text lives entirely in `.txt` files, with only `{{token}}` substitution — no runtime string composition of the command text itself. Building a `"File: {name}"` fragment in C# and injecting it as a token value would be the one placeholder whose *value* is itself partially literal text, breaking that pattern for no benefit. A second template file (same standing unattended-run block, same `{{spec_name}}`/`{{instructions}}` tokens, plus `{{file_name}}`) keeps every prompt fully declarative, matches this repo's "one file per command variant" convention, and lets `UpdateWorkflowRunner` pick the template by name (`"update"` vs `"update-file"`) exactly as it already picks a fixed template name today.

**4. Reuse `trackedIssue.SpecName` and the existing adoption path unchanged.**
File-anchored comments resolve to a spec/change exactly the way conversation comments do today (tracked record lookup, falling back to `PrAdoptionService` for an untracked PR) — there is no new "which spec does this file belong to" concept; the file name is contextual information passed to the CLI agent, not a routing key.

## Risks / Trade-offs

- **[Risk]** Two near-identical GitHub call paths (issue-comment vs review-comment endpoints) for react/list-reactions/reply increase the surface `UpdateWorkflowRunner` must branch on. → **Mitigation**: confine the branching to small helper methods keyed on `comment.Kind`, keeping the outer sequencing (adoption → git sync → render → CLI agent → commit/push → report) identical for both.
- **[Risk]** A review comment's reply, if ever posted without `in_reply_to`, requires `commit_id`/`path`/`line` and could fail or land on the wrong diff position. → **Mitigation**: always reply via `in_reply_to`; never construct a fresh (non-reply) review comment from this workflow.
- **[Risk]** Forgetting to update the "already-reacted" skip check for the new endpoint would cause the scan to reprocess the same review comment every run. → **Mitigation**: cover this explicitly in the spec scenarios and tests, mirroring the existing conversation-comment coverage.

## Open Questions

- None — the existing conversation-comment code path fully determines the shape of this addition; explicit assumptions are recorded in `proposal.md`.
