## Why

Any GitHub account able to comment on an open issue or PR in the target
repository can currently trigger `/propose`, `/implement`, `/update`, or
`/finalize` — each of which runs git operations, spins up an AI CLI agent
session, and creates/modifies PRs, all authenticated as the bot's PAT. None
of the four workflow runners check who posted the triggering comment before
acting on it, even though GitHub already returns the comment author and
`author_association` on every comment payload. This closes that gap by
authorizing the triggering comment's author before any git/AI/PR action is
taken.

## What Changes

- `GitHubService.ReadPrCommentsAsync` and `ListIssueCommentsAsync` parse and
  surface each comment's `author_association` (defaulting to `"NONE"` when
  absent), alongside the existing `author` field.
- `PrComment` and `GitHubIssueComment` gain an `AuthorAssociation` field.
- `EligibleProposeComment`, `EligibleImplementComment`,
  `EligibleUpdateComment`, and `EligibleFinalizeComment` gain `Author` and
  `AuthorAssociation` fields, threaded through from the triggering comment.
- `SpecRunnerOptions` gains two new config knobs: `AllowedAuthorAssociations`
  (default `OWNER`, `MEMBER`, `COLLABORATOR`) and `AllowedTriggerUsers`
  (default empty), the latter an explicit allowlist for trusted
  non-collaborators.
- A new shared `CommentAuthorization.IsAuthorized(author, authorAssociation,
  options)` helper (`SpecRunner.Core`) centralizes the authorization
  decision: authorized if the author case-insensitively matches
  `AllowedTriggerUsers`, OR the association case-insensitively matches
  `AllowedAuthorAssociations`.
- Each of the four workflow runners calls this helper when filtering
  eligible comments. An otherwise-eligible comment from an unauthorized
  author is silently skipped: a server-side warning log is emitted (comment
  id, issue/PR number, author) but no reaction is added and no reply is
  posted, so the authorization boundary is never revealed on GitHub and
  threads are never spammed. Authorized comments proceed exactly as they do
  today.

## Capabilities

### New Capabilities
- `comment-authorization`: defines the shared `CommentAuthorization`
  decision (association allowlist plus explicit username allowlist,
  case-insensitive) used by all four comment-triggered workflows to decide
  whether a triggering comment's author may cause the bot to act.

### Modified Capabilities
- `github-operations`: `ReadPrCommentsAsync` and the internal
  `ListIssueCommentsAsync` now also surface each comment's
  `author_association`.
- `app-configuration`: `SpecRunnerOptions` gains `AllowedAuthorAssociations`
  and `AllowedTriggerUsers` configuration surfaces.
- `propose-workflow`: an eligible `/propose` comment is now additionally
  checked against `CommentAuthorization.IsAuthorized` before any reaction,
  git, CLI-agent, or GitHub write happens for it; unauthorized comments are
  silently skipped (warning log only).
- `implement-workflow`: same authorization check added for `/implement`
  comments before adoption/git/CLI-agent/GitHub actions.
- `update-workflow`: same authorization check added for `/update` comments.
- `finalize-workflow`: same authorization check added for `/finalize`
  comments.

## Impact

- `SpecRunner/src/SpecRunner.GitHub/GitHubService.cs`
- `SpecRunner/src/SpecRunner.Core/Models/PrComment.cs`,
  `GitHubIssueComment.cs`, `EligibleProposeComment.cs`,
  `EligibleImplementComment.cs`, `EligibleUpdateComment.cs`,
  `EligibleFinalizeComment.cs`
- `SpecRunner/src/SpecRunner.Core/Configuration/SpecRunnerOptions.cs`
- New file: `SpecRunner/src/SpecRunner.Core/CommentAuthorization.cs`
- `SpecRunner/src/SpecRunner.Console/ProposeWorkflowRunner.cs`,
  `ImplementWorkflowRunner.cs`, `UpdateWorkflowRunner.cs`,
  `FinalizeWorkflowRunner.cs`
- `SpecRunner/tests/SpecRunner.Tests/` — new `CommentAuthorizationTests.cs`
  plus updates to the four `*WorkflowRunnerTests.cs` files and any fakes in
  `SpecRunner.Tests/Fakes/` that construct `PrComment`/`GitHubIssueComment`.
- No breaking change to public behavior for already-authorized authors
  (OWNER/MEMBER/COLLABORATOR); this is an additive restriction on who can
  trigger bot actions.

## Assumptions

- The four workflow-runner spec deltas each add one authorization
  requirement rather than restating the whole existing spec; this proposal
  treats that as a modified (not replaced) capability per file.
- `author_association` values not in the default allowlist (`CONTRIBUTOR`,
  `FIRST_TIME_CONTRIBUTOR`, `FIRST_TIMER`, `NONE`) are unauthorized unless
  the author is explicitly allowlisted — this matches the "OWNER/MEMBER/
  COLLABORATOR plus optional allowlist" decision already given.
- No new capability is introduced for the authorization *enforcement point*
  inside each runner (it's a delta on the four existing workflow specs);
  only the shared decision logic (`comment-authorization`) is new.
