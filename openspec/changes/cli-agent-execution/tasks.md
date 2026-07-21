## 1. Project structure

- [ ] 1.1 Create the `SpecRunner.Cli` project under `SpecRunner/src/SpecRunner.Cli`,
      referencing only `SpecRunner.Core`, using the centralized
      `Directory.Build.props`/`Directory.Build.targets` settings (no
      per-project `TargetFramework`/`ImplicitUsings`/`Nullable`).
- [ ] 1.2 Register `SpecRunner.Cli` in `SpecRunner.sln`.
- [ ] 1.3 Add a `SpecRunner.Cli` project reference to
      `SpecRunner.Console.csproj`.

## 2. Configuration model

- [ ] 2.1 Add `CliAgentOptions` to `SpecRunner.Core/Configuration` with
      `SectionName = "CliAgent"`, `Executable` (default `"claude"`),
      `Arguments` (`List<string>`, default empty), and `WorkingDirectory`
      (`string?`, default null/empty).
- [ ] 2.2 Add a `CliAgent` section to
      `SpecRunner.Console/appsettings.json` (`Executable: "claude"`, empty
      `Arguments`, no `WorkingDirectory` so it falls back to
      `LocalRepositoryPath`).
- [ ] 2.3 Register `CliAgentOptions` in `SpecRunner.Console`'s DI container
      via `AddOptions<CliAgentOptions>().Bind(...)`, matching the existing
      `SpecRunnerOptions` registration pattern.

## 3. Core abstractions

- [ ] 3.1 Add `CliAgentSessionState` enum to `SpecRunner.Core/Models`
      (`NotStarted`, `Running`, `Cancelling`, `Completed`, `Failed`,
      `Stopped`).
- [ ] 3.2 Add `CliAgentEventKind` enum to `SpecRunner.Core/Models`
      (`AssistantMessage`, `ToolUse`, `ToolResult`, `SystemInfo`, `Error`,
      `ResultCompleted`).
- [ ] 3.3 Add `CliAgentEvent` record to `SpecRunner.Core/Models` carrying
      `CliAgentEventKind`, a text/payload string, the raw source line, and
      a timestamp.
- [ ] 3.4 Add `ICliAgentSession` interface to
      `SpecRunner.Core/Abstractions` with: `CliAgentSessionState State`;
      `Task StartAsync(string initialPrompt, CancellationToken)`;
      `IAsyncEnumerable<CliAgentEvent> ReadEventsAsync(CancellationToken)`;
      `Task SendCommandAsync(string text, CancellationToken)`;
      `Task CancelCurrentRequestAsync(CancellationToken)`;
      `Task StopAsync(CancellationToken)`; extending `IAsyncDisposable`.
- [ ] 3.5 Add `ICliAgentSessionFactory` interface to
      `SpecRunner.Core/Abstractions` with
      `ICliAgentSession CreateSession()`.

## 4. Claude CLI session implementation

- [ ] 4.1 Add `ClaudeCliAgentSession` to `SpecRunner.Cli` implementing
      `ICliAgentSession`: `StartAsync` launches `CliAgentOptions.Executable`
      via `System.Diagnostics.Process` with stream-json input/output
      arguments appended to `CliAgentOptions.Arguments`, redirected
      stdin/stdout/stderr, working directory resolved to
      `CliAgentOptions.WorkingDirectory` or
      `SpecRunnerOptions.LocalRepositoryPath`, and writes the initial
      prompt as the first stream-json user-turn message on stdin;
      throws `InvalidOperationException` if called when state is not
      `NotStarted`.
- [ ] 4.2 Implement an async stdout reader that parses newline-delimited
      JSON into `CliAgentEvent`s and publishes them to `ReadEventsAsync`'s
      channel as they arrive; unparseable lines become `Error` events
      instead of throwing or being dropped.
- [ ] 4.3 Implement `SendCommandAsync`: writes a new stream-json user-turn
      message to stdin when `State == Running`; throws
      `InvalidOperationException` otherwise.
- [ ] 4.4 Implement `CancelCurrentRequestAsync`: writes a stream-json
      interrupt/control message to stdin when `State == Running`, without
      terminating the process or changing session state; completes
      without error if there is nothing in flight.
- [ ] 4.5 Implement `StopAsync`: closes stdin, waits briefly for graceful
      exit, force-kills the process if it hasn't exited within the grace
      period, and transitions state to `Stopped`.
- [ ] 4.6 Implement `IAsyncDisposable`: calls `StopAsync` if the session
      is not already in a terminal state (`Completed`, `Failed`,
      `Stopped`).
- [ ] 4.7 Wire process-exit handling: exit code `0` following a
      `ResultCompleted` event transitions state to `Completed`; non-zero
      exit transitions state to `Failed` and the final `CliAgentEvent`
      includes the exit code and captured stderr; the event stream
      completes normally in both cases (no exception).
- [ ] 4.8 Add `ClaudeCliAgentSessionFactory` implementing
      `ICliAgentSessionFactory`, taking `IOptions<CliAgentOptions>` and
      `IOptions<SpecRunnerOptions>` and returning a new
      `ClaudeCliAgentSession` per `CreateSession()` call.

## 5. Console wiring

- [ ] 5.1 Register `ICliAgentSessionFactory` as a singleton resolving to
      `ClaudeCliAgentSessionFactory` in `SpecRunner.Console`'s DI
      container.

## 6. Tests

- [ ] 6.1 Add a fake/scriptable process abstraction (or an in-memory
      stdin/stdout harness) in `SpecRunner.Tests` so
      `ClaudeCliAgentSession` behavior can be tested without spawning a
      real `claude` process.
- [ ] 6.2 Add unit tests for `CliAgentOptions` binding (defaults and
      explicit values) from configuration.
- [ ] 6.3 Add unit tests for session lifecycle: `StartAsync` transitions
      `NotStarted` → `Running`; a second `StartAsync` call throws
      `InvalidOperationException`.
- [ ] 6.4 Add unit tests for event streaming: events are observable as
      they're produced (not only after process exit), and an unparseable
      output line yields an `Error` event rather than throwing.
- [ ] 6.5 Add unit tests for `SendCommandAsync`: succeeds while `Running`;
      throws `InvalidOperationException` for every other state.
- [ ] 6.6 Add unit tests for `CancelCurrentRequestAsync`: writes an
      interrupt message without ending the session or the process; is a
      no-op when idle.
- [ ] 6.7 Add unit tests for `StopAsync`/dispose: process is terminated,
      state becomes `Stopped`; disposing a running session without an
      explicit `StopAsync` call also terminates the process.
- [ ] 6.8 Add unit tests for process-exit handling: zero exit with a
      prior `ResultCompleted` event yields state `Completed`; non-zero
      exit yields state `Failed` with exit code and stderr on the final
      event.
- [ ] 6.9 Add/update the DI smoke test to confirm
      `ICliAgentSessionFactory` resolves from the container.
- [ ] 6.10 Verify `dotnet test SpecRunner/SpecRunner.sln` passes.

## 7. Documentation

- [ ] 7.1 Update `SpecRunner/README.md` to describe the `CliAgent`
      configuration section, the `ICliAgentSession`/
      `ICliAgentSessionFactory` abstractions, and their current scope
      (process-execution primitive only, not yet wired into a workflow
      loop).

## 8. Verification

- [ ] 8.1 Run `dotnet build SpecRunner/SpecRunner.sln` and confirm it
      succeeds with no errors.
- [ ] 8.2 Run `dotnet test SpecRunner/SpecRunner.sln` and confirm all
      tests pass.
- [ ] 8.3 Manually start a session against a real `claude` executable
      (if available in the environment) with a trivial prompt, confirm
      events stream before the process exits, send a follow-up command,
      call `CancelCurrentRequestAsync` mid-response and confirm the
      session stays `Running`, then call `StopAsync` and confirm the
      process ends.
