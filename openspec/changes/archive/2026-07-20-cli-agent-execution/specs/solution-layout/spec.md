## MODIFIED Requirements

### Requirement: Project responsibility separation
The solution SHALL separate concerns into distinct projects: a console
entry point, a core project holding domain models and service
abstractions, a git-operations project, a GitHub-operations project, a
local state-store project, and a CLI-agent-execution project. Each project
SHALL depend only on `SpecRunner.Core` and, in the case of the console
project, on the other `src` projects — no circular project references
SHALL exist.

#### Scenario: Console project composes the other projects
- **WHEN** the solution is inspected for project references
- **THEN** `SpecRunner.Console` SHALL reference `SpecRunner.Core`,
  `SpecRunner.Git`, `SpecRunner.GitHub`, `SpecRunner.State`, and
  `SpecRunner.Cli`, and none of `SpecRunner.Git`, `SpecRunner.GitHub`,
  `SpecRunner.State`, or `SpecRunner.Cli` SHALL reference each other

#### Scenario: Core project has no outbound project dependencies
- **WHEN** the solution is inspected for project references
- **THEN** `SpecRunner.Core` SHALL NOT reference any other `SpecRunner.*`
  project
