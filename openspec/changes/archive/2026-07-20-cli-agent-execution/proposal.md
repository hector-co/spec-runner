## Why

SpecRunner's stated purpose is to drive OpenSpec propose/update/implement/archive
workflows by watching issue and PR comments, but nothing in the codebase yet
runs the actual coding agent that does that work. The app has no way to
launch a CLI-based coding agent (Claude Code CLI, configurable to another
executable later), feed it a prompt, observe its progress as it works, send
it follow-up instructions mid-session, or interrupt an in-flight request
(e.g. "cancel the current request") without killing the whole process. This
change adds that process-execution primitive so a future workflow-loop
change has something concrete to call.

## What Changes

- Add a `CliAgentOptions` configuration model (`SpecRunner.Core`) exposing
  the CLI executable/command to launch (default `claude`), extra
  command-line arguments, and an optional working directory override, bound
  via the standard `IOptions` pattern — so the specific CLI tool is a config
  value, not a hardcoded string.
- Add `ICliAgentSession` (`SpecRunner.Core`): an abstraction over a single
  running CLI-agent process/conversation exposing: starting a session with
  an initial prompt, streaming events back as the agent works (assistant
  output, tool activity, completion, errors) as they arrive rather than only
  after the process exits, sending additional commands into a running
  session, cancelling the current in-flight request without ending the
  session, and stopping the session entirely.
- Add `ICliAgentSessionFactory` (`SpecRunner.Core`) to create new
  `ICliAgentSession` instances per invocation (sessions are stateful and
  short-lived; the factory is what gets registered in DI).
- Add a new `SpecRunner.Cli` project providing the concrete implementation:
  launches the configured executable as a child process using Claude Code
  CLI's streaming JSON stdin/stdout protocol (`--input-format stream-json
  --output-format stream-json`), parses newline-delimited JSON events off
  stdout into typed `CliAgentEvent`s, writes user-turn and control (interrupt)
  messages to stdin, and surfaces process exit/crash as a terminal event.
- Register `CliAgentOptions` and `ICliAgentSessionFactory` in
  `SpecRunner.Console`'s DI container and add a `CliAgent` section to
  `appsettings.json`.

## Capabilities

### New Capabilities
- `cli-agent-execution`: configuring which CLI executable/arguments to run,
  starting and streaming a CLI-agent process session, sending follow-up
  commands into a running session, cancelling the current in-flight request
  without ending the session, and stopping the session.

### Modified Capabilities
- `solution-layout`: project responsibility separation gains a sixth
  project, `SpecRunner.Cli`, depending only on `SpecRunner.Core` and
  referenced by `SpecRunner.Console`, alongside the existing
  `SpecRunner.Git`/`SpecRunner.GitHub`/`SpecRunner.State` projects.

## Impact

- `SpecRunner.Core`: new `CliAgentOptions`, `ICliAgentSession`,
  `ICliAgentSessionFactory`, `CliAgentEvent`/`CliAgentEventKind`,
  `CliAgentSessionState` types.
- `SpecRunner.Cli` (new project): `ClaudeCliAgentSession`,
  `ClaudeCliAgentSessionFactory`, stream-json parsing/writing.
- `SpecRunner.Console`: DI registration for `CliAgentOptions` and
  `ICliAgentSessionFactory`; `appsettings.json` gains a `CliAgent` section;
  `SpecRunner.Console.csproj` references the new project.
- `SpecRunner/Directory.Packages.props` / `SpecRunner.sln`: new project
  registered, no new external package dependencies expected (uses
  `System.Diagnostics.Process` and `System.Text.Json`, both already
  available).
- Out of scope: actually wiring a session into the issue/PR comment-driven
  workflow loop, timeout enforcement against `SpecRunnerOptions.TaskTimeout`,
  and state-store updates — this change only provides the primitive a future
  workflow-loop change will call.
