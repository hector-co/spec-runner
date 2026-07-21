## Context

Prior changes ([solution-layout], [app-configuration], [repository-connection],
[state-store-schema], [cli-agent-execution]) stood up the project skeleton,
config, connection testing, state persistence shape, and the CLI-agent
process primitive, but `IGitService`/`IGitHubService` are still
`NotImplementedException` placeholders and nothing decides when to start a
CLI-agent session or reacts to a GitHub comment. This change closes that
loop for the single `/propose` trigger: an issue comment in, a draft PR
out, with the comment itself used as the visible progress indicator.

SpecRunner operates against exactly one local git clone at a time
(`SpecRunnerOptions.LocalRepositoryPath`) and one GitHub repository,
authenticated with a single PAT — there is no multi-tenant or concurrent-
clone concern to design around here.

## Goals / Non-Goals

**Goals:**
- One console invocation performs one scan pass over open issues, finds
  `/propose` comments the bot hasn't already reacted to, and processes each
  to completion (success, "already has a PR", or error) before exiting.
- Every processed comment ends with a reaction + reply that unambiguously
  tells a human (and a future scan pass) what happened, so re-running the
  console app is always safe — nothing is double-processed.
- Reuse `ICliAgentSessionFactory`/`ICliAgentSession` as-is to run the
  actual proposal generation; this change only decides what prompt to send
  and what to do with the result.
- `IGitService`/`IGitHubService` become real, directly testable
  implementations, not throwing placeholders.

**Non-Goals:**
- No continuous polling/daemon mode. Repeated invocation (cron, a
  scheduled GitHub Action, a manual re-run) is an external concern; this
  change only makes a single invocation do useful work.
- No handling of `/update`, `/implement`, `/archive`, or PR-level comments
  — only issue comments whose body starts with `/propose`.
- No concurrent processing of multiple matched comments within one scan
  pass — see Decisions.
- No retry/backoff policy beyond the existing `SpecRunnerOptions.TaskTimeout`
  bounding a single CLI-agent session.
- No database migration framework — see Decisions.

## Decisions

- **Reaction-based status protocol, not a separate "processed comments"
  list.** GitHub's reactions API only supports `+1`, `-1`, `laugh`,
  `confused`, `heart`, `hooray`, `rocket`, `eyes` — there is no native
  check-mark or error/X reaction. This design maps: `eyes` = working
  (in-progress), `rocket` = done (draft PR created, or "already has a PR"
  informational reply), `confused` = error/timeout. A comment already
  carrying any of these three reactions **from the authenticated bot
  identity** is skipped on the next scan, making re-runs idempotent
  without a side table. Alternative considered: track processed comment
  IDs only in the SQLite state store — rejected as the primary signal
  because it gives a human watching the issue no visible feedback, which
  the reaction protocol gets "for free" from the trigger surface itself.
  The state store is still updated (see below) for issue/PR/spec lookup,
  just not as the idempotency check.

- **Orchestration implemented directly in `SpecRunner.Console`, no new
  project.** `IProposeWorkflowRunner` is declared in `SpecRunner.Core` (so
  it's an interface, testable in isolation) but its implementation is
  pure composition of already-injected services (`IGitService`,
  `IGitHubService`, `IStateStore`, `ICliAgentSessionFactory`,
  `ISpecNameResolver`) with no external dependency of its own — it doesn't
  earn a dedicated project the way Git/GitHub/State/Cli did (each of those
  wraps a distinct external system or protocol).

- **Sequential processing of matched comments, not parallel.** All
  matched comments in one scan pass share the single local clone at
  `LocalRepositoryPath`; running two at once would mean two branch
  checkouts racing on the same working tree. Comments are processed one
  at a time, each getting its own branch/checkout/CLI-agent-session/PR
  cycle before the next starts. A future change could parallelize by
  giving each comment its own worktree, but nothing here requires that
  yet.

- **Hard reset to the base branch before every comment's branch
  creation.** `git fetch`/`pull` the base branch, then `git reset --hard`
  to it before creating `feature/{issue-number}`, guarantees each run
  starts from a clean, known state regardless of what a previous
  (possibly crashed or interrupted) run left behind, since the clone is a
  disposable working copy dedicated to SpecRunner rather than a
  developer's working directory.

- **"Already has an active PR" is decided from the state store, not a
  GitHub branch/PR search.** `IStateStore.FindByIssueNumberAsync` already
  associates an issue with an optional PR number; if that record exists
  and has a PR number, the workflow skips straight to posting the "already
  has an active Draft PR: #x" reply with the `rocket` (done) reaction
  instead of starting a new branch. Alternative considered: search GitHub
  for an open PR whose branch matches `feature/{issue-number}` — rejected
  as redundant, since the state store is updated at the point a PR is
  created and is already the source of truth for issue↔PR association per
  [state-store-schema].

- **CLI-agent prompt is exactly `/opsx-propose {spec-name}\n{issue
  description}`.** `{spec-name}` comes from `ISpecNameResolver` (issue
  number + sanitized title, already implemented); `{issue description}`
  is the issue body verbatim. The workflow does not parse or interpret
  the CLI agent's output beyond observing terminal session state
  (`Completed` vs `Failed`) — what the agent actually wrote to disk is
  trusted and simply `git add -A && git commit`ted.

- **Timeout wraps the whole per-comment cycle, not just the CLI-agent
  session.** `SpecRunnerOptions.TaskTimeout` bounds git operations, the
  CLI-agent session, and the PR creation call together for one comment;
  exceeding it cancels/stops whatever is in flight (session `StopAsync`
  if the agent was running) and records the `confused`/error outcome,
  matching the project context's "processing stops, error indicator
  recorded" description.

- **No database migrations; schema changes delete and recreate the SQLite
  file.** Per explicit product direction for this phase of the project,
  `IStateStore`'s SQLite implementation is not expected to preserve data
  across schema changes — if a future change needs a different shape, the
  database file is deleted and recreated rather than adding a migration
  framework. This change happens to need no schema change at all (the
  existing tracked-issue/tracked-comment shape already covers it), but the
  policy is recorded here since it governs how `SpecRunner.State` evolves
  going forward.

## Risks / Trade-offs

- [GitHub reactions have no dedicated "error" or "done" emoji, so `rocket`/
  `confused` are being repurposed as status signals] → Documented plainly
  in the `propose-workflow` spec and in the reply comment's text, so the
  reaction is never the *only* signal — the accompanying comment always
  spells out the outcome in words.
- [A crashed run could leave a branch checked out and mid-work in the
  local clone] → The next scan pass's hard-reset-before-branch step
  recovers regardless of what state a prior crash left behind; the
  triggering comment's `eyes` reaction, however, would remain without a
  terminal reaction until the issue is manually re-triggered or a future
  change adds a "resume/reconcile stuck `eyes`" pass — out of scope here.
- [Sequential-only processing means one slow/timing-out comment delays
  every other matched comment in the same scan pass] → Acceptable at
  current scale (one clone, one PAT, expected low volume of concurrent
  `/propose` triggers); revisit with per-comment worktrees if throughput
  becomes a problem.
- [Reaction-based idempotency depends on correctly identifying the bot's
  own reactions vs. a human's] → `github-operations` resolves and caches
  the authenticated identity (`GET /user` for the configured PAT) once
  per run and filters reactions to that login before deciding a comment
  is already handled.
