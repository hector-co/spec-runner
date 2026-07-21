## 1. Configuration (`SpecRunner.Core`)

- [x] 1.1 Add `PollingInterval` (`TimeSpan`, default `00:00:10`) to
      `SpecRunnerOptions`.
- [x] 1.2 Add a `PollingInterval` key under `SpecRunner` to
      `SpecRunner/src/SpecRunner.Console/appsettings.json` (and the
      `publish/win-x86` copy if one is checked in), matching the default.

## 2. CLI-agent session input closing (`SpecRunner.Core`, `SpecRunner.Cli`)

- [x] 2.1 Add a `CloseInputAsync(CancellationToken cancellationToken =
      default)` member to `ICliAgentSession` in
      `SpecRunner.Core/Abstractions/ICliAgentSession.cs`.
- [x] 2.2 Implement `CloseInputAsync` in `ClaudeCliAgentSession`: throw
      `InvalidOperationException` unless `State == Running`; otherwise
      call `_process.CloseStandardInput()` without waiting for exit,
      killing the process, or changing `State`.
- [x] 2.3 Update `FakeCliAgentSession` (test fake, if used beyond
      `ProposeWorkflowRunnerTests`' own fake) and
      `ProposeWorkflowRunnerTests`' inline fake session to implement the
      new interface member as a no-op returning `Task.CompletedTask`.

## 3. Claude CLI launch flags (`SpecRunner.Cli`)

- [x] 3.1 Add `--dangerously-skip-permissions` to the fixed argument list
      in `ClaudeCliAgentSession.StartAsync`, alongside the existing
      `--print`, `--verbose`, `--input-format stream-json`,
      `--output-format stream-json` flags.

## 4. Propose-workflow uses the new input-close member (`SpecRunner.Console`)

- [x] 4.1 In `ProposeWorkflowRunner.ProcessCommentAsync`, call
      `session.CloseInputAsync(timeoutCts.Token)` immediately after
      `session.StartAsync(...)` returns and before draining
      `ReadEventsAsync`.

## 5. Continuous polling entry point (`SpecRunner.Console`)

- [x] 5.1 In `Program.cs`, after a successful connection test, register a
      `PosixSignalRegistration` for `SIGINT` and `SIGTERM` that cancels a
      `CancellationTokenSource` and sets `context.Cancel = true`.
- [x] 5.2 Replace the single `await proposeWorkflowRunner.RunOnceAsync()`
      call with a loop: while the token is not cancelled, run
      `RunOnceAsync()` to completion (not passing the shutdown token into
      it), catching and logging (`ILogger<Program>`, `Error` level) any
      exception it throws without exiting the loop; then, if the token is
      not cancelled, await `Task.Delay(options.PollingInterval,
      cancellationToken)` before the next iteration, swallowing the
      `OperationCanceledException` the delay throws on cancellation.
- [x] 5.3 After the loop exits (via shutdown signal), return exit code
      `0`.
- [x] 5.4 Keep the existing behavior for a failing connection test
      (non-zero exit, no polling loop entered) unchanged.

## 6. Tests

- [x] 6.1 Add a unit test for `ClaudeCliAgentSession.CloseInputAsync`:
      calling it while `Running` closes standard input on the underlying
      `IChildProcess` fake without killing it or changing `State`.
- [x] 6.2 Add a unit test that `CloseInputAsync` throws
      `InvalidOperationException` when called on a session in
      `NotStarted`, `Completed`, `Failed`, or `Stopped`.
- [x] 6.3 Add a unit test that a session whose input was closed still
      reaches `Completed`/`Failed` normally when the underlying fake
      process subsequently raises `Exited`.
- [x] 6.4 Add/update a `ClaudeCliAgentSession` launch-argument test
      asserting `--dangerously-skip-permissions` is present in the
      arguments passed to the process factory.
- [x] 6.5 Update `ProposeWorkflowRunnerTests`' fake session/assertions to
      confirm `CloseInputAsync` is called after `StartAsync` for the
      success, error, and timeout paths.
- [x] 6.6 Add a test (or refactor the polling loop into a small
      internally-testable method, e.g. `RunPollingLoopAsync(IProposeWorkflowRunner,
      TimeSpan, CancellationToken, ILogger)`, so it's unit-testable
      without spawning the real process) covering: a scan-pass exception
      is caught and logged without stopping the loop; a cancelled token
      stops the loop before the next scan pass; a cancelled token during
      the polling delay stops the loop promptly instead of waiting out
      the full interval.
- [x] 6.7 Verify `dotnet test SpecRunner/SpecRunner.sln` passes.

## 7. Documentation

- [x] 7.1 Update `SpecRunner/README.md` to describe the continuous
      polling behavior (interval, graceful shutdown via Ctrl+C/SIGTERM,
      that a failed scan pass no longer stops the process) in place of
      the single-scan-per-invocation description.

## 8. Verification

- [x] 8.1 Run `dotnet build SpecRunner/SpecRunner.sln` and confirm it
      succeeds with no errors.
- [x] 8.2 Run `dotnet test SpecRunner/SpecRunner.sln` and confirm all
      tests pass.
- [x] 8.3 If a test GitHub repository and PAT are available, run
      `SpecRunner.Console`, confirm it keeps running and logs repeated
      scan passes roughly `PollingInterval` apart, post a `/propose`
      comment during a run and confirm it's picked up without restarting
      the process, and confirm Ctrl+C stops it cleanly with exit code
      `0`.
