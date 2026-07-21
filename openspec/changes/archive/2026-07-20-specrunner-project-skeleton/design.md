## Context

There is no code in this repository yet. This change only lays down the
.NET 10 solution/project skeleton, the configuration model, and the local
state-store schema that later changes (the actual propose/update/implement/
archive comment-processing workflow) will build on. The layout needs to
separate "talks to GitHub", "talks to git", and "tracks local state" so
each can be developed, tested, and mocked independently, since the real
workflow logic (issue/PR comment polling and dispatch) is intentionally
out of scope here and will be a future change.

## Goals / Non-Goals

**Goals:**
- Stand up a buildable `SpecRunner.sln` with clearly separated projects and
  a console entry point that starts, loads configuration, and exits
  cleanly.
- Define a configuration model (PAT, target repo, base branch name, task
  timeout) that binds from `appsettings.json` / environment variables /
  user secrets, so no secrets are hardcoded.
- Define the local state-store schema (entities + storage interface) that
  associates issues, PRs, comments, and spec/change names.
- Define service interfaces for git and GitHub operations so future
  changes implement against a fixed contract instead of inventing one
  mid-feature.
- Provide a working spec-name resolver utility (issue number + sanitized,
  dashed, lower-cased issue name), since it is pure and has no external
  dependency.
- Add a test project wired into the solution with one smoke test.

**Non-Goals:**
- Implementing any git or GitHub operation (branch/commit/push/pull, PR
  create/comment/ready-for-review). These stay as unimplemented interface
  contracts in this change.
- Implementing the comment-scanning workflow / state machine
  (propose/update/implement/archive dispatch logic).
- Choosing or wiring a specific GitHub SDK (e.g. Octokit) beyond adding the
  package reference needed to compile the interface layer, if any.
- CI/CD, packaging, or distribution concerns.

## Decisions

- **Solution layout**: `/SpecRunner/SpecRunner.sln` with `src/` and
  `tests/` subfolders:
  - `src/SpecRunner.Console` — entry point (`Program.cs`), generic host
    bootstrap, DI composition root, configuration wiring. Depends on all
    other `src` projects.
  - `src/SpecRunner.Core` — domain models (`TrackedIssue`, `TrackedPr`,
    `TrackedComment`, `SpecAssociation`, config option POCOs) and service
    abstractions (`IGitService`, `IGitHubService`, `IStateStore`,
    `ISpecNameResolver`). No dependency on other `src` projects, so it can
    be referenced everywhere without cycles.
  - `src/SpecRunner.Git` — future home of the git-operations
    implementation. This change adds the project and an unimplemented
    `IGitService` stub only.
  - `src/SpecRunner.GitHub` — future home of the GitHub API implementation
    (PAT auth, PR/comment operations). This change adds the project and an
    unimplemented `IGitHubService` stub only.
  - `src/SpecRunner.State` — local state-store implementation. Unlike Git
    and GitHub, this change provides a real (if minimal) JSON-file-backed
    `IStateStore` implementation, since it is self-contained storage
    plumbing with no GitHub/git side effects and the schema needs a
    concrete shape to be validated against.
  - `tests/SpecRunner.Tests` — xUnit project referencing `SpecRunner.Core`
    and `SpecRunner.State`, with one smoke test per project plus coverage
    for the spec-name resolver and the JSON state store.
  - Rationale for splitting Git/GitHub/State into separate projects rather
    than one "Infrastructure" project: they have different external
    dependencies (process/CLI vs. HTTP/GitHub SDK vs. filesystem) and
    different testing strategies, and keeping them separate avoids a
    console app that must reference GitHub SDK types just to run git
    commands or read local state.

- **Why interfaces now, implementations later for Git/GitHub**: the
  proposal explicitly scopes this change to structure only. Defining
  `IGitService` / `IGitHubService` in `SpecRunner.Core` lets the console
  app's DI composition and the state/config plumbing be built and tested
  today, and lets a future change implement each service independently
  without touching the composition root's shape again.

- **Configuration**: use `Microsoft.Extensions.Hosting` (`Host.CreateApplicationBuilder`)
  with the standard `appsettings.json` + `appsettings.{Environment}.json` +
  environment variable + user-secrets provider chain, bound via
  `IOptions<SpecRunnerOptions>`. `SpecRunnerOptions` holds:
  `GitHubToken`, `RepositoryOwner`, `RepositoryName`, `LocalRepositoryPath`,
  `BaseBranchName` (default `main`), `TaskTimeout` (`TimeSpan`,
  configurable). The PAT is read from configuration (environment variable
  or user-secrets in development) and never hardcoded or logged.
  Alternative considered: custom `.ini`/`.env` parsing — rejected in favor
  of the standard `IConfiguration` pipeline, which is already
  environment/secret-store aware and idiomatic for .NET console apps.

- **State store schema and storage mechanism**: a single JSON file (path
  configurable, defaulting to `.specrunner/state.json` under the local
  repository path) holding a flat list of association records. Each record
  captures: issue number, PR number (nullable until a PR exists), spec/
  change name, and a list of tracked comments (comment id, comment kind,
  processing status: pending/working/done/error). `IStateStore` exposes
  `LoadAsync`/`SaveAsync` plus lookup-by-issue and lookup-by-PR helper
  methods; the JSON file is the entire mechanism (no database) since a
  single local process handles one repository at a time. Alternative
  considered: SQLite — rejected as unnecessary weight for single-process,
  low-volume, single-file state.

- **Spec-name resolver**: `ISpecNameResolver.Resolve(int issueNumber,
  string issueTitle)` returns `"{issueNumber}-{sanitized-title}"`, lower-
  casing the title, replacing whitespace runs with single dashes, and
  stripping characters invalid in a Windows/POSIX folder name. Implemented
  fully in this change (in `SpecRunner.Core`) since it is a pure function
  with no I/O and is needed to validate the naming convention early.

- **Project/package choices**: target `net10.0`, nullable reference types
  and implicit usings enabled across all projects, xUnit for tests. No
  GitHub SDK package is pinned yet beyond what's needed for the
  `IGitHubService` interface to compile (none — the interface uses only
  `SpecRunner.Core` model types), deferring the SDK choice to the change
  that implements `SpecRunner.GitHub`.

- **Centralized build configuration**: `SpecRunner/Directory.Build.targets`
  sets `ImplicitUsings` and `Nullable` once for every project under
  `SpecRunner/src` and `SpecRunner/tests`. `TargetFramework` is set in a
  separate `SpecRunner/Directory.Build.props` instead of `.targets`,
  because NuGet's restore pass resolves `TargetFramework` from
  `Directory.Build.props` (imported before `Sdk.props`) and does not see
  `Directory.Build.targets` (imported after the project body) — putting
  `TargetFramework` only in `.targets` made `dotnet build`/`dotnet restore`
  fail with "Invalid framework identifier ''" before any project-level
  property could apply. `SpecRunner/Directory.Packages.props` turns on
  `ManagePackageVersionsCentrally` and lists a `PackageVersion` for every
  NuGet package referenced anywhere in the solution
  (`Microsoft.Extensions.Hosting`, `Microsoft.Extensions.DependencyInjection`,
  `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`,
  `coverlet.collector`). Individual `.csproj` files keep only
  project-specific properties (e.g. `OutputType`, `UserSecretsId`) and
  `PackageReference`/`ProjectReference` items without a `Version`
  attribute. This removes the drift risk of the same package resolving to
  different versions in different projects and matches the standard MSBuild
  convention (`Directory.Build.props`/`.targets` auto-import into every
  project under the directory tree). `Directory.Build.targets` was used
  (import-after-project, so its property values win over anything a
  project sets) rather than `Directory.Build.props` (import-before-project,
  where a project's own values would silently win instead), so the shared
  `TargetFramework`/`ImplicitUsings`/`Nullable` values are enforced
  consistently rather than quietly overridable per project.

## Risks / Trade-offs

- [Defining `IGitService`/`IGitHubService` now, before any real
  implementation exists] → the interfaces may need to change shape once
  real GitHub/git constraints are discovered. Mitigated by keeping them
  minimal (method signatures only, no assumed internal behavior) and
  treating them as revisable in the change that implements them.
- [JSON-file state store has no concurrency control] → acceptable because
  the app is designed to run one instance per local repository clone;
  documented as a constraint rather than solved with file locking in this
  change.
- [Splitting Git/GitHub/State into three projects adds solution
  boilerplate compared to one Infrastructure project] → accepted for
  clearer ownership and dependency boundaries; revisit only if it proves
  to add friction once real implementations land.

