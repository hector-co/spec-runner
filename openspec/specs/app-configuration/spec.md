# app-configuration

## Purpose

TBD - defines how SpecRunner loads and exposes runtime configuration
(GitHub token, repository targeting, base branch, task timeout) via a
strongly-typed options model.

## Requirements

### Requirement: Configuration model exposes required settings
`SpecRunner.Core` SHALL define a `SpecRunnerOptions` model exposing, at
minimum: a GitHub personal access token, the target repository URL, the
local repository path, the base branch name used for all PRs, and the
per-task timeout. `SpecRunner.Console` SHALL bind this model from
configuration via the standard `IOptions` pattern.

#### Scenario: Options bind from appsettings.json
- **WHEN** an `appsettings.json` file supplies values for
  `GitHubToken`, `RepositoryUrl`, `LocalRepositoryPath`,
  `BaseBranchName`, and `TaskTimeout`
- **THEN** `IOptions<SpecRunnerOptions>` resolved from the host SHALL
  expose those values unchanged

### Requirement: Base branch name is configurable per repository
The base branch used when creating PRs SHALL be read from configuration
rather than hardcoded, and SHALL default to `main` when not explicitly
set.

#### Scenario: Base branch omitted from configuration
- **WHEN** no `BaseBranchName` value is present in any configuration
  source
- **THEN** `SpecRunnerOptions.BaseBranchName` SHALL resolve to `"main"`

#### Scenario: Base branch explicitly configured
- **WHEN** `BaseBranchName` is set to `"develop"` in configuration
- **THEN** `SpecRunnerOptions.BaseBranchName` SHALL resolve to
  `"develop"`

### Requirement: Task execution timeout is configurable
The maximum time allowed for a single triggered task to complete SHALL be
configurable via `SpecRunnerOptions.TaskTimeout`, expressed as a
`TimeSpan`.

#### Scenario: Task timeout is read from configuration
- **WHEN** `TaskTimeout` is set to `"00:10:00"` in configuration
- **THEN** `SpecRunnerOptions.TaskTimeout` SHALL resolve to a `TimeSpan`
  of 10 minutes

### Requirement: GitHub token is never hardcoded or logged
The GitHub personal access token SHALL be supplied only through
configuration providers (environment variables, user secrets, or an
external configuration source) and SHALL NOT appear as a literal value in
source code or in application log output.

#### Scenario: Token sourced from environment variable
- **WHEN** the GitHub token is supplied via an environment variable
  recognized by the configuration pipeline
- **THEN** `SpecRunnerOptions.GitHubToken` SHALL resolve to that value
  without the value being written to any log sink
