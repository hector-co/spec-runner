## 1. Solution scaffolding

- [x] 1.1 Create `/SpecRunner/SpecRunner.sln` targeting `net10.0`, with
      `src/` and `tests/` folder structure.
- [x] 1.2 Create `src/SpecRunner.Core` class library project (nullable +
      implicit usings enabled) and add it to the solution.
- [x] 1.3 Create `src/SpecRunner.Git` class library project and add it to
      the solution, referencing `SpecRunner.Core`.
- [x] 1.4 Create `src/SpecRunner.GitHub` class library project and add it
      to the solution, referencing `SpecRunner.Core`.
- [x] 1.5 Create `src/SpecRunner.State` class library project and add it
      to the solution, referencing `SpecRunner.Core`.
- [x] 1.6 Create `src/SpecRunner.Console` executable project and add it to
      the solution, referencing `SpecRunner.Core`, `SpecRunner.Git`,
      `SpecRunner.GitHub`, and `SpecRunner.State`.
- [x] 1.7 Create `tests/SpecRunner.Tests` xUnit project and add it to the
      solution, referencing `SpecRunner.Core` and `SpecRunner.State`.
- [x] 1.8 Verify `dotnet build SpecRunner/SpecRunner.sln` succeeds with no
      errors.

## 2. Core domain models and service abstractions

- [x] 2.1 Add `SpecRunnerOptions` to `SpecRunner.Core` with
      `GitHubToken`, `RepositoryOwner`, `RepositoryName`,
      `LocalRepositoryPath`, `BaseBranchName` (default `"main"`), and
      `TaskTimeout` (`TimeSpan`).
- [x] 2.2 Add tracked-state record types to `SpecRunner.Core`
      (`TrackedIssue`, `TrackedPr`, `TrackedComment`, `CommentStatus`
      enum) matching the schema in `specs/state-store-schema/spec.md`.
- [x] 2.3 Add `IStateStore` interface to `SpecRunner.Core` with
      `LoadAsync`, `SaveAsync`, `FindByIssueNumberAsync`, and
      `FindByPrNumberAsync`.
- [x] 2.4 Add `ISpecNameResolver` interface and a default implementation
      in `SpecRunner.Core` that formats
      `{issue-number}-{sanitized-issue-title}` per the naming convention.
- [x] 2.5 Add `IGitService` interface to `SpecRunner.Core` covering create
      branch, switch branch, commit, push, and pull.
- [x] 2.6 Add `IGitHubService` interface to `SpecRunner.Core` covering
      create PR, create draft PR, read PR comments, write PR comments, and
      mark PR ready for review.

## 3. State store implementation

- [x] 3.1 Implement a JSON-file-backed `IStateStore` in
      `SpecRunner.State`, using a configurable file path (default
      `.specrunner/state.json` under the local repository path).
- [x] 3.2 Ensure the JSON implementation creates the file/directory on
      first save if it does not exist, and returns an empty state on load
      if the file is missing.

## 4. Placeholder Git and GitHub implementations

- [x] 4.1 Add a placeholder `IGitService` implementation in
      `SpecRunner.Git` where every member throws
      `NotImplementedException`.
- [x] 4.2 Add a placeholder `IGitHubService` implementation in
      `SpecRunner.GitHub` where every member throws
      `NotImplementedException`.

## 5. Console host and configuration wiring

- [x] 5.1 Implement `Program.cs` in `SpecRunner.Console` using
      `Host.CreateApplicationBuilder`, loading `appsettings.json`,
      `appsettings.{Environment}.json`, environment variables, and user
      secrets.
- [x] 5.2 Bind `SpecRunnerOptions` via `IOptions<SpecRunnerOptions>` and
      register `ISpecNameResolver`, `IStateStore`, `IGitService`, and
      `IGitHubService` in the DI container.
- [x] 5.3 Add an `appsettings.json` with placeholder/empty values for all
      `SpecRunnerOptions` fields (no real secrets committed).
- [x] 5.4 Ensure the host starts, resolves all registered services, and
      exits with code `0` when run with no action requested.

## 6. Tests

- [x] 6.1 Add a smoke test verifying the DI container resolves
      `IStateStore`, `ISpecNameResolver`, `IGitService`, and
      `IGitHubService` without error.
- [x] 6.2 Add unit tests for `ISpecNameResolver` covering: spaces replaced
      with dashes, lower-casing, and stripping of invalid folder-name
      characters.
- [x] 6.3 Add unit tests for the JSON `IStateStore` implementation
      covering save/load round-trip, lookup by issue number, and lookup by
      PR number.
- [x] 6.4 Verify `dotnet test SpecRunner/SpecRunner.sln` passes.

## 7. Documentation

- [x] 7.1 Add a short `SpecRunner/README.md` describing the project
      layout, how to configure `SpecRunnerOptions` locally, and that
      git/GitHub operations are not yet implemented in this change.
