## Why

SpecRunner currently has no way to point at a real GitHub repository or
prove it can reach one: configuration only holds a raw owner/name pair with
no validation, there is no way to confirm the configured PAT actually grants
access before a workflow run starts, and the app has no structured logging
to record what happened when a run succeeds or fails. Before any git/GitHub
operation can be implemented, the app needs a configured repository URL +
PAT, a startup check that proves the connection works (and says why, when it
doesn't), and durable logs to diagnose failures.

## What Changes

- Replace `RepositoryOwner`/`RepositoryName` in `SpecRunnerOptions` with a
  single `RepositoryUrl` setting, so the repository is configured as one
  connection string alongside the existing `GitHubToken` (PAT). **BREAKING**:
  existing `appsettings.json`/environment configuration using
  `RepositoryOwner`/`RepositoryName` must migrate to `RepositoryUrl`.
- Add a repository connection test that runs when `SpecRunner.Console`
  executes: it calls the GitHub API for the repository derived from
  `RepositoryUrl`, authenticated with the configured PAT, and reports a
  connection state (e.g. connected, authentication failed, repository not
  found, network error, not configured) plus a human-readable message.
- Wire the connection test into the console entry point so the app prints
  and logs the connection state on every run and exits with a non-zero code
  when the connection check fails.
- Add Serilog as the logging framework: console sink and rolling file sink
  (max file size 1 MB, rolling to a new file on size limit) configured by
  default, with all application logging going through `Serilog.ILogger` /
  `Microsoft.Extensions.Logging` using Serilog's structured message-template
  conventions instead of string concatenation.
- Update `appsettings.json` with a `Serilog` configuration section
  (minimum level, console sink, rolling-file sink with the 1 MB limit) so
  logging is configuration-driven rather than hardcoded.
- Record the Serilog logging convention (console + 1 MB rolling file,
  structured templates) in `openspec/config.yaml` project context so future
  changes generate code consistent with it.

## Capabilities

### New Capabilities
- `repository-connection`: configuring a repository URL and PAT, testing
  connectivity/access to that repository on startup, and reporting a typed
  connection state and message.
- `structured-logging`: Serilog-based logging with console + size-limited
  rolling-file sinks by default, and structured (message-template) logging
  conventions used throughout the app.

### Modified Capabilities
- `app-configuration`: `SpecRunnerOptions` drops `RepositoryOwner` and
  `RepositoryName` in favor of a single `RepositoryUrl` setting used to
  derive the owner/repo for connection testing.
- `solution-layout`: the console entry point's startup behavior changes
  from "no git/GitHub operation, always exit 0" to "run the repository
  connection test and exit with a code reflecting its result."

## Impact

- `SpecRunner.Core`: `SpecRunnerOptions` (config shape), new
  `IRepositoryConnectionTester` abstraction and connection-result model.
- `SpecRunner.GitHub`: new implementation of `IRepositoryConnectionTester`
  calling the GitHub REST API over HTTPS with the configured PAT.
- `SpecRunner.Console`: `Program.cs` wires Serilog into the generic host and
  invokes the connection test at startup; `appsettings.json` gains
  `RepositoryUrl` and a `Serilog` section, loses `RepositoryOwner`/
  `RepositoryName`.
- `SpecRunner/Directory.Packages.props`: adds Serilog package versions
  (hosting integration, console sink, file sink, configuration sink) and an
  HTTP client / GitHub API dependency if one is introduced.
- `openspec/config.yaml`: project context gains the Serilog logging
  convention.
- `SpecRunner/README.md` and tests: updated to describe the new
  configuration fields and connection-test behavior.
