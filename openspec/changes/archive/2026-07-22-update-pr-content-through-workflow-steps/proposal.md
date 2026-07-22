## Why

Every workflow step (`propose`, `implement`, `finalize`) currently leaves the pull request description exactly as it was set at creation time: a copy of the raw GitHub issue body. Reviewers looking at a PR have no way to see the current state of the OpenSpec task list, and merging a finalized PR never closes the originating issue because the description never carries a `Closes #N` link. The task list already exists in the change's `tasks.md` on the branch — it should drive the PR description instead of being ignored.

## What Changes

- `propose` workflow: when the draft PR is created, its description is populated from the newly generated `tasks.md` content for the change (instead of the raw issue body).
- `implement` workflow: after a completed run commits and pushes, the PR description is refreshed by re-reading the current `tasks.md` content for the change and replacing the PR body with it.
- `finalize` workflow: after the change is archived and the branch is pushed, `Closes #<issue-number>` is appended to the PR description (on top of the final task-list content), so merging the PR closes the originating issue.
- `github-operations` capability gains a PR-description-update operation (`IGitHubService` member + REST implementation) since no such method exists today — only creation and the ready-for-review mutation exist.
- The change/spec folder name needed to locate `tasks.md` on disk is already persisted as `TrackedIssue.SpecName`; no new state-store fields are required for issue → folder resolution. Because `finalize` runs `openspec archive`, which moves the change directory under `openspec/changes/archive/<date>-<specName>/` before the workflow updates the PR, the finalize step must resolve `tasks.md`'s post-archive location rather than assuming it still lives at `openspec/changes/<specName>/tasks.md`.

## Capabilities

### New Capabilities
- `tasks-file-access`: reads a change's current `tasks.md` content (pre-archive) and its archived `tasks.md` content (post-`openspec archive`) from the local working tree, given a resolved spec/change name.

### Modified Capabilities
- `propose-workflow`: draft PR body is sourced from the change's `tasks.md` content instead of the raw issue body.
- `implement-workflow`: after commit/push, the PR description is refreshed with current `tasks.md` content.
- `finalize-workflow`: after archive/push, the PR description is refreshed with final `tasks.md` content and `Closes #<issue-number>` is appended.
- `github-operations`: adds a method to update an existing pull request's description.

## Impact

- `SpecRunner.Core/Abstractions/IGitHubService.cs` and `SpecRunner.GitHub/GitHubService.cs`: new PR-description-update method.
- New `ITasksFileReader` abstraction (`SpecRunner.Core/Abstractions/`) plus a real implementation reading from `SpecRunnerOptions.LocalRepositoryPath`.
- `SpecRunner.Console/ProposeWorkflowRunner.cs`, `ImplementWorkflowRunner.cs`, `FinalizeWorkflowRunner.cs`: read `tasks.md` from the working tree (pre- or post-archive path as applicable) and call the new update method.
- Test doubles `RecordingGitHubService`/`FakeGitHubService` and a new fake `ITasksFileReader` (`SpecRunner.Tests/Fakes/`): track PR-description-update calls and stub tasks.md content for assertions.
- No database schema changes required (`SpecName` already resolves to the on-disk change folder name).
