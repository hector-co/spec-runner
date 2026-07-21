## MODIFIED Requirements

### Requirement: Console entry point starts, tests the repository connection, and runs one propose-workflow scan pass
The `SpecRunner.Console` project SHALL provide an executable entry point
that builds a generic host, loads configuration, configures Serilog
logging, and runs the repository connection test (`repository-connection`
capability) exactly once per invocation. When the connection test reports
status `Connected`, the entry point SHALL additionally run exactly one
`propose-workflow` scan pass (`IProposeWorkflowRunner.RunOnceAsync`)
before exiting. The process SHALL exit with code `0` when the connection
test reports status `Connected` and the scan pass completes without an
unhandled exception (individual comment failures are reported on GitHub
per `propose-workflow`, not via process exit code), and with a non-zero
exit code when the connection test reports any status other than
`Connected`, or when the scan pass itself throws an unhandled exception.

#### Scenario: Running the console app with a working connection
- **WHEN** the built `SpecRunner.Console` executable is run with
  configuration that resolves to a `Connected` repository connection
  status
- **THEN** the process SHALL start, load configuration, run the
  connection test, run one `propose-workflow` scan pass, log/print the
  connected state, and terminate with exit code `0` without throwing an
  unhandled exception

#### Scenario: Running the console app with a failing connection
- **WHEN** the built `SpecRunner.Console` executable is run with
  configuration that resolves to any repository connection status other
  than `Connected`
- **THEN** the process SHALL start, load configuration, run the
  connection test, log/print that status and its message, terminate with
  a non-zero exit code without throwing an unhandled exception, and SHALL
  NOT run a `propose-workflow` scan pass

#### Scenario: A scan pass with per-comment errors still exits zero
- **WHEN** the connection test reports `Connected` and the
  `propose-workflow` scan pass processes at least one comment that ends in
  an error outcome, but the scan pass itself completes without an
  unhandled exception
- **THEN** the process SHALL still terminate with exit code `0`
