# cli-agent-execution

## Purpose

TBD - defines how SpecRunner configures, launches, and drives an external
CLI coding agent (e.g. the Claude Code CLI) as a subprocess, including
configurable launch options, the session abstraction used to start/drive/
stop a conversation, incremental event streaming, and the stream-json-based
Claude CLI implementation.

## Requirements

### Requirement: CLI agent execution options are configurable
`SpecRunner.Core` SHALL define a `CliAgentOptions` model exposing, at
minimum: the executable/command to launch (`Executable`, defaulting to
`"claude"`), a list of additional command-line arguments (`Arguments`), and
an optional working directory override (`WorkingDirectory`).
`SpecRunner.Console` SHALL bind this model from configuration via the
standard `IOptions` pattern under a `CliAgent` section, so the specific CLI
tool invoked is a configuration value rather than a hardcoded string.

#### Scenario: Options bind from appsettings.json
- **WHEN** an `appsettings.json` file supplies a `CliAgent` section with
  `Executable`, `Arguments`, and `WorkingDirectory` values
- **THEN** `IOptions<CliAgentOptions>` resolved from the host SHALL expose
  those values unchanged

#### Scenario: Executable defaults when not configured
- **WHEN** no `Executable` value is present in any configuration source
- **THEN** `CliAgentOptions.Executable` SHALL resolve to `"claude"`

#### Scenario: Working directory falls back to the local repository path
- **WHEN** `CliAgentOptions.WorkingDirectory` is empty and a session is
  started
- **THEN** the underlying process SHALL be launched with its working
  directory set to `SpecRunnerOptions.LocalRepositoryPath`

### Requirement: CLI agent session abstraction
`SpecRunner.Core` SHALL define an `ICliAgentSession` interface representing
a single running CLI-agent conversation, exposing at minimum: starting the
session with an initial prompt, sending additional commands into a running
session, requesting cancellation of the current in-flight request, stopping
the session, and a current `CliAgentSessionState`
(`NotStarted`/`Running`/`Cancelling`/`Completed`/`Failed`/`Stopped`).
`SpecRunner.Core` SHALL also define `ICliAgentSessionFactory` with a single
method that creates a new `ICliAgentSession` instance, since a session is
stateful and scoped to one conversation rather than shared or long-lived.

#### Scenario: Factory creates an independent session per call
- **WHEN** `ICliAgentSessionFactory.CreateSession()` is called twice
- **THEN** two distinct `ICliAgentSession` instances SHALL be returned,
  each starting in state `NotStarted` and independently startable/stoppable

#### Scenario: Session factory is registered in DI
- **WHEN** the `SpecRunner.Console` DI container is inspected
- **THEN** `ICliAgentSessionFactory` SHALL resolve to the `SpecRunner.Cli`
  implementation

### Requirement: Starting a session launches the configured CLI tool with an initial prompt
Calling `ICliAgentSession.StartAsync` with an initial prompt SHALL launch
the executable and arguments configured via `CliAgentOptions` as a child
process in the resolved working directory, transition the session to state
`Running`, and deliver the initial prompt as the first user turn to the
process. Starting a session that is not in state `NotStarted` SHALL throw
`InvalidOperationException` without launching a second process.

#### Scenario: Starting a fresh session launches the process
- **WHEN** `StartAsync` is called on a session in state `NotStarted` with
  prompt `"propose a change for issue 45"`
- **THEN** the configured executable SHALL be launched as a child process
  and the session SHALL transition to state `Running`

#### Scenario: Starting an already-started session fails
- **WHEN** `StartAsync` is called on a session whose state is not
  `NotStarted`
- **THEN** the call SHALL throw `InvalidOperationException` and no
  additional process SHALL be launched

### Requirement: Session output streams incrementally as events
`ICliAgentSession` SHALL expose an asynchronous stream of `CliAgentEvent`
values produced as the underlying process emits output, rather than only
after the process exits. Each `CliAgentEvent` SHALL carry a
`CliAgentEventKind` (at minimum `AssistantMessage`, `ToolUse`,
`ToolResult`, `SystemInfo`, `Error`, `ResultCompleted`) and the associated
text/payload.

#### Scenario: Events are observable before the process exits
- **WHEN** a started session's underlying process has emitted output but
  has not yet exited
- **THEN** consuming `ICliAgentSession`'s event stream SHALL yield the
  `CliAgentEvent` values produced so far without waiting for process exit

#### Scenario: Unparseable output is surfaced, not dropped
- **WHEN** the underlying process writes a line of output that is not
  valid stream-json
- **THEN** the event stream SHALL yield a `CliAgentEvent` of kind `Error`
  describing the parse failure rather than silently discarding the line

### Requirement: Additional commands can be sent into a running session
`ICliAgentSession.SendCommandAsync` SHALL write a new user turn to a
session in state `Running`, allowing follow-up instructions to be given
without starting a new process. Calling it on a session that is not
`Running` SHALL throw `InvalidOperationException`.

#### Scenario: Follow-up command reaches the running process
- **WHEN** `SendCommandAsync` is called with text `"also update the tests"`
  on a session in state `Running`
- **THEN** that text SHALL be written to the underlying process as a new
  user turn without terminating or restarting the process

#### Scenario: Sending a command to a non-running session fails
- **WHEN** `SendCommandAsync` is called on a session in state
  `NotStarted`, `Completed`, `Failed`, or `Stopped`
- **THEN** the call SHALL throw `InvalidOperationException`

### Requirement: The current in-flight request can be cancelled without ending the session
`ICliAgentSession.CancelCurrentRequestAsync` SHALL send an interrupt
control message to a session in state `Running`, stopping the agent's
current in-flight turn while leaving the underlying process alive and the
session in state `Running` so further commands can still be sent.

#### Scenario: Cancelling an in-flight request keeps the session alive
- **WHEN** `CancelCurrentRequestAsync` is called while the session is
  `Running` and the process is mid-response
- **THEN** an interrupt control message SHALL be sent to the process, the
  process SHALL NOT be terminated, and the session SHALL remain in state
  `Running` and accept subsequent `SendCommandAsync` calls

#### Scenario: Cancelling when nothing is in flight is a no-op
- **WHEN** `CancelCurrentRequestAsync` is called while the session is
  `Running` but idle (no request currently being processed)
- **THEN** the call SHALL complete without error and without changing
  session state

### Requirement: Stopping a session terminates the underlying process
`ICliAgentSession.StopAsync` SHALL end the conversation and terminate the
underlying process (gracefully if the process exits on its own within a
short grace period, otherwise forcefully), transitioning the session to
state `Stopped`. `ICliAgentSession` SHALL implement `IAsyncDisposable`,
calling `StopAsync` if the session has not already reached a terminal
state (`Completed`, `Failed`, `Stopped`) when disposed.

#### Scenario: Stopping a running session ends the process
- **WHEN** `StopAsync` is called on a session in state `Running`
- **THEN** the underlying process SHALL be terminated and the session
  SHALL transition to state `Stopped`

#### Scenario: Disposing an unstopped session stops it
- **WHEN** an `ICliAgentSession` in state `Running` is disposed without
  `StopAsync` having been called first
- **THEN** the underlying process SHALL be terminated as part of disposal

### Requirement: Process exit and crashes surface as terminal events, not unhandled exceptions
When the underlying process exits, `ICliAgentSession` SHALL transition to
state `Completed` on a zero exit code following a `ResultCompleted` event,
or to state `Failed` on a non-zero exit code, in both cases yielding a
final `CliAgentEvent` describing the outcome (exit code and any captured
stderr) rather than throwing from the event stream.

#### Scenario: Clean exit reaches Completed
- **WHEN** the underlying process exits with code `0` after emitting a
  `ResultCompleted` event
- **THEN** the session SHALL transition to state `Completed`

#### Scenario: Non-zero exit reaches Failed with details
- **WHEN** the underlying process exits with a non-zero code
- **THEN** the session SHALL transition to state `Failed` and the final
  `CliAgentEvent` SHALL include the exit code and any captured stderr
  output, and the event stream SHALL complete without throwing

### Requirement: Claude CLI implementation uses the stream-json protocol
`SpecRunner.Cli` SHALL provide the default `ICliAgentSession`/
`ICliAgentSessionFactory` implementation for the Claude Code CLI, launching
the configured executable with stream-json input and output enabled
(newline-delimited JSON on stdin for user turns and control/interrupt
messages, newline-delimited JSON on stdout for agent events) and with
permission prompts disabled (`--dangerously-skip-permissions`), since
SpecRunner runs the CLI unattended with no human available to answer a
permission prompt, so that prompts, follow-up commands, and interrupts are
exchanged over the running process's stdin/stdout rather than by
restarting the process per turn.

#### Scenario: Session is launched with stream-json input and output
- **WHEN** `ClaudeCliAgentSession.StartAsync` launches the configured
  executable
- **THEN** the process SHALL be started with stream-json input and output
  enabled and with stdin/stdout redirected for the session's lifetime

#### Scenario: Session is launched with permission prompts disabled
- **WHEN** `ClaudeCliAgentSession.StartAsync` launches the configured
  executable
- **THEN** the process SHALL be started with the
  `--dangerously-skip-permissions` argument, in addition to any
  `CliAgentOptions.Arguments` configured

### Requirement: A running session's input can be closed without stopping the session
`ICliAgentSession` SHALL expose a member that closes the underlying
process's standard input while leaving the process running and the
session in state `Running`, so a caller that has finished sending turns
can signal end-of-input and let the process notice stdin closed and exit
on its own, without the grace-period-then-kill behavior of `StopAsync`.
Calling it on a session that is not in state `Running` SHALL throw
`InvalidOperationException`. Closing input SHALL NOT itself change
`State`; the session SHALL still transition to `Completed`/`Failed`
through the normal process-exit path, or to `Stopped` if `StopAsync` is
called afterward.

#### Scenario: Closing input on a running session leaves it running
- **WHEN** the session's input-close member is called while the session
  is in state `Running`
- **THEN** the underlying process's standard input SHALL be closed, the
  process SHALL NOT be terminated, and the session SHALL remain in state
  `Running`

#### Scenario: A process that exits after input closes reaches a terminal state normally
- **WHEN** input has been closed via the session's input-close member and
  the underlying process subsequently exits on its own
- **THEN** the session SHALL transition to `Completed` or `Failed` per the
  existing process-exit handling, exactly as if the process had exited
  without input having been explicitly closed

#### Scenario: Closing input on a non-running session fails
- **WHEN** the session's input-close member is called on a session in
  state `NotStarted`, `Completed`, `Failed`, or `Stopped`
- **THEN** the call SHALL throw `InvalidOperationException`
