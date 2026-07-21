## MODIFIED Requirements

### Requirement: Console entry point starts, tests the repository connection, and exits accordingly
The `SpecRunner.Console` project SHALL provide an executable entry point
that builds a generic host, loads configuration, configures Serilog
logging, and runs the repository connection test (`repository-connection`
capability) exactly once per invocation. The process SHALL exit with code
`0` when the connection test reports status `Connected`, and with a
non-zero exit code otherwise, without performing any other git or GitHub
operation.

#### Scenario: Running the console app with a working connection
- **WHEN** the built `SpecRunner.Console` executable is run with
  configuration that resolves to a `Connected` repository connection status
- **THEN** the process SHALL start, load configuration, run the connection
  test, log/print the connected state, and terminate with exit code `0`
  without throwing an unhandled exception

#### Scenario: Running the console app with a failing connection
- **WHEN** the built `SpecRunner.Console` executable is run with
  configuration that resolves to any repository connection status other
  than `Connected`
- **THEN** the process SHALL start, load configuration, run the connection
  test, log/print that status and its message, and terminate with a
  non-zero exit code without throwing an unhandled exception
