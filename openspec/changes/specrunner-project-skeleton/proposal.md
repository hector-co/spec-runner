## Why

SpecRunner needs a concrete .NET 10 solution and project layout before any
comment-driven workflow logic can be built. Right now there is no code, no
configuration model, and no agreed shape for the local state that will tie
GitHub issues, PRs, comments, and OpenSpec changes together. Establishing
that skeleton first lets future changes add the propose/update/implement/
archive behavior against stable project boundaries instead of inventing
structure ad hoc.

## What Changes

- Create a `SpecRunner.sln` solution under a top-level `/SpecRunner` folder.
- Add a console entry-point project plus separate projects for GitHub
  access, git operations, local state, and shared core models/abstractions,
  so each concern (GitHub API, git CLI/process control, persistence) can
  evolve independently.
- Add a test project wired to the solution (no tests yet beyond a smoke
  test) so future changes have somewhere to put coverage.
- Define an application configuration model covering: GitHub PAT, target
  repository (owner/name), configurable base branch name for PRs, and a
  configurable per-task timeout. No values are hardcoded; the shape is
  config-driven so the same binary can point at different repos.
- Define the shape of the local state store: entities/records for issues,
  PRs, comments, and specs/changes, and how they associate with each other
  (e.g. an issue maps to a spec/change name, a PR maps to an issue, a
  comment maps to a processing status). Only the schema and a storage
  interface are defined here — no read/write implementation or workflow
  logic.
- Define service boundaries (interfaces only) for git operations (branch
  create/switch, commit, push, pull) and GitHub operations (create PR,
  create draft PR, read/write PR comments, mark PR ready for review), so
  later changes implement against a fixed contract.
- Establish the spec/change naming convention as a pure utility contract:
  `[issue number]-[issue name, lower-cased, spaces replaced with dashes,
  invalid folder-name characters stripped]`.

## Capabilities

### New Capabilities
- `solution-layout`: The SpecRunner .NET 10 solution and project structure
  (console app, core, GitHub, git, state, tests) and how responsibilities
  are divided between projects.
- `app-configuration`: The configuration model and its required settings
  (GitHub PAT, target repository, base branch name, task timeout), and how
  the console app loads it.
- `state-store-schema`: The shape of the local state store used to
  associate issues, PRs, comments, and specs/changes, and the storage
  interface contract (not its implementation).

### Modified Capabilities
(none — first change in this repository)

## Impact

- New solution and project files under `/SpecRunner` (no existing code
  affected).
- Establishes the configuration and state-store contracts that all future
  SpecRunner behavior (propose/update/implement/archive workflows) will be
  built against, so changing them later is a breaking change to those
  future features.
- No GitHub or git operations are implemented yet; interfaces are defined
  but unimplemented, so the console app is not yet functional end-to-end.
