## MODIFIED Requirements

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
