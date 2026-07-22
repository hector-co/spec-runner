## ADDED Requirements

### Requirement: A completed run refreshes the PR description with current task list content
After the existing commit-and-push step completes, the workflow SHALL read
the tracked record's spec name's current `tasks.md` content via
`ITasksFileReader.ReadCurrentAsync` and, if content is found, call
`IGitHubService.UpdatePullRequestDescriptionAsync` with the tracked PR
number and that content, replacing the PR's existing description. If no
`tasks.md` content is found, the workflow SHALL skip the description update
rather than clearing the PR body.

#### Scenario: PR description is replaced with the current task list after a push
- **WHEN** the workflow pushes changes for a tracked PR numbered `12` whose
  spec name's `tasks.md` currently contains an updated task list
- **THEN** PR `12`'s description SHALL be replaced with that `tasks.md`
  content via `UpdatePullRequestDescriptionAsync`

#### Scenario: Missing tasks.md leaves the existing PR description untouched
- **WHEN** the workflow pushes changes for a tracked PR whose resolved spec
  name has no `tasks.md` on disk
- **THEN** `UpdatePullRequestDescriptionAsync` SHALL NOT be called, and the
  PR's existing description SHALL be left unchanged
