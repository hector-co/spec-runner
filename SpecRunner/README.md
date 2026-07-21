# SpecRunner

## Project layout

```
SpecRunner/
  SpecRunner.sln
  Directory.Build.props    - sets TargetFramework (net10.0) for every project
  Directory.Build.targets  - sets ImplicitUsings/Nullable for every project
  Directory.Packages.props - central NuGet package version management
  src/
    SpecRunner.Core     - domain models and service abstractions (no project deps)
    SpecRunner.Git      - git operations (placeholder implementation only)
    SpecRunner.GitHub   - GitHub API operations (placeholder implementation only)
    SpecRunner.State    - JSON-file-backed local state store
    SpecRunner.Console  - entry point; composes the above via generic host + DI
  tests/
    SpecRunner.Tests    - xUnit tests for Core, GitHub, and State
```

`SpecRunner.Console` references `SpecRunner.Core`, `SpecRunner.Git`,
`SpecRunner.GitHub`, and `SpecRunner.State`. `SpecRunner.Git`,
`SpecRunner.GitHub`, and `SpecRunner.State` each reference only
`SpecRunner.Core` and do not reference each other.

## Configuration

`SpecRunner.Console` binds `SpecRunnerOptions` from configuration (`appsettings.json`,
environment variables, and user secrets in Development) under the `SpecRunner` section:

- `GitHubToken` - GitHub personal access token. Do not commit a real value; set it via
  an environment variable or `dotnet user-secrets set SpecRunner:GitHubToken <token>`
  from `src/SpecRunner.Console`.
- `RepositoryUrl` - the target GitHub repository as an HTTPS URL, e.g.
  `https://github.com/owner/repo` (a trailing `.git` is also accepted). Owner/repo are
  derived from this URL; SSH URLs and non-GitHub hosts are not supported.
- `LocalRepositoryPath` - local clone path used to derive the default state file
  location (`<LocalRepositoryPath>/.specrunner/state.json`).
- `BaseBranchName` - base branch used for PRs (defaults to `main`).
- `TaskTimeout` - per-task timeout, e.g. `"00:10:00"` for 10 minutes.

## Status

Git and GitHub operations are not implemented yet beyond the connection check:
`SpecRunner.Git` and `SpecRunner.GitHub`'s `IGitService`/`IGitHubService` register
placeholder services that throw `NotImplementedException`. The local JSON state store
is fully implemented.

On every run, `SpecRunner.Console` starts the host and tests whether the configured
`RepositoryUrl` + `GitHubToken` can reach the target repository via the GitHub REST API
(`IRepositoryConnectionTester`). It logs and prints the resulting status (`NotConfigured`,
`InvalidRepositoryUrl`, `Connected`, `AuthenticationFailed`, `RepositoryNotFound`, or
`NetworkError`) and a message, then exits with code `0` when the status is `Connected`
and `1` for any other status. `GitHubToken` is never included in printed or logged output.

## Logging

`SpecRunner.Console` logs through Serilog (`ILogger`/`ILogger<T>` with structured
message-template calls, e.g. `logger.LogInformation("... {RepositoryUrl}", url)`), wired
into the generic host via `UseSerilog`/`ReadFrom.Configuration`. By default, sinks and
levels come from the `Serilog` section of `appsettings.json` and include:

- Console - all log output also goes to stdout.
- Rolling file - written under `logs/` (relative to the working directory), rolling to a
  new file once the current one reaches 1 MB (`fileSizeLimitBytes: 1048576`,
  `rollOnFileSizeLimit: true`). Older log files are not deleted automatically.

## Build and test

```
dotnet build SpecRunner/SpecRunner.sln
dotnet test SpecRunner/SpecRunner.sln
```
