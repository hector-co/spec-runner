## Context

SpecRunner's project context states its purpose as watching issue/PR
comments and driving OpenSpec propose/update/implement/archive workflows,
but the prior changes ([solution-layout], [repository-connection],
[state-store-schema]) only stood up project structure, config, connection
testing, and state persistence — `IGitService`/`IGitHubService` are still
`NotImplementedException` placeholders, and nothing invokes the actual
coding agent that does propose/implement/archive work. This change adds
that missing piece: a way to run a CLI-based coding agent (Claude Code CLI
today, a different executable in the future via config) as a child process,
observe its output as it streams, feed it more instructions mid-session,
and interrupt it without tearing the whole process down.

This is a process-execution primitive only. It does not decide when to
start a session, what prompt to send, or how to react to events — that
belongs to a future change that wires this into the comment-driven
workflow loop (state store lookups, `TaskTimeout` enforcement, posting
progress back to PR comments).

## Goals / Non-Goals

**Goals:**
- Make the CLI executable/command a configuration value
  (`CliAgentOptions.Executable`, default `"claude"`), not a hardcoded
  string, so a different CLI-based agent can be substituted by changing
  config.
- Stream agent output as it's produced (assistant text, tool activity,
  completion, errors) rather than buffering until the process exits, so a
  caller can report progress incrementally.
- Support sending more than one command into the same running session
  (multi-turn), not just a single fire-and-forget prompt.
- Support cancelling the agent's current in-flight turn without killing the
  session, mirroring "cancel the current request" from an interactive
  Claude Code session.
- Report process exit and crashes as data (a terminal `CliAgentEvent` and
  session state), not as exceptions thrown out of the event stream.

**Non-Goals:**
- Deciding what prompt to send or when — that's the workflow loop's job,
  not this primitive's.
- Enforcing `SpecRunnerOptions.TaskTimeout` — a future caller wraps
  `StartAsync`/the event stream with its own cancellation/timeout; this
  change doesn't hardcode a timeout into the session itself.
- Posting progress to GitHub PR/issue comments — that's `IGitHubService`'s
  concern once implemented, consuming this session's events.
- Supporting CLI tools that don't speak a JSON-lines stdin/stdout protocol
  — `SpecRunner.Cli`'s implementation targets Claude Code CLI's
  stream-json mode specifically; a genuinely different protocol would need
  its own `ICliAgentSession` implementation, not a config toggle, since
  parsing/framing differs.
- Permission prompts / tool-approval flows the CLI might raise
  interactively — out of scope here; `SystemInfo`/`Error` events surface
  whatever the CLI reports, but no auto-approval logic is added.

## Decisions

- **New `SpecRunner.Cli` project, not folded into `SpecRunner.Core` or an
  existing project.** Matches the established pattern (`SpecRunner.Git`,
  `SpecRunner.GitHub`, `SpecRunner.State`): interfaces/models in
  `SpecRunner.Core`, the concrete implementation with its own dependencies
  in a dedicated project referencing only `Core`. Keeps `Core` free of
  `System.Diagnostics.Process` orchestration details and keeps the
  Claude-CLI-specific stream-json parsing isolated so a future alternate
  implementation doesn't have to coexist in the same project.

- **`ICliAgentSession` is per-conversation, created via
  `ICliAgentSessionFactory`, not a singleton service.** A session owns one
  child process and its stdin/stdout for the lifetime of one conversation;
  registering `ICliAgentSession` itself as a singleton would force one
  process for the app's entire lifetime and make concurrent/sequential
  conversations impossible. The factory is the long-lived, DI-registered
  piece; each `CreateSession()` call yields an independent, disposable
  session. Alternative considered: a single long-lived session reused
  across issues — rejected because a stuck or crashed process would then
  take down all future work instead of just the one conversation.

- **Long-running process with a JSON-lines stdin/stdout protocol
  (stream-json), not one process invocation per prompt.** Claude Code
  CLI's `--input-format stream-json --output-format stream-json` mode
  keeps a single process alive across multiple user turns, exchanging
  newline-delimited JSON messages on stdin (user turns, control/interrupt
  requests) and stdout (assistant/tool/result events). This is what makes
  `SendCommandAsync` (multi-turn) and `CancelCurrentRequestAsync`
  (interrupt) possible at all — a one-shot `claude -p "prompt"` invocation
  per command would mean a new process per turn and no way to interrupt a
  turn already in flight. Alternative considered: shell out per command
  and diff state between calls — rejected, it can't express "interrupt the
  turn that's currently running" and reprocesses the same context
  repeatedly.

- **Interrupt is a control message, not `Process.Kill`/`SIGINT` on the
  child process.** Killing the process would end the whole session
  (losing conversation context) to cancel a single turn. Writing a
  control/interrupt message on stdin — the same mechanism the CLI's
  stream-json protocol uses for user turns — stops only the current turn
  and leaves the process, and the conversation, running. `StopAsync`
  remains the path that actually terminates the process, for callers that
  want the whole session gone.

- **Events modeled as a typed `CliAgentEventKind` enum + payload, not raw
  JSON exposed to callers.** `AssistantMessage`, `ToolUse`, `ToolResult`,
  `SystemInfo`, `Error`, `ResultCompleted` covers what a caller building a
  workflow loop needs to react to (report progress, detect completion,
  detect failure) without every consumer re-parsing the CLI's wire format.
  The raw line is still retained on the event for callers that need it,
  but dispatch/logging code works off `CliAgentEventKind`.

- **Unparseable stdout lines become `Error` events, not silently dropped
  or thrown.** A future CLI version changing its output shape shouldn't
  crash the workflow loop or vanish without a trace; surfacing it as an
  `Error` event keeps the stream consumable and gives the caller (and logs)
  visibility into the mismatch.

- **`WorkingDirectory` defaults to `SpecRunnerOptions.LocalRepositoryPath`
  when unset.** The CLI agent's whole purpose is to operate on the one
  local clone SpecRunner is configured against; requiring every caller to
  pass that path explicitly through `CliAgentOptions` would just duplicate
  `SpecRunnerOptions.LocalRepositoryPath`. An explicit
  `CliAgentOptions.WorkingDirectory` is still allowed for tests/overrides.

- **No new NuGet dependencies.** `System.Diagnostics.Process` (redirected
  stdin/stdout/stderr, async reads) and `System.Text.Json` (already used
  transitively via `Microsoft.Extensions.*`) are sufficient for
  process management and stream-json parsing; nothing here needs a
  dedicated process-management or JSON-RPC package.

## Risks / Trade-offs

- [Claude Code CLI's stream-json wire format (event/control message
  shapes) is not a versioned, guaranteed-stable public contract] →
  Unparseable/unexpected lines surface as `Error` events instead of
  crashing (see Decisions), and `SpecRunner.Cli` is the single place that
  would need updating if the CLI's output shape changes.
- [Interrupt-without-kill relies on the CLI process actually honoring a
  stdin control message while mid-turn; if it doesn't, cancellation could
  appear to hang] → `CancelCurrentRequestAsync` only sends the message; it
  does not block waiting for confirmation beyond the write completing, so
  a caller with its own timeout (e.g. a future `TaskTimeout` wrapper) can
  still fall back to `StopAsync` if the session doesn't respond.
- [A crashed or hung child process could leak if `StopAsync`/dispose is
  never called] → `ICliAgentSession` implements `IAsyncDisposable` and
  stops the process on disposal if it hasn't reached a terminal state,
  matching the existing `HttpRepositoryConnectionTester`-style pattern of
  disposing acquired resources.
- [No timeout is enforced inside this change] → Deliberate per Non-Goals;
  a future workflow-loop change owns wrapping session calls with
  `SpecRunnerOptions.TaskTimeout` and deciding what "processing stops,
  error indicator recorded" means for a PR comment, rather than this
  primitive guessing at that policy.
