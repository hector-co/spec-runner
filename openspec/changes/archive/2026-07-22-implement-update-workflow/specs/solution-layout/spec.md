## MODIFIED Requirements

### Requirement: Console entry point starts, tests the repository connection, and continuously polls for propose-workflow, implement-workflow, and update-workflow comments
The `SpecRunner.Console` project SHALL provide an executable entry point
that builds a generic host, loads configuration, configures Serilog
logging, and runs the repository connection test (`repository-connection`
capability) exactly once at startup. The process SHALL exit with a
non-zero exit code, without entering the polling loop, when the connection
test reports any status other than `Connected`. When the connection test
reports `Connected`, the entry point SHALL enter a loop that runs one
`propose-workflow` scan pass (`IProposeWorkflowRunner.RunOnceAsync`) to
completion, then one `implement-workflow` scan pass
(`IImplementWorkflowRunner.RunOnceAsync`) to completion, then one
`update-workflow` scan pass (`IUpdateWorkflowRunner.RunOnceAsync`) to
completion, waits `SpecRunnerOptions.PollingInterval`, and repeats. The
three scan passes SHALL run sequentially, never concurrently, since they
share the same local clone. Upon receiving a `SIGINT` (including Ctrl+C) or
`SIGTERM` shutdown signal, the process SHALL NOT abort a scan pass already
in progress; it SHALL let that pass finish, SHALL NOT start another
`propose-workflow`, `implement-workflow`, or `update-workflow` pass
afterward, and SHALL then exit with code `0`. A signal received while
waiting out `PollingInterval` SHALL end the wait immediately and proceed
directly to exit. An unhandled exception from any scan pass SHALL be logged
and SHALL NOT terminate the process or the loop, and SHALL NOT prevent the
other scan passes from running that cycle; the loop SHALL continue to its
next `PollingInterval` wait and subsequent scan passes.

#### Scenario: Running the console app with a working connection enters the polling loop
- **WHEN** the built `SpecRunner.Console` executable is run with
  configuration that resolves to a `Connected` repository connection status
- **THEN** the process SHALL start, load configuration, run the connection
  test, log/print the connected state, and begin repeating
  `propose-workflow` then `implement-workflow` then `update-workflow` scan
  passes separated by `PollingInterval`, without exiting on its own

#### Scenario: Running the console app with a failing connection exits immediately
- **WHEN** the built `SpecRunner.Console` executable is run with
  configuration that resolves to any repository connection status other
  than `Connected`
- **THEN** the process SHALL start, load configuration, run the connection
  test, log/print that status and its message, terminate with a non-zero
  exit code without throwing an unhandled exception, and SHALL NOT enter
  the polling loop or run any `propose-workflow`, `implement-workflow`, or
  `update-workflow` scan pass

#### Scenario: A scan pass with per-comment errors does not stop the loop
- **WHEN** the connection test reports `Connected` and a `propose-workflow`,
  `implement-workflow`, or `update-workflow` scan pass processes at least
  one comment that ends in an error outcome, but the scan pass itself
  completes without an unhandled exception
- **THEN** the process SHALL wait `PollingInterval` and run the next set of
  scan passes as normal

#### Scenario: An unhandled exception from one scan pass does not stop the others
- **WHEN** a `propose-workflow` scan pass throws an unhandled exception
  during a poll cycle
- **THEN** the process SHALL log the exception, SHALL still run the
  `implement-workflow` and `update-workflow` scan passes for that same
  cycle, SHALL NOT terminate, and SHALL wait `PollingInterval` before
  running the next set of scan passes

#### Scenario: A shutdown signal received while waiting exits promptly
- **WHEN** the process is waiting out `PollingInterval` between poll cycles
  and receives a `SIGINT` or `SIGTERM` signal
- **THEN** the process SHALL end the wait immediately, SHALL NOT start
  another `propose-workflow`, `implement-workflow`, or `update-workflow`
  pass, and SHALL exit with code `0` without throwing an unhandled
  exception

#### Scenario: A shutdown signal received mid-scan-pass lets the pass finish
- **WHEN** the process receives a `SIGINT` or `SIGTERM` signal while a
  `propose-workflow`, `implement-workflow`, or `update-workflow` scan pass
  is in progress
- **THEN** the process SHALL let the in-progress scan pass run to
  completion, SHALL NOT start another `propose-workflow`,
  `implement-workflow`, or `update-workflow` pass afterward, and SHALL then
  exit with code `0` without throwing an unhandled exception
