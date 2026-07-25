## Why

SpecRunner depends on the Claude CLI, the OpenSpec CLI, and a working GitHub
connection to do anything useful. Today only the GitHub connection is
verified at startup (inline in `Program.cs`); if the `claude` or `openspec`
executables are missing or misconfigured, SpecRunner starts anyway and only
fails later, mid-workflow, with a less obvious error. Checking all required
tools up front gives an operator a single, clear signal about what's wrong
before any GitHub/git/CLI-agent work is attempted.

## What Changes

- Add a startup dependency check that verifies, on every run, before the
  polling loop starts:
  - The Claude CLI executable (`CliAgentOptions.Executable`) can be located
    and launched.
  - The OpenSpec CLI executable can be located and launched.
  - The GitHub repository connection (existing `IRepositoryConnectionTester`)
    succeeds.
- Print and log the status of each of the three dependencies individually at
  startup (name + status + message), not just an aggregate result.
- If any dependency check fails, log an error identifying which
  dependency/dependencies failed and exit the process with a non-zero code
  without starting the polling loop. **BREAKING**: this replaces the current
  inline GitHub-only startup check in `Program.cs` with the new aggregate
  check; overall exit-code behavior on GitHub failure is unchanged, but a
  missing/broken Claude or OpenSpec CLI now also stops startup where
  previously it would not have been checked at all.
- Add configuration for the OpenSpec CLI executable name/path, following the
  same pattern as `CliAgentOptions.Executable` for the Claude CLI.

## Capabilities

### New Capabilities
- `startup-dependency-check`: defines the `IStartupDependencyChecker`
  abstraction and `SpecRunner.Cli`/`SpecRunner.Console` wiring that checks
  Claude CLI availability, OpenSpec CLI availability, and the GitHub
  connection at startup, reports each individually, and stops the
  application with a non-zero exit code if any check fails.

### Modified Capabilities
- `repository-connection`: the "Console reports connection state on every
  run" requirement is superseded — the GitHub connection test is still run
  once per startup with the same status/exit-code semantics, but it is now
  invoked as one of three checks performed by the startup dependency
  checker, and its result is reported alongside the Claude/OpenSpec CLI
  checks rather than printed/logged on its own.

## Impact

- `SpecRunner.Core`: new `IStartupDependencyChecker` abstraction, dependency
  check result model, and configuration model for the OpenSpec CLI
  executable.
- `SpecRunner.Cli`: new implementation that probes the Claude CLI and
  OpenSpec CLI executables using the existing child-process infrastructure.
- `SpecRunner.Console`: `Program.cs` startup sequence changes to call the
  new checker instead of `IRepositoryConnectionTester` directly.
- No changes to `SpecRunner.Git`, `SpecRunner.GitHub` (implementation), or
  `SpecRunner.State`.

## Assumptions

- Tool availability is determined by attempting to launch the configured
  executable with a lightweight flag (`--version`) and treating "process
  starts and exits with code 0" as available; a failure to start the
  process (executable not found) is reported distinctly from the process
  starting but exiting non-zero.
- The OpenSpec CLI executable defaults to `"openspec"` (resolved via PATH),
  matching how `CliAgentOptions.Executable` defaults to `"claude"`.
- "Close the application" means exiting the process with a non-zero exit
  code before the polling loop starts, consistent with the existing
  GitHub-only startup check's behavior on failure.
- Dependencies are checked and reported in the order given in the request:
  Claude CLI, OpenSpec CLI, GitHub connection.
- The `--version` probe processes for the Claude CLI and OpenSpec CLI are
  launched with the current working directory (`Directory.GetCurrentDirectory()`)
  rather than `SpecRunnerOptions.LocalRepositoryPath`, since a version check
  doesn't need the repository clone to exist and shouldn't fail startup if
  it doesn't yet.
- The console line format for each dependency result is
  `"{Name}: {OK|FAILED} - {Message}"`, replacing the previous
  `"Repository connection: {Status} - {Message}"` line, since
  `DependencyCheckResult` exposes a success flag rather than a
  connection-specific status enum.
- Tasks 6.1–6.3 (test suite run and manual console verification) were
  verified manually by the requester outside of this automated run and are
  marked complete on that basis.
