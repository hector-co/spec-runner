# SpecRunner

SpecRunner is a .NET console service that watches a GitHub repository for
`/propose`, `/implement`, `/update`, and `/finalize` comments and drives an
OpenSpec-based, CLI-agent change workflow (issue → draft PR → implementation
→ ready-for-review PR) on your behalf, unattended.

## Build

Run the build script from the repository root:

```powershell
build/build.ps1 [-Configuration <All|FrameworkDependent|X86|SingleFile>]
```

`-Configuration` defaults to `SingleFile`. The build always publishes into
`build/.specrunner`, regardless of which configuration is selected.

## Deploy

`build/build.ps1` always publishes into `build/.specrunner`; it never writes
directly to the repository root. After building, copy (or move) that
`build/.specrunner` folder to `.specrunner` at the same level as the
`openspec` folder — preferably the root of the repository SpecRunner is
configured to operate on.

This placement matters because the shipped `appsettings.json` sets
`SpecRunner:LocalRepositoryPath` to `../`, which is resolved relative to the
running executable. That only resolves to the repository root when the
executable itself lives one level below it — i.e. in a `.specrunner` folder
that is a sibling of `openspec/`.

`/.specrunner` (and `/build/.specrunner`) are already listed in the
repository's root `.gitignore`, so the deployed binaries and the local
`state.db` are never committed.

## Configuration

Configuration is read from `.specrunner/appsettings.json` (next to the
deployed executable).

### `SpecRunner` section

| Key | Description |
| --- | --- |
| `GitHubToken` | A GitHub personal access token with access to the target repository. |
| `RepositoryUrl` | The target GitHub repository, e.g. `https://github.com/owner/repo`. |
| `LocalRepositoryPath` | Path to the local git clone SpecRunner operates on. Ships as `../`, which resolves to the repository root when SpecRunner is deployed as described above. |
| `BaseBranchName` | Base branch used when opening pull requests. Defaults to `main`. |
| `TaskTimeout` | Per-comment processing timeout. Defaults to `00:10:00` (10 minutes). |
| `PollingInterval` | Delay between poll cycles. Defaults to `00:00:10` (10 seconds). |

### `CliAgent` section

| Key | Description |
| --- | --- |
| `Executable` | The CLI-based coding agent to launch. Defaults to `claude` (the Claude Code CLI). |

### `OpenSpecCli` section

| Key | Description |
| --- | --- |
| `Executable` | The OpenSpec CLI executable. Defaults to `openspec.cmd`, resolved via `PATH`. |

### Prerequisites

Before starting SpecRunner, the machine it runs on must have:

- An authenticated `claude` CLI available.
- An `openspec` CLI available (on `PATH`, or at the configured
  `OpenSpecCli:Executable` path).
- A GitHub personal access token with access to the target repository.

SpecRunner verifies all three at startup and exits if any check fails.

## Comment-driven flow

Once running, SpecRunner polls the configured repository and reacts to
comments in order:

1. **`/propose`** — posted as a comment on a GitHub issue. Creates a
   `feature/<issue-number>` branch, runs the CLI agent to generate an
   OpenSpec proposal, commits and pushes it, and opens a draft pull request
   for that issue.
2. **`/implement`** / **`/update`** — posted as comments on the pull request
   `/propose` opened. Each runs the corresponding CLI-agent task against
   that pull request's branch, then commits and pushes the result.
3. **`/finalize`** — posted as a comment on the pull request. Archives the
   OpenSpec change and marks the pull request ready for review.

The automated flow ends once `/finalize` marks the pull request ready for
review. From that point, reviewing and merging the pull request is a manual
step performed by a human.
