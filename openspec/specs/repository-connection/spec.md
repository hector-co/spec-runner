# repository-connection

## Purpose

TBD - defines the repository connection test used to validate access to
the configured GitHub repository (URL and PAT) before SpecRunner performs
any git or GitHub operation.

## Requirements

### Requirement: Repository connection tester abstraction
`SpecRunner.Core` SHALL define an `IRepositoryConnectionTester` interface
with a single asynchronous method that tests access to the repository
configured via `SpecRunnerOptions.RepositoryUrl` and
`SpecRunnerOptions.GitHubToken`, and returns a `RepositoryConnectionResult`
carrying a `RepositoryConnectionStatus` value (`NotConfigured`,
`InvalidRepositoryUrl`, `Connected`, `AuthenticationFailed`,
`RepositoryNotFound`, `NetworkError`) and a human-readable message.
`SpecRunner.GitHub` SHALL provide an implementation that calls the GitHub
REST API using the configured PAT.

#### Scenario: Connection tester is registered in DI
- **WHEN** the `SpecRunner.Console` DI container is inspected
- **THEN** `IRepositoryConnectionTester` SHALL resolve to the
  `SpecRunner.GitHub` implementation

### Requirement: Connection test reports success for an accessible repository
The connection test SHALL return status `Connected` when `RepositoryUrl`
and `GitHubToken` are both configured and the GitHub REST API confirms the
PAT can access the repository.

#### Scenario: Valid URL and PAT with access
- **WHEN** `TestConnectionAsync` is called with a `RepositoryUrl` of
  `https://github.com/owner/repo` and a `GitHubToken` that has access to
  that repository
- **THEN** the GitHub REST API request `GET
  https://api.github.com/repos/owner/repo` SHALL be made with the token as
  a bearer credential, and on an HTTP `200` response the result SHALL have
  status `Connected`

### Requirement: Connection test distinguishes authentication failure from missing/inaccessible repository
The connection test SHALL map an HTTP `401` or `403` response to status
`AuthenticationFailed`, and an HTTP `404` response to status
`RepositoryNotFound`, each with a message describing which occurred.

#### Scenario: Invalid or expired PAT
- **WHEN** the GitHub REST API responds with HTTP `401` or `403` to the
  repository request
- **THEN** the result SHALL have status `AuthenticationFailed`

#### Scenario: Repository does not exist or PAT lacks access
- **WHEN** the GitHub REST API responds with HTTP `404` to the repository
  request
- **THEN** the result SHALL have status `RepositoryNotFound`

### Requirement: Connection test detects missing or malformed configuration before calling the network
If `RepositoryUrl` or `GitHubToken` is empty, the connection test SHALL
return status `NotConfigured` without making an HTTP request. If
`RepositoryUrl` is non-empty but is not a parseable
`https://github.com/{owner}/{repo}` URL, the connection test SHALL return
status `InvalidRepositoryUrl` without making an HTTP request.

#### Scenario: Repository URL not set
- **WHEN** `TestConnectionAsync` is called with an empty `RepositoryUrl`
- **THEN** the result SHALL have status `NotConfigured` and no HTTP request
  SHALL be made

#### Scenario: Token not set
- **WHEN** `TestConnectionAsync` is called with a non-empty `RepositoryUrl`
  and an empty `GitHubToken`
- **THEN** the result SHALL have status `NotConfigured` and no HTTP request
  SHALL be made

#### Scenario: Malformed repository URL
- **WHEN** `TestConnectionAsync` is called with `RepositoryUrl` set to a
  value that is not a `https://github.com/{owner}/{repo}` URL (e.g. an SSH
  URL or a non-GitHub host)
- **THEN** the result SHALL have status `InvalidRepositoryUrl` and no HTTP
  request SHALL be made

### Requirement: Connection test reports network failures distinctly
The connection test SHALL return status `NetworkError` with a message
describing the failure when the HTTP request to the GitHub REST API fails
due to a network-level error (DNS resolution failure, connection timeout,
TLS failure, or similar transport exception) rather than completing with
an HTTP response.

#### Scenario: GitHub API unreachable
- **WHEN** the HTTP request to the GitHub REST API throws a transport-level
  exception instead of completing with an HTTP response
- **THEN** the result SHALL have status `NetworkError`

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
