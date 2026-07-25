## Context

`ProposeWorkflowRunner`, `ImplementWorkflowRunner`, `UpdateWorkflowRunner`,
and `FinalizeWorkflowRunner` each implement `RunOnceAsync` /
`ProcessCommentAsync` with the same shape: react to the triggering comment,
resolve tracked state, run a fixed sequence of steps (git sync, prompt
render, CLI agent session, git commit/push, GitHub updates), then report
success/timeout/error. Today, `ILogger<T>` is only used for the
Warning-level timeout message and the Error-level failure message inside
each runner; there is no Information- or Debug-level signal for a healthy
run in progress. `PollingLoop` already logs an Error per workflow scan pass
that throws, but nothing marks a scan pass or step flow starting.

The CLI agent session (`ICliAgentSession` / `ClaudeCliAgentSession`) is the
one step whose duration is unbounded except by `_options.TaskTimeout`
(`SpecRunnerOptions.TaskTimeout`) — every other step (git command, GitHub
API call) is a single bounded async call. `ProcessCommentAsync` in all four
runners awaits it the same way:

```csharp
session = _cliAgentSessionFactory.CreateSession();
await session.StartAsync(...).ConfigureAwait(false);
await session.CloseInputAsync(...).ConfigureAwait(false);

await foreach (var _ in session.ReadEventsAsync(timeoutCts.Token).ConfigureAwait(false))
{
    // Drain events; the channel completes once the session reaches a terminal state.
}
```

## Goals / Non-Goals

**Goals:**
- Give operators enough log signal to answer, from logs alone: "which
  issue/PR is being processed right now, and is it still alive?"
- Add Debug-level start/finish detail for each step in a step flow, useful
  when diagnosing a stuck or misbehaving run without raising the default
  Information-level noise floor.
- Keep the change additive and mechanical: no change to control flow, error
  handling, retry behavior, or existing Warning/Error logging.

**Non-Goals:**
- No new configuration surface (no configurable indicator interval; 5
  seconds is fixed per the request).
- No structured/metrics-based progress reporting (e.g. OpenTelemetry
  spans) — this is plain Serilog logging only, consistent with
  `structured-logging`.
- No change to `PollingLoop`'s existing per-workflow Error logging.

## Decisions

### 1. "Step flow" = one `ProcessCommentAsync` invocation
Each eligible comment already carries an issue number (propose) or PR
number (implement/update/finalize) and drives one pass through the full
step sequence. This is the natural unit for an Information-level
"starting" log: one line per flow invocation, logged at the very top of
`ProcessCommentAsync`, e.g.
`logger.LogInformation("Starting /propose flow for issue #{IssueNumber} (comment {CommentId})", ...)`
and the PR-keyed equivalent in the other three runners.

Alternative considered: log at `RunOnceAsync` (per scan pass) instead.
Rejected — a scan pass can cover zero, one, or many eligible comments, so
it doesn't map to "the issue or PR number" the way one flow invocation
does.

### 2. Shared periodic "still in progress" helper for the CLI agent wait
Add one small static helper, `ProgressIndicator` (in
`SpecRunner.Console`), that runs alongside the `ReadEventsAsync` drain loop
and logs `LogInformation("... still in progress ({Elapsed} elapsed)")`
every 5 seconds until the drain loop completes or the flow's
`timeoutCts.Token` fires. Implementation shape:

```csharp
internal static class ProgressIndicator
{
    public static async Task RunAsync(ILogger logger, string message, CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                logger.LogInformation(message);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected once the awaited work completes and the linked token is cancelled.
        }
    }
}
```

Each runner starts this as a fire-and-forget `Task` (linked to a
`CancellationTokenSource` chained off `timeoutCts.Token`) immediately
before the `await foreach (... ReadEventsAsync ...)` loop, and cancels +
awaits it in a `finally` right after the loop exits — mirroring the
existing `session` disposal pattern already in each `ProcessCommentAsync`.

Alternative considered: a `System.Timers.Timer` or
`PeriodicTimer`. Rejected in favor of a plain cancellable delay loop —
`PeriodicTimer` needs .NET's `WaitForNextTickAsync`, which is equivalent in
behavior here but the delay-loop form is more obviously symmetric with the
rest of the codebase's `Task.Delay`-based polling in `PollingLoop`.

Alternative considered: only start the indicator around the whole
`ProcessCommentAsync`, not scoped to the CLI agent wait. Rejected per
proposal Assumptions — other steps are already bounded and fast; ticking
during them would be misleading ("still in progress" implying the CLI
agent is running when it isn't).

### 3. Debug-level start/finish messages wrap each existing step call
For each of the existing await-ed step calls inside `ProcessCommentAsync`
(git reset/fetch/switch/pull, prompt render, session start/close, commit,
push, tasks file read, GitHub description/title/ready-for-review calls),
add a `LogDebug` immediately before ("Starting <step>...") and immediately
after ("Finished <step>") the call, using the same message-template style
already used for Warning/Error. These are pure logging additions — no
`try/finally` needed since the surrounding `ProcessCommentAsync` `try`
block already routes exceptions to `ReportErrorAsync`/`ReportTimeoutAsync`,
and a step that throws simply never logs its own "Finished" line (which is
useful signal, not a bug).

Alternative considered: a reusable "step" wrapper
(`await LogStepAsync(name, () => _git.PullAsync(...))`) to avoid repeating
the before/after pattern. Rejected — the four runners' steps differ enough
in signature (some return values, some don't; some are used in later
steps) that a generic wrapper adds indirection without saving much, and the
task explicitly asks to avoid overlap with Information-level messages, which
is easier to eyeball with inline calls than through a wrapper.

## Risks / Trade-offs

- [Risk] Fire-and-forget progress task could out-live its scope if not
  cancelled/awaited correctly → Mitigation: cancel via a token linked to
  `timeoutCts.Token` and always await the task in the same `finally` block
  that already disposes `session`, so a flow can't return while the
  indicator is still ticking.
- [Risk] Debug-level noise roughly doubles the number of log lines per
  step flow → Mitigation: Debug is off by default in typical Serilog
  minimum-level configuration; this only affects operators who explicitly
  raise the level, which is the intent.
- [Risk] Duplicating the progress-indicator start/stop code four times
  (once per runner) → Mitigation: the periodic-logging mechanism itself is
  shared via `ProgressIndicator.RunAsync`; only the 2-line start/stop
  call site is repeated, consistent with how `session` lifecycle is
  already handled per-runner.

## Migration Plan

No migration required. Purely additive logging behind existing Serilog
configuration; deploying is just shipping the new build. No rollback
concerns beyond a normal revert.
