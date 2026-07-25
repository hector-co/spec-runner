## Why

SpecRunner has no root-level `README.md`. There is no single place that
tells an operator how to build the tool, where to place it relative to the
repository it watches, what `appsettings.json` values it needs, or what the
propose → implement/update → finalize comment-driven flow actually does at
each step. New operators must currently read source code and `openspec/specs/`
to reconstruct this.

## What Changes

- Add a root-level `README.md` describing:
  - How to build the application (`build/build.ps1`) and where the
    published output ends up.
  - That the published output SHALL be placed in a folder named
    `.specrunner`, expected to sit at the same level as the `openspec`
    folder — preferably the repository root of the repository SpecRunner
    is configured to operate on.
  - The required `appsettings.json` values: `SpecRunner:GitHubToken` (PAT),
    `SpecRunner:RepositoryUrl`, `SpecRunner:LocalRepositoryPath`, plus the
    other `SpecRunner`, `CliAgent`, and `OpenSpecCli` settings, and the
    prerequisite of an authenticated `claude` CLI on `PATH`.
  - Each step of the comment-driven flow (`/propose` on an issue,
    `/implement` and `/update` on the resulting draft PR, `/finalize` to
    mark the PR ready for review) — where each command is issued and what
    it triggers.
  - That the automated flow ends once `/finalize` marks the PR ready for
    review; the user must verify and merge the PR manually.
- No `.gitignore` change is required by this proposal: `/.specrunner` is
  already present in the repository-root `.gitignore` (added alongside the
  build script). The README documents this existing entry rather than
  introducing it.

## Capabilities

### New Capabilities
- `readme`: A root-level `README.md` documenting how to build, deploy,
  configure, and operate SpecRunner, including the full comment-driven
  workflow and where that automated flow hands off to the user.

### Modified Capabilities
(none — this change adds documentation only and does not alter any
existing behavior or requirement)

## Impact

- Adds `README.md` at the repository root. No source code, configuration
  schema, or build script changes.

## Assumptions

- `/.specrunner` is already listed in the root `.gitignore` (confirmed by
  inspection), so this change documents that convention rather than adding
  it.
- "the folder url" in the request is interpreted as
  `SpecRunner:LocalRepositoryPath` (the local clone path SpecRunner
  operates on), since that is the only folder-path setting in
  `SpecRunnerOptions`.
- The README targets the currently shipped flow only (`propose`,
  `implement`, `update`, `finalize`); no new workflow steps are introduced.
