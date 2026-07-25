# startup-dependency-check

## Purpose

TBD - defines the startup dependency check that verifies the Claude CLI,
the OpenSpec CLI, and the GitHub repository connection are all available
before SpecRunner starts its polling loop.

## Requirements

### Requirement: Tool availability checker abstraction
`SpecRunner.Core` SHALL define an `ICliToolAvailabilityChecker` interface
with a single asynchronous method that takes an executable name and
returns a `ToolAvailabilityResult` carrying a `ToolAvailabilityStatus`
value (`Available`, `NotFound`, `LaunchFailed`) and a human-readable
message. `SpecRunner.Cli` SHALL provide an implementation that launches the
named executable with a `--version` argument and maps the outcome: the
process exits with code `0` maps to `Available`; the process starts and
exits with a non-zero code maps to `LaunchFailed`; the process fails to
start (e.g. the executable cannot be found) maps to `NotFound`.

#### Scenario: Tool availability checker is registered in DI
- **WHEN** the `SpecRunner.Console` DI container is inspected
- **THEN** `ICliToolAvailabilityChecker` SHALL resolve to the
  `SpecRunner.Cli` implementation

#### Scenario: Executable is available
- **WHEN** `CheckAsync` is called with the name of an executable that
  starts successfully and exits with code `0` for `--version`
- **THEN** the result SHALL have status `Available`

#### Scenario: Executable exists but fails to run cleanly
- **WHEN** `CheckAsync` is called with the name of an executable that
  starts but exits with a non-zero code for `--version`
- **THEN** the result SHALL have status `LaunchFailed` and the message
  SHALL include the exit code

#### Scenario: Executable cannot be found
- **WHEN** `CheckAsync` is called with an executable name that cannot be
  located/launched (e.g. not present on `PATH`)
- **THEN** the result SHALL have status `NotFound`

### Requirement: OpenSpec CLI executable is configurable
`SpecRunner.Core` SHALL define an `OpenSpecCliOptions` model exposing an
`Executable` value, defaulting to `"openspec"`. `SpecRunner.Console` SHALL
bind this model from configuration via the standard `IOptions` pattern
under an `OpenSpecCli` section.

#### Scenario: Executable defaults when not configured
- **WHEN** no `Executable` value is present in any configuration source
  under the `OpenSpecCli` section
- **THEN** `OpenSpecCliOptions.Executable` SHALL resolve to `"openspec"`

#### Scenario: Executable is overridden via configuration
- **WHEN** an `appsettings.json` file supplies an `OpenSpecCli` section
  with an `Executable` value
- **THEN** `IOptions<OpenSpecCliOptions>` resolved from the host SHALL
  expose that value unchanged

### Requirement: Startup dependency checker aggregates all required checks
`SpecRunner.Core` SHALL define an `IStartupDependencyChecker` interface
with a single asynchronous method that checks, in order, the Claude CLI
(using `CliAgentOptions.Executable` via `ICliToolAvailabilityChecker`), the
OpenSpec CLI (using `OpenSpecCliOptions.Executable` via
`ICliToolAvailabilityChecker`), and the GitHub repository connection (via
the existing `IRepositoryConnectionTester`), and returns an ordered list of
per-dependency results, each carrying a dependency name, a success flag,
and a human-readable message.

#### Scenario: Startup dependency checker is registered in DI
- **WHEN** the `SpecRunner.Console` DI container is inspected
- **THEN** `IStartupDependencyChecker` SHALL resolve to an implementation
  that composes `ICliToolAvailabilityChecker` and
  `IRepositoryConnectionTester`

#### Scenario: All dependencies present results in three successful checks
- **WHEN** the Claude CLI, the OpenSpec CLI, and the GitHub connection all
  check out successfully
- **THEN** the returned list SHALL contain three results, all marked
  successful, in the order Claude CLI, OpenSpec CLI, GitHub connection

#### Scenario: One dependency failing does not stop the others from being checked
- **WHEN** the Claude CLI check fails (status `NotFound` or
  `LaunchFailed`)
- **THEN** the OpenSpec CLI and GitHub connection checks SHALL still be
  performed and included in the returned results

### Requirement: Console reports each dependency and stops on any failure
`SpecRunner.Console` SHALL run the startup dependency checker once at
startup on every invocation, before starting the polling loop, log and
print the status of each dependency individually, and — if every
dependency check succeeded — proceed to start the polling loop. If any
dependency check failed, `SpecRunner.Console` SHALL log an error
identifying which dependency/dependencies failed and exit the process with
a non-zero code without starting the polling loop.

#### Scenario: All dependencies available allows startup to proceed
- **WHEN** the console app is run and every dependency check succeeds
- **THEN** the status of each of the three dependencies SHALL be
  printed/logged individually, and the polling loop SHALL start

#### Scenario: A missing dependency stops the application
- **WHEN** the console app is run and at least one dependency check fails
- **THEN** the status of each dependency SHALL be printed/logged
  individually, an error identifying the failed dependency/dependencies
  SHALL be logged, the polling loop SHALL NOT start, and the process SHALL
  exit with a non-zero code

#### Scenario: GitHub token is never included in dependency check output
- **WHEN** the console app reports dependency check results, regardless of
  outcome
- **THEN** the printed and logged output SHALL NOT contain the literal
  `GitHubToken` value
