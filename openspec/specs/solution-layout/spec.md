# solution-layout

## Purpose

TBD - defines the SpecRunner .NET solution structure, project
responsibility separation, entry point behavior, test wiring, and
centralized build configuration.

## Requirements

### Requirement: SpecRunner solution structure
The repository SHALL contain a `SpecRunner.sln` solution located under a
top-level `/SpecRunner` folder, with source projects under `SpecRunner/src`
and test projects under `SpecRunner/tests`.

#### Scenario: Solution builds from a clean checkout
- **WHEN** `dotnet build SpecRunner/SpecRunner.sln` is run against a clean
  checkout of the repository
- **THEN** the build SHALL succeed with no compilation errors, using
  target framework `net10.0` for every project in the solution

### Requirement: Project responsibility separation
The solution SHALL separate concerns into distinct projects: a console
entry point, a core project holding domain models and service
abstractions, a git-operations project, a GitHub-operations project, and a
local state-store project. Each project SHALL depend only on
`SpecRunner.Core` and, in the case of the console project, on the other
`src` projects — no circular project references SHALL exist.

#### Scenario: Console project composes the other projects
- **WHEN** the solution is inspected for project references
- **THEN** `SpecRunner.Console` SHALL reference `SpecRunner.Core`,
  `SpecRunner.Git`, `SpecRunner.GitHub`, and `SpecRunner.State`, and none
  of `SpecRunner.Git`, `SpecRunner.GitHub`, or `SpecRunner.State` SHALL
  reference each other

#### Scenario: Core project has no outbound project dependencies
- **WHEN** the solution is inspected for project references
- **THEN** `SpecRunner.Core` SHALL NOT reference any other `SpecRunner.*`
  project

### Requirement: Console entry point starts and exits cleanly
The `SpecRunner.Console` project SHALL provide an executable entry point
that builds a generic host, loads configuration, and exits with a zero
exit code when no work is available, without performing any git or GitHub
operation.

#### Scenario: Running the console app with no configured action
- **WHEN** the built `SpecRunner.Console` executable is run
- **THEN** the process SHALL start, load configuration, and terminate with
  exit code `0` without throwing an unhandled exception

### Requirement: Test project wired into the solution
The solution SHALL include a `SpecRunner.Tests` project referencing
`SpecRunner.Core` and `SpecRunner.State`, registered in `SpecRunner.sln`,
containing at least one passing smoke test per referenced project.

#### Scenario: Test project runs via dotnet test
- **WHEN** `dotnet test SpecRunner/SpecRunner.sln` is run
- **THEN** the `SpecRunner.Tests` project SHALL execute and all tests
  SHALL pass

### Requirement: Centralized build configuration
The solution SHALL centralize MSBuild properties and NuGet package versions
across all projects using top-level `SpecRunner/Directory.Build.props`,
`SpecRunner/Directory.Build.targets`, and `SpecRunner/Directory.Packages.props`
files, instead of repeating `TargetFramework`, `ImplicitUsings`, `Nullable`,
and package version numbers in each project file.

#### Scenario: Common MSBuild properties are set once
- **WHEN** any project under `SpecRunner/src` or `SpecRunner/tests` is
  inspected
- **THEN** its `.csproj` file SHALL NOT redeclare `TargetFramework`,
  `ImplicitUsings`, or `Nullable`; `TargetFramework` SHALL be set once in
  `SpecRunner/Directory.Build.props` (required there so NuGet restore can
  resolve it before `Directory.Build.targets` is imported), and
  `ImplicitUsings`/`Nullable` SHALL be set once in
  `SpecRunner/Directory.Build.targets`, applying to every project in the
  solution

#### Scenario: Central package management controls NuGet versions
- **WHEN** any project under `SpecRunner/src` or `SpecRunner/tests`
  declares a `PackageReference`
- **THEN** the reference SHALL omit a `Version` attribute, and the version
  SHALL instead be resolved from a matching `PackageVersion` entry in
  `SpecRunner/Directory.Packages.props` with
  `ManagePackageVersionsCentrally` enabled

#### Scenario: Solution still builds and tests after centralization
- **WHEN** `dotnet build SpecRunner/SpecRunner.sln` and
  `dotnet test SpecRunner/SpecRunner.sln` are run
- **THEN** both SHALL succeed with no errors, using the centrally defined
  properties and package versions
