# structured-logging

## Purpose

TBD - defines Serilog as SpecRunner's logging framework, its default
sinks, and the rule that secret configuration values never appear in log
output.

## Requirements

### Requirement: Serilog is the application's logging framework
`SpecRunner.Console` SHALL configure Serilog as the logging provider for
the generic host, replacing/augmenting the default `Microsoft.Extensions.Logging`
provider so that all log output — from the host, from
`IRepositoryConnectionTester`, and from any other component — flows through
Serilog sinks. Application code SHALL log via `ILogger`/`ILogger<T>` using
Serilog's structured message-template style (e.g.
`logger.LogInformation("Testing connection to {RepositoryUrl}", url)`)
rather than pre-formatted/concatenated strings.

#### Scenario: Host logging flows through Serilog
- **WHEN** `SpecRunner.Console` starts
- **THEN** `Host.CreateApplicationBuilder` SHALL be configured with
  `UseSerilog`, and log entries produced anywhere in the app during that
  run SHALL be emitted through the configured Serilog sinks

### Requirement: Console and rolling file sinks enabled by default
Serilog SHALL be configured, by default, with a console sink and a rolling
file sink. The file sink SHALL roll to a new file once the current file
reaches a maximum size of 1 MB (`fileSizeLimitBytes: 1048576`,
`rollOnFileSizeLimit: true`). Sink and level configuration SHALL be read
from the `Serilog` section of `appsettings.json` via
`Serilog.Settings.Configuration`, not hardcoded in `Program.cs`.

#### Scenario: Default configuration logs to console and file
- **WHEN** `SpecRunner.Console` runs with the default `appsettings.json`
  `Serilog` section unmodified
- **THEN** log output SHALL appear both on the console and in a log file
  under the configured log directory

#### Scenario: File sink rolls at 1 MB
- **WHEN** the active log file reaches `1048576` bytes
- **THEN** subsequent log entries SHALL be written to a new log file rather
  than growing the current file past that size

### Requirement: Logged output never contains secret configuration values
Log entries produced by `SpecRunner.Console` and its services SHALL NOT
contain the literal value of `SpecRunnerOptions.GitHubToken`, whether
logged directly or as part of a serialized options object.

#### Scenario: Options are not logged wholesale
- **WHEN** any component logs information derived from `SpecRunnerOptions`
- **THEN** the log entry SHALL reference individual non-secret fields (e.g.
  `RepositoryUrl`) rather than serializing the entire options object

### Requirement: Step flows log an Information-level start message
Each workflow runner (`ProposeWorkflowRunner`, `ImplementWorkflowRunner`, `UpdateWorkflowRunner`, `FinalizeWorkflowRunner`) SHALL log one Information-level message at the start of processing an eligible comment, identifying the workflow and the associated issue number (propose) or PR number (implement/update/finalize).

#### Scenario: Propose flow logs its issue number on start
- **WHEN** `ProposeWorkflowRunner` begins processing an eligible `/propose`
  comment for issue #42
- **THEN** an Information-level log entry SHALL be emitted that includes
  issue number 42 before any git, CLI agent, or GitHub step for that
  comment runs

#### Scenario: Implement/update/finalize flows log their PR number on start
- **WHEN** `ImplementWorkflowRunner`, `UpdateWorkflowRunner`, or
  `FinalizeWorkflowRunner` begins processing an eligible comment for PR
  #17
- **THEN** an Information-level log entry SHALL be emitted that includes PR
  number 17 before any git, CLI agent, or GitHub step for that comment runs

### Requirement: In-progress indicator during the CLI agent session wait
The workflow runner SHALL log an Information-level "still in progress" message every 5 seconds while a step flow is awaiting its CLI agent session's event stream (the step bounded only by the configured task timeout, not by a fixed short duration), until the session reaches a terminal state or the flow's timeout/cancellation fires. The indicator SHALL stop as soon as the wait completes, and SHALL NOT continue ticking after the step flow has moved on to subsequent steps.

#### Scenario: Long-running CLI agent session produces periodic indicators
- **WHEN** a step flow's CLI agent session takes 17 seconds to reach a
  terminal state
- **THEN** at least 3 Information-level "still in progress" log entries
  SHALL be emitted during that wait, spaced approximately 5 seconds apart

#### Scenario: Indicator stops once the session completes
- **WHEN** the CLI agent session reaches a terminal state and
  `ProcessCommentAsync` proceeds to the git commit/push step
- **THEN** no further "still in progress" log entries SHALL be emitted for
  that step flow

### Requirement: Debug-level start/finish detail for individual steps
Each individual step within a step flow (git sync operations, prompt rendering, CLI agent session start/close, git commit, git push, tasks-file read, and GitHub description/title/ready-for-review updates) SHALL log a Debug-level message immediately before it starts and a Debug-level message immediately after it completes. These Debug-level messages SHALL be distinct from the Information-level step-flow-start and in-progress messages — no step's start/finish detail SHALL be duplicated at Information level, and the Information-level messages SHALL NOT be duplicated at Debug level.

#### Scenario: Individual step logs start and finish at Debug level
- **WHEN** a step flow executes its git push step
- **THEN** a Debug-level "starting" log entry SHALL be emitted immediately
  before the push, and a Debug-level "finished" log entry SHALL be emitted
  immediately after it completes successfully

#### Scenario: A step that throws does not log a false completion
- **WHEN** an individual step (e.g. the CLI agent session) throws an
  exception
- **THEN** the Debug-level "finished" message for that step SHALL NOT be
  logged, while the existing Warning/Error-level timeout/failure logging
  for the overall step flow SHALL still occur unchanged
