## Context

Today `SpecRunner.Console`'s `PollingLoop` runs a single sequential loop: propose →
implement → update → finalize → sleep `PollingInterval` (default 10s) → repeat. Each
runner's `RunOnceAsync` lists its eligible comments once, then processes them
strictly one at a time (`ProcessCommentAsync`), each wrapped in a `timeoutCts` linked
to the top-level shutdown token and cancelled after `SpecRunnerOptions.TaskTimeout`
(see `ProposeWorkflowRunner.cs:116-117,230-238` for the established pattern, repeated
in the other three runners). A `CliAgentSession` can run for a long time (it drives
an LLM coding agent), and while it does, the whole polling loop is blocked awaiting
that one comment — no other scan happens until it returns.

`/cancel` needs to interrupt exactly that blocked state. That means it cannot be a
fifth step in the same sequential loop (it would never be scanned until the very
thing it needs to stop has already finished on its own). It must run concurrently
with the existing loop, and it needs a way to reach into whichever runner is
currently mid-flight to stop its CLI-agent session and unblock its awaits.

There is exactly one local git clone (`SpecRunnerOptions.LocalRepositoryPath`),
shared by every runner. Only one comment is ever being processed at a time today
(propose/implement/update/finalize never run concurrently with each other); `/cancel`
introduces the first real concurrency in the process, so the design has to preserve
"only one thing touches the clone at a time" without restructuring the rest of the
loop into something concurrent.

## Goals / Non-Goals

**Goals:**
- Stop, promptly and reliably, whichever runner is currently processing the
  `/cancel` comment's issue or PR — including killing its in-flight CLI-agent child
  process.
- Discard uncommitted/untracked changes left behind (`git reset --hard`) once it is
  safe to touch the clone, and confirm this on GitHub.
- Keep the change additive and mechanical: reuse the existing per-runner
  `timeoutCts` pattern rather than inventing a new cancellation plumbing model.
- Preserve "single writer to the clone at a time" as an invariant.

**Non-Goals:**
- Supporting `/cancel` as a file-anchored PR review comment (only issue comments and
  PR conversation comments are scanned, per the proposal's assumptions).
- Making the main propose/implement/update/finalize loop itself concurrent — only
  the cancel scan runs alongside it.
- Recovering a run after a process restart (the active-run registry is in-memory
  and process-local; if SpecRunner itself is restarted, there is nothing left to
  cancel, and the next workflow run's existing reset-at-start behavior handles
  clone cleanliness as it does today).
- Cancelling `TaskTimeout`-driven stops (that path is unchanged; `/cancel` and
  `TaskTimeout` are two distinct triggers feeding the same underlying
  stop-the-session mechanism).

## Decisions

### An in-memory `IActiveRunRegistry`, not a state-store column

**Decision:** Add `SpecRunner.Core.Abstractions.IActiveRunRegistry` with
`Register(RunKey key, ActiveRun run)`, `Deregister(RunKey key)`, and
`TryGet(RunKey key, out ActiveRun run)`/`IsAnyActive()` members, backed by a single
thread-safe in-memory implementation (`ConcurrentDictionary`) in `SpecRunner.Console`,
registered as a singleton. `RunKey` is a small struct discriminating `Issue(int)` from
`Pr(int)`, matching the existing issue-number-vs-PR-number split used throughout the
state store. `ActiveRun` carries the `CancellationTokenSource` that scopes that
comment's processing (the same one already created as `timeoutCts` today), the
`ICliAgentSession` once one has been started (nullable until then), and a `Task`
that completes when `ProcessCommentAsync` returns (so a canceller can await it).

**Rationale:** The registry only needs to answer "is issue/PR X currently being
processed, and if so, what do I call to stop it" — a live, in-process handle, not
persisted state. Persisting it in SQLite would require a new table, a cleanup story
for crashed processes, and would still need an in-memory session handle anyway
(you can't resume a stopped child process from a database row). An in-memory
registry is the smallest thing that works, and losing it on restart is fine because
there's nothing to cancel across a restart.

**Alternatives considered:** A shared `CancellationTokenSource` per issue/PR stored
directly in `IStateStore`'s existing tables — rejected because `TrackedIssue`/
`TrackedComment` are meant to be durable, serializable records, and a live
`CancellationTokenSource`/`ICliAgentSession` can't be serialized into them without
splitting the concern anyway.

### Each existing runner registers/deregisters around its existing `timeoutCts`

**Decision:** In each of the four runners, immediately after creating `timeoutCts`
in `ProcessCommentAsync`, call `_activeRunRegistry.Register(key, new ActiveRun(timeoutCts, session: null, task: /* current comment's Task */))`, updating the
registered `ActiveRun`'s session reference once `session = _cliAgentSessionFactory.CreateSession()` is assigned. Deregister in the existing `finally` block, after
`session.DisposeAsync()`.

**Rationale:** This reuses the exact `CancellationTokenSource` that already governs
the per-comment operation (used for `TaskTimeout`), so requesting cancellation
externally needs no new plumbing inside the awaited operations — every `await` in
`ProcessCommentAsync` already observes `timeoutCts.Token`. Only the *outer*
`CancellationTokenSource` that feeds `timeoutCts` needs to grow a second parent.

### A three-way linked token distinguishes cancel from timeout from shutdown

**Decision:** Change `timeoutCts` construction from linking only the app shutdown
token to additionally linking a new externally-triggered `CancellationTokenSource`
obtained from (or registered into) the active-run registry at the start of
`ProcessCommentAsync`:
```
using var cancelRequestCts = new CancellationTokenSource();
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancelRequestCts.Token);
timeoutCts.CancelAfter(_options.TaskTimeout);
```
`cancelRequestCts` (not `timeoutCts` itself) is what gets registered in the active-run
registry and is what `CancelWorkflowRunner` cancels. The existing catch clause:
```
catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
```
splits into two arms — one checking `cancelRequestCts.IsCancellationRequested` (external
cancel: stop the session, deregister, return without posting any report — the cancel
workflow owns reporting) and the existing one for a genuine `TaskTimeout` expiry
(unchanged behavior).

**Rationale:** The existing timeout-catch code already does exactly what `/cancel`
needs (`session.StopAsync()` then a report), so the only new piece of logic is
telling the two triggers apart so the right report gets posted exactly once. This is
a small, mechanical, four-times-repeated change, consistent with how this codebase
already repeats per-runner logic rather than sharing a base class.

**Alternatives considered:** Introducing a shared base class or extension method for
the per-comment processing skeleton — rejected as out of scope; refactoring four
runners' control flow into a shared abstraction is a much larger, riskier change than
this feature needs, and the codebase's existing convention is duplication over a
premature shared abstraction.

### `CancelWorkflowRunner` owns the git reset and all reporting for a cancelled run

**Decision:** `CancelWorkflowRunner.ProcessCommentAsync` (for an eligible, authorized
`/cancel` comment):
1. `AddCommentReactionAsync`/`WritePrCommentAsync`-appropriate `eyes` reaction on the
   `/cancel` comment itself (start indicator).
2. Resolve `RunKey` from the comment's issue or PR number and call
   `_activeRunRegistry.TryGet`.
3. If found: call `run.CancelRequestCts.Cancel()`, then `run.Session?.StopAsync()`
   (if a session had been assigned) to kill the child process immediately rather
   than waiting for the cooperative `OperationCanceledException` to unwind, then
   `await run.CompletionTask` with a bounded grace period (e.g. 30s) so the reset
   below never races the victim's own in-flight git call.
4. Whether a run was found or not: only proceed to `IGitService.ResetHardAsync("HEAD")`
   if, after step 3, `_activeRunRegistry.IsAnyActive()` is false (i.e. nothing else is
   using the clone) — this covers both "we just stopped the only active run" and "no
   run was active, but the clone might be dirty from a crash." If some other run is
   active (a different issue/PR), skip the reset and report that nothing was running
   for *this* issue/PR.
5. Report on GitHub: a completion reaction on the `/cancel` comment, a reply
   confirming cancellation (or confirming there was nothing to cancel), and a state
   store upsert setting the *original triggering comment's* status to
   `CommentStatus.Canceled` (looked up via the tracked record, if one exists) —
   without needing a reply on the original comment as well, since the `/cancel`
   comment's own reply is the user-visible confirmation.

**Rationale:** Centralizing both the reset and the reporting in one place avoids two
different code paths writing to GitHub/the state store for the same outcome (the
victim's own catch arm intentionally does neither once it detects external
cancellation, so there's a single owner). Waiting for `CompletionTask` before
resetting preserves the single-writer-to-the-clone invariant without adding locking
primitives beyond what the registry already provides.

**Alternatives considered:** Having the victim's own catch arm perform the reset and
reporting for a cancel (like it already does for timeout) — rejected because the
victim's thread could be anywhere in its cleanup by the time it observes
cancellation, and letting *two* independent callers (victim and canceller) both
decide whether/when to touch git reintroduces the race this design exists to avoid.

### The cancel scan runs on its own concurrent loop, reusing `PollingInterval`

**Decision:** `Program.cs` starts a second loop (`CancelPollingLoop.RunAsync` or
inlined) alongside the existing `PollingLoop.RunAsync`, both `await`ed together
(e.g. `Task.WhenAll`), both driven by `options.PollingInterval` and the same shutdown
token. The cancel loop only calls `cancelWorkflowRunner.RunOnceAsync` and sleeps; it
does not touch the other four runners.

**Rationale:** No new configuration surface is needed — `/cancel` responsiveness at
the existing 10s default is already fast relative to a multi-minute CLI-agent
session. Running it as a sibling loop (rather than restructuring `PollingLoop` into
something generically concurrent) keeps the blast radius to "one new loop," not "the
whole polling model changed."

**Alternatives considered:** A single loop that `Task.Run`s all five runners
concurrently every tick — rejected: propose/implement/update/finalize still need to
run sequentially relative to each other (they already share the clone in a way the
existing spec depends on: "all comments in a scan pass share the same local clone"),
so only carving out the one runner that must be concurrent (cancel) minimizes change.

## Risks / Trade-offs

- **[Risk]** A victim runner stuck in an uncancellable native call (e.g. a hung git
  subprocess) never completes `CompletionTask`, so the reset waits out its full grace
  period every time. → **Mitigation**: after the grace period elapses, log a warning
  and perform the reset anyway (best effort) rather than blocking `/cancel` forever;
  killing the CLI-agent session's child process (step 3) already resolves the most
  likely cause of a long hang.
- **[Risk]** Two `/cancel` comments posted in quick succession for the same issue/PR
  could both observe `TryGet` succeeding before either deregisters. → **Mitigation**:
  the second `/cancel` comment is still only processed after the first has completed
  reacting to it (each `/cancel` scan pass processes its own eligible comments
  sequentially, same as every other workflow), and cancelling an already-cancelled
  `CancellationTokenSource` is a no-op, so this degrades to "reports nothing was
  running" rather than double-resetting.
- **[Risk]** Adding a third linked token to four existing runners touches
  well-tested, currently-passing code paths (timeout handling). → **Mitigation**:
  the existing timeout scenarios are unchanged in behavior (only the `when` guard's
  ordering gains one more check); existing tests for timeout behavior should continue
  to pass unmodified, and new tests cover the added cancel-vs-timeout branch
  specifically.
- **[Trade-off]** `/cancel` cannot interrupt a run within the same instant it's
  posted — it waits for the next cancel-scan tick (`PollingInterval`, default 10s).
  Accepted as adequate given the loop's existing polling cadence.

## Migration Plan

No data migration. `CommentStatus.Canceled` is a new enum member persisted by name
(`TEXT` column via `.ToString()`/`Enum.Parse`), so existing rows and existing values
are unaffected. Deploying this change requires no downtime beyond the normal restart
to pick up the new build; there is nothing to backfill since the active-run registry
starts empty on every process start.

## Open Questions

- Should `/cancel` also work as a file-anchored PR review comment (mirroring
  `/update`'s dual-source scanning)? Assumed out of scope for this change (see
  proposal Assumptions) since cancellation is a control action, not tied to a file.
- Should the grace period for awaiting `CompletionTask` before a best-effort reset be
  configurable, or is a fixed constant (e.g. 30s) sufficient? Proposed as a fixed
  constant for now; can be promoted to configuration later if it proves too short in
  practice.
