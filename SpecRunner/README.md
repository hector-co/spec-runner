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
    SpecRunner.Tests    - xUnit tests for Core and State
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
- `RepositoryOwner` / `RepositoryName` - the target GitHub repository.
- `LocalRepositoryPath` - local clone path used to derive the default state file
  location (`<LocalRepositoryPath>/.specrunner/state.json`).
- `BaseBranchName` - base branch used for PRs (defaults to `main`).
- `TaskTimeout` - per-task timeout, e.g. `"00:10:00"` for 10 minutes.

## Status

Git and GitHub operations are not implemented yet: `SpecRunner.Git` and
`SpecRunner.GitHub` register placeholder services that throw
`NotImplementedException`. The local JSON state store is fully implemented.
Running `SpecRunner.Console` starts the host, resolves all registered
services, and exits with code `0` without performing any git or GitHub
operation.

## Build and test

```
dotnet build SpecRunner/SpecRunner.sln
dotnet test SpecRunner/SpecRunner.sln
```
