## Context

`SpecRunner.Console`'s `Program.cs` currently runs a connection test,
then exactly one `IProposeWorkflowRunner.RunOnceAsync()` scan pass, then
exits (0 on success, non-zero on a failed connection or an unhandled
exception from the scan pass). `RunOnceAsync` already handles per-comment
failures internally (reaction + reply + state-store update, then it moves
on to the next comment) — it only throws out of `RunOnceAsync` itself for
something unexpected at the scan level (e.g. the GitHub issue-listing call
failing outright). Nothing currently repeats the pass or keeps the process
alive.

Separately, `ClaudeCliAgentSession.StartAsync` launches the Claude CLI with
a fixed set of flags (`--print --verbose --input-format stream-json
--output-format stream-json`) and never signals end-of-input; the process's
stdin stays open until `StopAsync` closes it (which also kills the process
after a grace period). `ProposeWorkflowRunner` only ever sends one prompt
per session and then drains events to a terminal state — it never sends a
follow-up turn.

## Goals / Non-Goals

**Goals:**
- `SpecRunner.Console` runs indefinitely, executing a `propose-workflow`
  scan pass, waiting `PollingInterval`, and repeating, until it receives
  Ctrl+C (SIGINT) or SIGTERM.
- A scan pass that throws no longer takes the whole process down — it's
  logged and the loop continues on the next interval, since "always
  running" is the point.
- The Claude CLI subprocess never blocks on a permission prompt with no
  one present to answer it.
- A one-shot caller (like `ProposeWorkflowRunner`) can tell a running CLI
  session "no more input is coming" without force-killing it, so a
  well-behaved CLI process can notice stdin closed and exit on its own
  once it's done responding, instead of always being torn down by
  `StopAsync`'s grace-period-then-kill path.

**Non-Goals:**
- No backoff/jitter on `PollingInterval` — a fixed delay between passes is
  sufficient at current scale.
- No conversion to a Windows Service / systemd unit / `BackgroundService`
  — the process itself becomes long-running; wrapping it in a service
  manager for restart-on-crash or boot-start is a deployment concern, not
  addressed here.
- No change to how an individual comment's timeout/error is handled
  inside one scan pass — that's unchanged.
- No change to `ICliAgentSession.StopAsync`'s kill semantics — the new
  input-close member is an additional, softer option alongside it, not a
  replacement.

## Decisions

- **Polling loop lives directly in `Program.cs`, not a new
  `BackgroundService`/hosted service.** `Program.cs` already resolves
  `IProposeWorkflowRunner` from `host.Services` and calls it once; the
  minimal change is wrapping that call in a `while` loop with a delay and
  a cancellation token. Introducing `IHostedService`/`BackgroundService`
  would mean also starting the host lifetime (`host.RunAsync()`), which
  today's `Program.cs` never does — that's a bigger structural change
  than this proposal needs and would still require signal handling to be
  wired up for `IHostApplicationLifetime` to reflect Ctrl+C/SIGTERM
  correctly outside of a web host. Revisit if a second long-running
  concern shows up that would benefit from real hosted-service lifecycle
  management.

- **Shutdown is a `CancellationTokenSource` cancelled from
  `PosixSignalRegistration` for `SIGINT` and `SIGTERM`.**
  `PosixSignalRegistration.Create` is available on .NET 6+ and handles
  Ctrl+C (delivered as `SIGINT`) uniformly on Windows and Linux, which
  matters since SpecRunner's dev/runtime targets include both. The
  registration's handler sets `context.Cancel = true` (so the default
  terminate-immediately behavior doesn't fire) and cancels the token
  source; the loop observes that token only while awaiting the polling
  delay, not while a scan pass is in flight. Alternative considered:
  `Console.CancelKeyPress` — rejected because it only covers Ctrl+C, not
  `SIGTERM` (the signal a container runtime or service manager sends on
  graceful stop).

- **A shutdown signal lets an in-flight scan pass finish rather than
  cancelling it mid-flight.** The loop does not pass the shutdown token
  into `RunOnceAsync`; it only checks the token before starting the next
  pass and while awaiting `PollingInterval`. Cancelling mid-pass would
  propagate into whatever git/GitHub call is in flight (e.g. mid-commit,
  mid-push, between adding the `eyes` reaction and ever resolving it to
  `rocket`/`confused`), leaving the local clone or a comment's reaction
  state visibly stuck. Letting the pass finish keeps every comment's
  reaction/reply cycle atomic from an outside observer's perspective, at
  the cost of shutdown taking as long as the slowest in-flight comment
  (already bounded by `TaskTimeout`).

- **A scan pass is wrapped in try/catch inside the loop; the loop itself
  never exits because of it.** Before this change, an unhandled exception
  from `RunOnceAsync` was the *only* way a scan pass communicated failure
  to the outside (non-zero exit code) — appropriate for a one-shot
  process. In a continuous loop that behavior would mean one transient
  failure (e.g. a momentary GitHub API error) permanently ends the
  "always running" process. Instead, the loop logs the exception at
  `Error` level via the existing `ILogger<Program>`/Serilog wiring and
  proceeds to the next `PollingInterval` wait. The process's exit code is
  now only meaningful for the startup connection-test failure path
  (unchanged) or a graceful shutdown (`0`).

- **`PollingInterval` is a new `TimeSpan` property on
  `SpecRunnerOptions`, bound the same way as `TaskTimeout`.** No separate
  options class — it's a single value governing the same console
  process's behavior as the rest of `SpecRunnerOptions`. Default value is
  10 seconds, matching the value already present in the local dev
  `appsettings.json` used against the test repository.

- **`--dangerously-skip-permissions` is a fixed argument, not a
  configurable one.** It's appended alongside the existing hardcoded
  `--print`/`--verbose`/stream-json flags in
  `ClaudeCliAgentSession.StartAsync` rather than exposed through
  `CliAgentOptions`. SpecRunner has no interactive terminal attached to
  the CLI subprocess and no mechanism to answer a permission prompt, so
  running without this flag is never a valid configuration for this
  application — there's nothing for a config toggle to meaningfully
  select between.

- **A new `ICliAgentSession.CloseInputAsync` member closes stdin without
  stopping the session.** It delegates to the same
  `IChildProcess.CloseStandardInput()` that `StopAsync` already calls,
  but does *not* wait for exit, force-kill, or transition `State` away
  from `Running` — the session stays `Running` and its event stream keeps
  yielding events until the process exits on its own (reaching
  `Completed`/`Failed` through the existing `OnProcessExited` path) or
  `StopAsync`/dispose intervenes. Valid only in state `Running` (mirrors
  `SendCommandAsync`'s guard); throws `InvalidOperationException`
  otherwise. `ProposeWorkflowRunner.ProcessCommentAsync` calls it
  immediately after `session.StartAsync(...)` returns, since it never
  calls `SendCommandAsync` — the initial prompt is the only turn it ever
  sends. Alternative considered: have `StartAsync` itself close stdin
  right after writing the initial prompt — rejected because
  `ClaudeCliAgentSession` is the shared multi-turn primitive (its own
  tests drive `SendCommandAsync`/`CancelCurrentRequestAsync` after
  `StartAsync`), so baking in an input-close would break any caller that
  intends to keep talking to the session.

## Risks / Trade-offs

- [A stuck/hanging scan pass with no timeout of its own would block the
  loop indefinitely, delaying the next poll forever] → Each comment
  processed within a scan pass is already bounded by
  `SpecRunnerOptions.TaskTimeout` (existing `propose-workflow` behavior);
  the scan-level loop adds no additional outer timeout, which is
  acceptable since the per-comment bound already caps the worst case.
- [Logging-and-continuing on scan-pass exceptions could mask a
  persistent, non-transient failure (e.g. an expired PAT) by silently
  retrying forever] → Each failed pass is logged at `Error` level to the
  existing Serilog file sink, so a persistent failure is visible in logs
  as a repeating error rather than a crash; alerting on that pattern is
  left to log monitoring, not this change.
- [`--dangerously-skip-permissions` removes a safety backstop the CLI
  tool would otherwise provide] → Acceptable because SpecRunner's whole
  purpose is unattended execution against a disposable local clone
  dedicated to it, and there is no human available to service prompts if
  the flag were absent — the process would simply hang instead.
