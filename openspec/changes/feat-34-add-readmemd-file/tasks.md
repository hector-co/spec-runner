## 1. README scaffold

- [ ] 1.1 Create `README.md` at the repository root with a title and short
      project summary for SpecRunner.
- [ ] 1.2 Add a "Build" section documenting `build/build.ps1`, its
      `-Configuration` parameter (`All` / `FrameworkDependent` / `X86` /
      `SingleFile`, default `SingleFile`), and that it publishes into
      `build/.specrunner`.

## 2. Deployment section

- [ ] 2.1 Add a "Deploy" section stating the built output must be copied
      into a `.specrunner` folder placed at the same level as the
      `openspec` folder — preferably the root of the repository SpecRunner
      is configured to operate on — and explain why (`LocalRepositoryPath`
      resolves relative to the running executable).
- [ ] 2.2 Note that `/.specrunner` is already present in `.gitignore`, so
      the deployed binaries and local `state.db` are not committed.

## 3. Configuration section

- [ ] 3.1 Document the `SpecRunner` section of `appsettings.json`:
      `GitHubToken` (PAT), `RepositoryUrl`, `LocalRepositoryPath`,
      `BaseBranchName` (default `main`), `TaskTimeout` (default
      `00:10:00`), `PollingInterval` (default `00:00:10`).
- [ ] 3.2 Document the `CliAgent` section (`Executable`, default `claude`)
      and the `OpenSpecCli` section (`Executable`, default `openspec`).
- [ ] 3.3 State the prerequisites: an authenticated `claude` CLI and an
      available `openspec` CLI on `PATH` (or configured executable path),
      and a GitHub PAT with access to the target repository — all
      verified by SpecRunner's startup dependency checks.

## 4. Flow section

- [ ] 4.1 Document `/propose` on a GitHub issue comment: creates a
      `feature/<issue-number>` branch, runs the CLI agent to generate a
      proposal, commits, pushes, and opens a draft pull request.
- [ ] 4.2 Document `/implement` and `/update` as PR comments: run the
      corresponding CLI-agent task against the PR's branch, commit, push,
      and update the PR description/title.
- [ ] 4.3 Document `/finalize` as a PR comment: archives the change and
      marks the PR ready for review.
- [ ] 4.4 State explicitly that the automated flow ends when `/finalize`
      marks the PR ready for review, and that the user must review and
      manually merge the PR afterward.

## 5. Review

- [ ] 5.1 Re-read `README.md` against `appsettings.json`,
      `build/build.ps1`, and the four workflow runners to confirm no
      claim has drifted from the current source.
