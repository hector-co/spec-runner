## MODIFIED Requirements

### Requirement: Console reports connection state on every run
`SpecRunner.Console` SHALL run the repository connection test once at
startup on every invocation, as one of the checks performed by the
startup dependency checker (see `startup-dependency-check`), log the
resulting status and message, and print a summary of the connection state
to the console alongside the other dependency check results. Overall, the
process SHALL exit with code `0` only when the connection status is
`Connected` and every other startup dependency check also succeeded, and
SHALL exit with a non-zero code if the connection status is anything other
than `Connected`, regardless of the outcome of the other checks.

#### Scenario: Successful run exits zero
- **WHEN** the console app is run with a valid `RepositoryUrl` and
  `GitHubToken` that resolve to status `Connected`, and the Claude CLI and
  OpenSpec CLI checks also succeed
- **THEN** the process SHALL print/log the connected state and exit with
  code `0`

#### Scenario: Failed connection exits non-zero
- **WHEN** the console app is run with configuration that resolves to any
  status other than `Connected`
- **THEN** the process SHALL print/log that status and its message,
  SHALL NOT start the polling loop, and SHALL exit with a non-zero code

#### Scenario: PAT is never included in printed or logged output
- **WHEN** the console app reports the connection state, regardless of
  outcome
- **THEN** the printed and logged output SHALL NOT contain the literal
  `GitHubToken` value
