## Context

`ProposeWorkflowRunner`, `ImplementWorkflowRunner`, `UpdateWorkflowRunner`,
and `FinalizeWorkflowRunner` each poll GitHub once per scan pass, find
comments whose trimmed body starts with their trigger token
(`/propose`, `/implement`, `/update`, `/finalize`), and process every
eligible comment: git operations against the bot's local clone, an AI CLI
agent session, and PR/issue writes, all authenticated as the bot's PAT.
`GitHubService.ReadPrCommentsAsync` / the internal `ListIssueCommentsAsync`
already parse `user.login` into `PrComment.Author` /
`GitHubIssueComment.Author`, but nothing downstream reads it — every commenter
is currently treated as trusted. GitHub's REST API returns
`author_association` (`OWNER`, `MEMBER`, `COLLABORATOR`, `CONTRIBUTOR`,
`FIRST_TIME_CONTRIBUTOR`, `FIRST_TIMER`, `NONE`, and a couple of rarer values)
on every comment payload already, at no extra request cost.

The four runners are structurally identical (same file layout: `RunOnceAsync`
builds an `eligibleComments` list, then a second loop skips comments already
reacted to by the bot before calling `ProcessCommentAsync`), so the fix is one
shared decision function invoked identically in all four places.

## Goals / Non-Goals

**Goals:**
- Ensure no git/CLI-agent/GitHub-write action is taken on behalf of a
  comment whose author is not authorized, across all four workflows.
- Make the authorization rule configurable per deployment (association
  allowlist + explicit username allowlist) without a code change.
- Keep the boundary invisible on GitHub: no reaction, no reply, only a
  server-side log line, so an unauthorized actor gets no signal about
  whether the bot even saw their comment.
- Reuse one implementation for all four runners rather than four copies of
  the same conditional.

**Non-Goals:**
- No change to *which* comment bodies count as eligible triggers (the
  `/propose`, `/implement`, `/update`, `/finalize` token-matching logic is
  unchanged).
- No new GitHub API calls — `author_association` rides along on the
  existing comment-listing responses.
- No UI/CLI surface for managing the allowlists beyond
  `SpecRunnerOptions` configuration binding (same mechanism as every other
  option today).
- No retroactive re-check of comments already marked with a bot reaction
  (`eyes`/`rocket`/`+1`/`confused`) from before this change ships — the
  existing "already handled" skip logic is unchanged and untouched by
  authorization.

## Decisions

### Decision: Authorization is `author_association` allowlist OR explicit username allowlist
Rationale: `author_association` (OWNER/MEMBER/COLLABORATOR by default) covers
the common case — anyone with real repo access — without maintaining a
username list. The explicit `AllowedTriggerUsers` allowlist covers trusted
non-collaborators (e.g. a bot account, an external maintainer who isn't a
repo collaborator) without having to grant them repo membership just to use
the bot. OR (not AND) because either signal alone is sufficient trust;
requiring both would make the explicit allowlist pointless for non-collaborators.

Alternatives considered:
- *Association-only, no allowlist*: simpler, but forces every legitimate
  trusted user to become a repo collaborator; rejected per explicit user
  decision to support an allowlist.
- *Allowlist-only, no association check*: would require manually listing
  every current and future collaborator; rejected as high-maintenance and
  error-prone (a new collaborator would be silently unauthorized until
  someone remembers to add them).

### Decision: A single static `CommentAuthorization.IsAuthorized` helper in `SpecRunner.Core`
Rationale: the four runners are otherwise-independent classes with no shared
base class; a static pure function (`(author, authorAssociation, options) ->
bool`) is the smallest shared surface, trivially unit-testable in isolation
from any runner, and avoids introducing a DI-registered service for what is
one string-comparison decision with no state or side effects.

Alternatives considered:
- *Injected `ICommentAuthorizationService`*: more idiomatic for a stateful
  dependency, but this has no state, no I/O, and no reason to be mocked in
  runner tests (the real logic should always run) — a static helper is
  simpler and just as testable.
- *Duplicate the check inline in each runner*: rejected — four copies of the
  same case-insensitive comparison logic is exactly the kind of duplication
  the proposal is trying to avoid by centralizing it once.

### Decision: Unauthorized comments are dropped before they enter `eligibleComments`, not inside `ProcessCommentAsync`
Rationale: filtering happens at the same point the trigger-token match
happens (in the `eligibleComments`-building loop), so an unauthorized
comment never gets an `eyes` reaction, never enters the per-comment
try/catch, and never reaches the state store — it behaves as if it were
never a trigger at all, only observable via a warning log. Filtering later
(inside `ProcessCommentAsync`) would require every runner to special-case an
early return that skips the `eyes` reaction it currently adds unconditionally
as its first action — filtering earlier is fewer touch points and cannot
regress the "eyes reaction is the first action taken" contract for
authorized comments in `propose-workflow`.

Alternatives considered:
- *Check inside `ProcessCommentAsync`, before the `eyes` reaction*: works,
  but means the reaction line has to move after a conditional in every
  runner and slightly complicates the "eyes reaction precedes any other
  work" existing requirement's scope (does authorization count as "other
  work"?). Filtering at list-build time sidesteps the ambiguity entirely.

### Decision: `Author`/`AuthorAssociation` are threaded through the `Eligible*Comment` records rather than re-fetched
Rationale: the comment's author/association are already available at the
point `EligibleProposeComment` etc. are constructed (from `GitHubIssueComment`
/ `PrComment`); threading them through means `CommentAuthorization.IsAuthorized`
can be called with data already in hand, and the same fields are available
inside `ProcessCommentAsync` for logging without a second GitHub call.

### Decision: Default `AllowedAuthorAssociations` is `["OWNER", "MEMBER", "COLLABORATOR"]`; default `AllowedTriggerUsers` is empty
Rationale: matches the exact default given in the proposal/user decision.
This preserves today's *intent* (repo insiders can trigger the bot) while
closing the gap for `CONTRIBUTOR`/`NONE`/etc., which today can also trigger
it but per the security concern should not be able to by default.

## Risks / Trade-offs

- **[Risk] Silent-ignore makes debugging "why didn't my `/propose` fire"
  harder for a legitimately-confused user with insufficient access.** →
  Mitigation: the warning log includes comment id, issue/PR number, and
  author, so an operator with log access can diagnose it; this is an
  explicit trade-off already made in the proposal (avoid revealing the
  boundary over avoiding operator friction).
- **[Risk] `author_association` is computed by GitHub relative to the
  *repository*, and its exact value can differ subtly from what a maintainer
  expects (e.g. an org owner who isn't a direct repo collaborator may show
  as `NONE` on some repos).** → Mitigation: the proposal's own verification
  step calls for manually sanity-checking `IsAuthorized` against GitHub's
  real association values before relying on it; `AllowedTriggerUsers` is the
  explicit escape hatch for exactly this case.
- **[Trade-off] Case-insensitive comparison for both author and association**
  avoids a class of "works for `Octocat` but not `octocat`" bugs, but means
  `AllowedTriggerUsers` cannot be used to distinguish two GitHub logins that
  differ only by case — acceptable since GitHub logins are already
  case-insensitive/unique at signup.

## Migration Plan

- Additive change: new fields default such that existing tests using an
  implicit/default `authorAssociation` of `"OWNER"` keep passing unmodified
  in behavior (per proposal's test-update note).
- No data migration — `AuthorAssociation` is derived fresh from each GitHub
  API response, not persisted in `IStateStore`.
- No feature flag: this is a security fix: rollout is "ship it," with the
  default allowlist chosen to match current legitimate usage
  (OWNER/MEMBER/COLLABORATOR) so no currently-authorized user is
  newly locked out on deploy.
- Rollback: revert the change; no persisted state depends on the new fields.

## Open Questions

- None outstanding — proposal decisions (association+allowlist OR logic,
  silent-ignore) resolve the ambiguities that would otherwise block
  implementation.
