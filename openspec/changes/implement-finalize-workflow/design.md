## Context

`propose-workflow`, `implement-workflow`, and `update-workflow` already
share one shape: scan open issues/PRs for an eligible trigger comment,
react `eyes`, do git work, run a CLI agent session, commit/push, then
report success (`+1`/`rocket`) or failure (`confused`) back on the
comment and in the SQLite-backed state store. `finalize-workflow` is a
fourth instance of that same shape, triggered by `/finalize` on a PR
instead of `/update`/`/implement`, so this design is mostly "which of the
two existing PR-comment templates does it follow, and where does it
diverge."

## Goals / Non-Goals

**Goals:**
- Reuse the exact scan/react/error-handling shape `update-workflow` and
  `implement-workflow` already established, rather than inventing a new
  one.
- Implement the one genuinely new piece of infrastructure this requires:
  a real `MarkPrReadyForReviewAsync`.

**Non-Goals:**
- No change to the state-store schema. `TrackedIssue` (issue number, PR
  number, spec name) and `TrackedComment` (id, kind, status) already
  carry everything `finalize-workflow` needs to record; there is no new
  fact about a finalized PR that the current shape can't express as a
  `CommentStatus.Done` comment under the existing tracked issue.
- No enforcement that `/finalize` only runs after `/implement`, or that a
  PR can only be finalized once. Re-running `/finalize` on an
  already-`+1`'d comment is already excluded by the shared "skip
  bot-reacted comments" rule; running it on a fresh comment against an
  already-archived change is left to `openspec archive` itself to reject
  or no-op.

## Decisions

**Branch refresh: reuse `update-workflow`'s fetch/switch/reset-hard
sequence, not `propose-workflow`'s create-branch sequence.** The request
describes this step as "clear all changes and move/create the feature
branch," which could be read as needing `IGitService.CreateBranchAsync`.
But `/finalize` only makes sense on a PR that already exists (opened by
`/propose`, refined by `/implement`/`/update`), so the branch already
exists on `origin` — this is the same situation `update-workflow` and
`implement-workflow` are in, not the one `propose-workflow` is in when it
creates `feature/{issue}` from scratch. Reusing `FetchAsync` →
`SwitchBranchAsync` → `ResetHardAsync("origin/{branch}")` satisfies
"clear all changes and move to the feature branch" and keeps one git
sequence for "operate on an existing PR" across all three PR-triggered
workflows instead of introducing a second.

**CLI agent prompt: natural language, not an `/opsx-*` command, matching
`update-workflow`.** `openspec archive` is a direct CLI invocation, not
one of the `opsx:*` skills, so the prompt instructs the agent in prose
(as `update-workflow` does for `/update`) rather than dispatching a slash
command (as `propose-workflow`/`implement-workflow` do). Prompt text:
```
Run `openspec archive "{spec-name}" --yes`. Mark missing tasks as
completed and continue.
{instructions}
```
where `{instructions}` is the triggering comment's body with the leading
`/finalize` token and its separating whitespace stripped (identical
stripping rule to `/update`/`/implement`), and the whole prompt is sent
as one value wrapped in escaped double quotes (`\"...\"`), matching the
existing prompt-quoting convention.

**Success reaction: `+1`, reusing the existing checkmark convention.**
`update-workflow`/`implement-workflow` already use `+1` as a checkmark
stand-in because the GitHub REST reaction set has no literal checkmark.
`finalize-workflow` reuses the same convention rather than introducing a
different "done" signal.

**Commit message: `"finalizing specs for #{issue-number}"`.** Follows the
existing per-workflow naming: `"adding specs for #N"` (propose),
`"applying specs for #N"` (implement), `"updating specs for #N"`
(update).

**`MarkPrReadyForReviewAsync` needs the GraphQL API, not REST.** GitHub's
REST API has no "convert from draft" endpoint; the only supported way is
the `markPullRequestReadyForReview` GraphQL mutation, which takes the
PR's GraphQL node id (`id` field on the REST PR payload, a base64-ish
opaque string), not its REST-visible PR number. The implementation SHALL
fetch the PR by number via REST first to obtain `node_id`, then POST that
node id to `https://api.github.com/graphql` with the mutation. This is
the only `IGitHubService` member that talks to the GraphQL endpoint
instead of REST; existing error-reporting conventions (typed
exception/result, not a raw `HttpRequestException`) still apply.

**Ready-for-review happens after push, before the success reaction.**
Ordering matches the existing "commit → push → report" sequence in
`update-workflow`, with mark-ready-for-review inserted between push and
the GitHub/state-store reporting step, since a failure to flip the PR out
of draft should surface as the same `confused`/error path as any other
step failing mid-cycle.

## Risks / Trade-offs

- [Risk] `MarkPrReadyForReviewAsync` fails on a PR that is already not a
  draft (GitHub's mutation may error on a non-draft PR) → Mitigation:
  treat this the same as any other step failure — it lands in the
  existing `catch (Exception ex)` path, reports `confused` +
  human-readable reply, and does not require special-casing since
  `openspec archive` reruns are already expected to be idempotent-ish.
- [Risk] The archive CLI agent run reports `Completed` but
  `openspec archive` itself failed inside the session (e.g. change
  already archived, validation error) → Mitigation: out of scope for
  this change, same as `update-workflow`/`implement-workflow` already
  trust `Completed` state as sufficient; a future change can tighten
  this for all four workflows at once if it becomes a real problem.

## Migration Plan

Additive: new `IFinalizeWorkflowRunner`/`FinalizeWorkflowRunner`, one new
`PollingLoop.RunAsync` scan pass, one new `IGitHubService` member gaining
a real body. No existing behavior changes for `/propose`, `/implement`,
or `/update`. No state-store migration.

## Open Questions

None outstanding — resolved the branch-refresh and prompt-format
questions above by following existing workflow precedent.
