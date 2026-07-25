## Context

`SpecRunner.Console/Program.cs` currently runs exactly one startup check: it
resolves `IRepositoryConnectionTester` (implemented in `SpecRunner.GitHub`)
and exits non-zero if the configured GitHub repository/token don't check
out. The Claude CLI (`CliAgentOptions.Executable`, default `"claude"`) is
only exercised later, when a workflow actually starts a CLI-agent session
(`SpecRunner.Cli.ClaudeCliAgentSession`), and the OpenSpec CLI is never
invoked or checked by SpecRunner itself — it's expected to be on `PATH` for
the Claude CLI to shell out to during a session. Neither failure mode is
caught before the polling loop starts.

`SpecRunner.Cli` already owns child-process launching (`IChildProcess`,
`IChildProcessFactory`, `SystemChildProcessFactory`), but those types are
`internal` to that assembly. `SpecRunner.Cli` only references
`SpecRunner.Core`; it does not reference `SpecRunner.GitHub`. Only
`SpecRunner.Console` references all of `SpecRunner.Cli`, `SpecRunner.Core`,
`SpecRunner.Git`, `SpecRunner.GitHub`, and `SpecRunner.State`. `Console`
already hosts orchestration classes that implement `SpecRunner.Core`
abstractions directly (`SpecFolderResolver`, `TasksFileReader`) rather than
living in a dedicated project per capability.

## Goals / Non-Goals

**Goals:**
- Verify, once per process startup and before the polling loop starts, that
  the Claude CLI executable, the OpenSpec CLI executable, and the GitHub
  repository connection are all usable.
- Report each of the three checks individually (name, status, message) via
  the existing Serilog logging convention and console output.
- Exit with a non-zero code and skip the polling loop if any check fails,
  matching the existing GitHub-only failure behavior.
- Make the OpenSpec CLI executable name configurable, mirroring
  `CliAgentOptions.Executable`.

**Non-Goals:**
- Validating that the Claude CLI or OpenSpec CLI are a specific minimum
  version — only that the configured executable can be located and
  launched successfully.
- Re-checking dependencies during the polling loop (this change is
  startup-only, matching the existing repository-connection check's
  once-per-run scope).
- Changing how the Claude CLI or OpenSpec CLI are actually invoked during
  workflow execution.

## Decisions

- **New `ICliToolAvailabilityChecker` abstraction in `SpecRunner.Core`,
  implemented in `SpecRunner.Cli`.** A single method takes an executable
  name and returns a `ToolAvailabilityResult` (`Available` / `NotFound` /
  `LaunchFailed`, plus a message). The `SpecRunner.Cli` implementation
  reuses the existing internal `IChildProcessFactory`/`IChildProcess`
  (via `InternalsVisibleTo`, already used for `SpecRunner.Cli`'s tests) to
  launch `<executable> --version`, wait for exit, and map the outcome:
  process starts and exits `0` → `Available`; process starts and exits
  non-zero → `LaunchFailed` (message includes exit code); the underlying
  process start throws (executable not found on `PATH`, e.g.
  `Win32Exception`/`FileNotFoundException`) → `NotFound`. Reusing the
  existing process infra avoids a second child-process abstraction; `
  --version` is a low-cost, side-effect-free probe supported by both the
  Claude CLI and the OpenSpec CLI.
  - *Alternative considered*: shell out to `where`/`which` to check `PATH`
    only. Rejected — it doesn't confirm the executable actually runs (e.g.
    a corrupt or non-executable file on `PATH`), and behaves differently
    across OSes, whereas actually launching the tool is what SpecRunner
    needs to have confidence in.

- **New `OpenSpecCliOptions` in `SpecRunner.Core.Configuration`**, bound
  from an `OpenSpecCli` configuration section, exposing `Executable`
  (default `"openspec"`). This mirrors `CliAgentOptions.Executable` instead
  of introducing a different configuration shape for the same kind of
  setting. The Claude CLI check reuses `CliAgentOptions.Executable` rather
  than duplicating it under a new option, since that's already the
  configured Claude executable used to start sessions.

- **New `IStartupDependencyChecker` abstraction in `SpecRunner.Core`,
  implemented as `StartupDependencyChecker` in `SpecRunner.Console`.** It
  composes `ICliToolAvailabilityChecker` (called twice, once per configured
  CLI executable) and the existing `IRepositoryConnectionTester`, returning
  an ordered `IReadOnlyList<DependencyCheckResult>` (name, success flag,
  message) for Claude CLI, OpenSpec CLI, then GitHub connection. It lives in
  `SpecRunner.Console` — the only project that already references
  `SpecRunner.Cli`, `SpecRunner.Core`, and `SpecRunner.GitHub` together —
  following the existing precedent of `SpecFolderResolver`/
  `TasksFileReader` implementing `Core` abstractions directly inside
  `Console` rather than adding a new project.
  - *Alternative considered*: a new `SpecRunner.Startup` project. Rejected
    as unnecessary indirection for one small orchestrating class with no
    reuse outside `Console`.

- **`Program.cs` replaces its direct `IRepositoryConnectionTester` call
  with `IStartupDependencyChecker.CheckAllAsync()`.** Each result is logged
  individually (`LogInformation` on success, `LogError` on failure) and
  printed to the console in the same "one line per dependency" shape the
  GitHub check already uses. If any result is unsuccessful, a final
  `LogError` summarizes which dependencies failed and the process returns a
  non-zero exit code before constructing the shutdown/polling-loop
  machinery. This preserves the current GitHub-failure exit behavior while
  adding the two new checks ahead of it.

## Risks / Trade-offs

- [`--version` isn't universally supported / has inconsistent exit codes
  across CLI tools] → Both the Claude CLI and OpenSpec CLI support
  `--version` with a zero exit code; if a future tool doesn't, the checker
  treats a non-zero exit as `LaunchFailed` (visible in the reported
  message) rather than silently passing.
- [Adding two subprocess launches to every startup increases startup
  latency slightly] → Both probes are short-lived, argument-less CLI
  invocations and run sequentially with the existing GitHub check, which
  is already a network round-trip; the added latency is small relative to
  that.
- [`IChildProcess`/`IChildProcessFactory` are `internal` to
  `SpecRunner.Cli`] → The new `ICliToolAvailabilityChecker` implementation
  is added inside `SpecRunner.Cli` itself (same assembly), so it uses these
  types directly without changing their visibility or adding new
  `InternalsVisibleTo` targets beyond what already exists for tests.
