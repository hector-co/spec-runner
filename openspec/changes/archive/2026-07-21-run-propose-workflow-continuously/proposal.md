## Why

SpecRunner is meant to watch GitHub for `/propose` comments, but each
invocation of `SpecRunner.Console` runs exactly one scan pass and exits,
so it only reacts to comments that exist at the moment someone happens to
run it — there is no standing process actually watching the repository.
Separately, the CLI-agent subprocess is launched without
`--dangerously-skip-permissions`, so an unattended run can stall on a
permission prompt with nobody present to answer it, and the process is
never told its input is finished, so it can sit waiting for another turn
instead of exiting once its one-shot prompt has been answered.

## What Changes

- Change `SpecRunner.Console`'s entry point from "connection test, one
  `propose-workflow` scan pass, exit" to "connection test, then repeat
  `propose-workflow` scan passes on a configurable interval until the
  process receives a shutdown signal (Ctrl+C/SIGTERM)". **BREAKING**: the
  process no longer exits after one pass; running it now requires an
  external supervisor (service manager, container restart policy) to
  expect a long-lived process instead of a one-shot exit code.
- Add a new `PollingInterval` setting to `SpecRunnerOptions` (`TimeSpan`,
  bound from configuration) controlling the delay between scan passes.
- Always launch the Claude CLI subprocess with `--dangerously-skip-permissions`
  alongside the existing fixed `--print`/`--verbose`/stream-json flags,
  since SpecRunner runs unattended and no human is available to approve a
  permission prompt.
- Add a way to close a running CLI-agent session's input stream without
  terminating the session, so a caller that has sent its only prompt can
  signal "no more input is coming" and let the CLI process exit on its
  own once it finishes responding. The `propose-workflow` orchestration
  calls this immediately after its one-shot `StartAsync` prompt is sent.

## Capabilities

### New Capabilities
(none — this change modifies existing capabilities)

### Modified Capabilities
- `solution-layout`: the console entry point requirement changes from
  "connection test, one scan pass, exit" to "connection test, then repeat
  scan passes on `PollingInterval` until a shutdown signal is received."
- `cli-agent-execution`: the Claude CLI implementation requirement gains
  the `--dangerously-skip-permissions` launch flag, and
  `ICliAgentSession` gains a way to close a running session's input
  stream without stopping the session outright.

## Impact

- `SpecRunner.Core`: `SpecRunnerOptions` gains `PollingInterval`;
  `ICliAgentSession` gains an input-close member.
- `SpecRunner.Cli`: `ClaudeCliAgentSession` adds
  `--dangerously-skip-permissions` to its fixed argument list and
  implements the new input-close member via `IChildProcess.CloseStandardInput`.
- `SpecRunner.Console`: `Program.cs` becomes a polling loop with
  graceful-shutdown handling instead of a single pass; `ProposeWorkflowRunner`
  closes the CLI-agent session's input right after starting it.
- `appsettings.json`: gains a `PollingInterval` key under `SpecRunner`.
- Out of scope: exponential backoff or jitter on the polling interval;
  converting the process into a Windows Service/systemd unit (the process
  itself now runs continuously, but installing it as a managed service is
  left to deployment); parallel/overlapping scan passes (a pass still
  runs to completion before the next one starts).
