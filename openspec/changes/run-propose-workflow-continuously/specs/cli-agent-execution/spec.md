## MODIFIED Requirements

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

## ADDED Requirements

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
