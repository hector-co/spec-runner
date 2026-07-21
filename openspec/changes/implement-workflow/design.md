## Context

`ProposeWorkflowRunner` (`SpecRunner.Console/ProposeWorkflowRunner.cs`) is
the one complete workflow today: each `PollingLoop.RunAsync` cycle calls
its `RunOnceAsync`, which lists open issues with comments via
`IGitHubService.ListOpenIssuesWithCommentsAsync`, filters to comments
matching a hardcoded `/propose` trigger, skips any already carrying a bot
`eyes`/`rocket`/`confused` reaction, and processes the rest sequentially:
react `eyes` → check `IStateStore` for an existing PR → reset the clone to
`BaseBranchName` → create a fresh `feature/{issue}` branch → run the CLI
agent with `/opsx-propose {spec}\n{body}` → commit/push/open a draft PR →
react `rocket` + reply + upsert state, or react `confused` + reply + upsert
state on error/timeout.

`/implement` needs the same shape, but every step that assumes "starting
fresh from an issue" has to change to "continuing an already-open PR":
comments live on PRs (`IGitHubService.ReadPrCommentsAsync`/
`WritePrCommentAsync` are still `NotImplementedException` stubs, and there
is no "list open PRs" operation at all), the spec name has to come from a
prior `/propose` run's state-store record rather than being derived from
an issue title, and the git branch to work on already exists on `origin`
and must be refreshed in place rather than created from `BaseBranchName`.

`IGitHubService`'s issue-comment endpoints (list/create comments, list/add
reactions) are already the correct GitHub REST endpoints for a PR's
conversation-tab comments too — a PR is an issue under the hood, and its
number is the same number both APIs use — so `/implement` can reuse
`AddCommentReactionAsync`, `ListCommentReactionsAsync`, and
`CreateIssueCommentAsync` unchanged; only *listing open PRs* and
*reading/writing a PR's own comments* are missing operations.

## Goals / Non-Goals

**Goals:**
- A `/implement` comment on an open PR drives the CLI coding agent against
  that PR's existing branch and reports the outcome back on the comment,
  with the same react-first/skip-if-already-reacted/sequential-processing
  guarantees `/propose` already provides.
- The spec/change name acted on is resolved from the state store record
  created when the PR was opened (via `/propose`), not re-derived or
  guessed from the PR itself.
- Both workflows keep sharing one local clone safely — they never run
  concurrently.

**Non-Goals:**
- No `/update`-style handling of PR comments that aren't a `/implement`
  trigger.
- No `MarkPrReadyForReviewAsync` call — `/implement` only pushes commits
  to the existing (draft) PR; whether/when a PR leaves draft state is a
  separate concern.
- No PR *review* comment (inline code comment) support — only general PR
  conversation comments, matching what `ReadPrCommentsAsync`/
  `WritePrCommentAsync` were already scoped to.
- No generalized multi-workflow runner list/registry — see the polling
  loop decision below.

## Decisions

- **A comment on an untracked PR is a reported error, not a guessed spec
  name or a crash.** Unlike `/propose`, which always has an issue number
  and title to derive a spec name from, `/implement` only has the PR. If
  `IStateStore.FindByPrNumberAsync` returns nothing (the PR wasn't opened
  by `/propose`, or its record was lost), there is no reliable spec name
  to hand the CLI agent. The workflow reacts `confused` and replies
  explaining no associated spec/change was found, and skips the git/CLI
  steps entirely, without writing to the state store (there's no issue
  number to key a `TrackedComment` under). Reprocessing is still
  prevented because the `confused` reaction itself is what the next scan
  pass checks against — the same mechanism `/propose` already relies on
  for its own skip logic, not a state-store flag.

- **The bot's own-reaction skip list extends `/propose`'s to `{eyes, +1,
  confused}`** (replacing `rocket` with `+1`, the reaction chosen for
  `/implement`'s success signal — GitHub's reaction set has no literal
  checkmark). Skipping already-`confused` comments too (not just the
  literal "eyes or check" the proposal names) mirrors `/propose`'s
  existing convention and avoids an error comment being silently retried
  forever on every poll cycle; the alternative — only skipping `eyes`/`+1`
  — would mean a permanently-failing `/implement` comment (e.g. a typo'd
  spec reference) gets reprocessed, timed out, and re-reported on every
  single poll indefinitely.

- **Refreshing the PR's branch is a new `IGitService.FetchAsync(branchName)`
  primitive composed with the existing `SwitchBranchAsync` and
  `ResetHardAsync`, rather than a new bundled "pull PR branch" method or a
  change to `PullAsync`'s signature.** `PullAsync` today is hardcoded to
  fast-forward `BaseBranchName` specifically and `/propose` still needs
  exactly that. `/implement` needs the equivalent for an arbitrary branch
  name determined at runtime (the PR's head branch, from the new
  list-open-PRs operation): `FetchAsync(branch)` (`git fetch origin
  {branch}`) → `SwitchBranchAsync(branch)` → `ResetHardAsync("origin/{branch}")`.
  This keeps `IGitService`'s existing methods each a thin single-purpose
  wrapper (matching every other member) instead of overloading `PullAsync`
  with a second, differently-shaped calling convention.

- **`ListOpenPullRequestsAsync` returns PR number/title/body/head-branch
  only; comments are still fetched per-PR via the already-declared
  `ReadPrCommentsAsync`**, mirrored after `/propose`'s
  `ListOpenIssuesWithCommentsAsync` shape but split in two rather than
  bundled. `ReadPrCommentsAsync(int prNumber)` already exists on
  `IGitHubService` as a stub reserved for exactly this kind of use — reusing
  it (finally implementing it for real) avoids introducing a second,
  overlapping "list PRs with comments" method alongside it.

- **Both workflows run sequentially within one poll cycle, not as
  separate concurrent loops.** `propose-workflow`'s own spec already
  requires its comments be processed one at a time "since all comments in
  a scan pass share the same local clone" — that constraint applies
  equally between workflows, not just within one. `PollingLoop.RunAsync`
  is extended to take both `IProposeWorkflowRunner` and the new
  `IImplementWorkflowRunner`, calling `propose-workflow`'s
  `RunOnceAsync()` then `implement-workflow`'s `RunOnceAsync()` in order
  each cycle, each independently wrapped in its own try/catch so one
  workflow's unhandled exception doesn't prevent the other from running
  that cycle. A generic `IReadOnlyList<IWorkflowRunner>` abstraction was
  considered and rejected as premature for two callers — revisit if a
  third workflow (e.g. `/archive`) needs the same wiring.

- **`TrackedPr` is deleted, not reused.** It's an unused record
  (`PrNumber`, `IssueNumber`) with no backing table and no `IStateStore`
  member — `TrackedIssue.PrNumber` plus the existing
  `FindByPrNumberAsync` already provide PR-keyed lookup, which is all
  `/implement` needs. Since the project is pre-release, removing dead code
  is preferred over keeping an unused model "just in case."

- **Commit message is `"implementing #{issue-number}"`,
  keyed off the issue number (not the PR number) recorded in the resolved
  `TrackedIssue`**, for consistency with `/propose`'s existing
  `"adding specs for #{issue-number}"` convention — both commands' commit
  history reads against the same canonical identifier.

## Risks / Trade-offs

- [An untracked PR's `/implement` comment gets a `confused` reaction and
  no state-store trace, which could look like a silent failure to
  someone auditing the store rather than GitHub] → The GitHub reply
  comment already states the reason in full; the state store was never
  the source of truth for comment outcomes (reactions are), consistent
  with `/propose`'s existing dedupe mechanism.
- [Running `implement-workflow` right after `propose-workflow` every
  cycle means an `/implement` comment waits for the full `propose-workflow`
  scan to finish first, even when there are no eligible `/propose`
  comments] → Acceptable at current scale: an empty `propose-workflow`
  scan is one GitHub list call, not a full per-comment cycle: negligible
  added latency compared to `PollingInterval`.
- [`ResetHardAsync("origin/{branch}")` discards any local commits on that
  branch not yet pushed to `origin`] → Intentional and consistent with
  `/propose`'s existing reset-before-work behavior: the local clone is
  disposable, `origin` is the source of truth, and every workflow always
  starts a comment's processing from a known-clean state matching the
  remote.
