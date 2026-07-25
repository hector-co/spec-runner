# readme

## Purpose

TBD - defines the root `README.md` documentation covering how to build,
deploy, configure, and operate SpecRunner via its comment-driven GitHub
workflow.

## Requirements

### Requirement: Root README describes how to build and where to deploy SpecRunner
The repository SHALL contain a `README.md` at its root describing how to
build SpecRunner via `build/build.ps1`, and stating that the built output
SHALL be placed in a folder named `.specrunner`, expected to sit at the
same level as the `openspec` folder — preferably at the root of the
repository SpecRunner is configured to operate on.

#### Scenario: README exists at the repository root
- **WHEN** the repository is inspected
- **THEN** a file SHALL exist at `README.md` in the repository root

#### Scenario: README documents the build step
- **WHEN** `README.md` is read
- **THEN** it SHALL reference `build/build.ps1` as the way to produce a
  runnable SpecRunner build

#### Scenario: README documents the expected deployment location
- **WHEN** `README.md` is read
- **THEN** it SHALL state that the built application belongs in a
  `.specrunner` folder situated at the same level as the `openspec`
  folder, preferably at the repository root

### Requirement: README documents the `.gitignore` entry for the deployed tool
`README.md` SHALL note that `/.specrunner` is present in the repository's
`.gitignore` so that the deployed binaries and local state database are
not committed.

#### Scenario: README references the ignored deployment folder
- **WHEN** `README.md` is read
- **THEN** it SHALL state that `/.specrunner` is ignored via `.gitignore`

### Requirement: README describes required configuration and prerequisites
`README.md` SHALL describe, for the deployed `.specrunner/appsettings.json`,
the required `SpecRunner` section values (`GitHubToken` as a GitHub
personal access token, `RepositoryUrl`, `LocalRepositoryPath`, and
`BaseBranchName`), and SHALL state that an authenticated `claude` CLI and
an available `openspec` CLI must be present on the machine before
SpecRunner is started.

#### Scenario: README lists the GitHub PAT and repository settings
- **WHEN** `README.md` is read
- **THEN** it SHALL describe `SpecRunner:GitHubToken` as a GitHub personal
  access token and `SpecRunner:RepositoryUrl` as the GitHub repository
  URL SpecRunner operates against

#### Scenario: README describes the local repository path setting
- **WHEN** `README.md` is read
- **THEN** it SHALL describe `SpecRunner:LocalRepositoryPath` as the path
  to the local git clone that SpecRunner operates on

#### Scenario: README states the authenticated Claude CLI prerequisite
- **WHEN** `README.md` is read
- **THEN** it SHALL state that an authenticated `claude` CLI must be
  available before starting SpecRunner

### Requirement: README describes each step of the comment-driven flow
`README.md` SHALL describe, in order, the `/propose`, `/implement`,
`/update`, and `/finalize` triggers: where each is posted (issue comment
vs. pull request comment), and what SpecRunner does in response to each.

#### Scenario: README describes the propose step
- **WHEN** `README.md` is read
- **THEN** it SHALL state that posting `/propose` as a comment on a
  GitHub issue causes SpecRunner to create a branch, generate a proposal
  via the CLI agent, and open a draft pull request for that issue

#### Scenario: README describes the implement and update steps
- **WHEN** `README.md` is read
- **THEN** it SHALL state that posting `/implement` or `/update` as a
  comment on the pull request opened by `/propose` causes SpecRunner to
  run the corresponding CLI-agent task against that pull request's branch
  and push the resulting changes

#### Scenario: README describes the finalize step and the manual merge handoff
- **WHEN** `README.md` is read
- **THEN** it SHALL state that posting `/finalize` as a comment on the
  pull request causes SpecRunner to archive the change and mark the pull
  request ready for review, and SHALL state that the automated flow ends
  there — the user is expected to review and manually merge the pull
  request afterward
