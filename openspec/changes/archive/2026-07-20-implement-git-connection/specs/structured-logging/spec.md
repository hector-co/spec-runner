## ADDED Requirements

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
