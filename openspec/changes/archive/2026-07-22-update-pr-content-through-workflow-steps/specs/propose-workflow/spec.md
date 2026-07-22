## MODIFIED Requirements

### Requirement: A completed CLI-agent run is committed, pushed, and opened as a draft PR
When the CLI agent session reaches state `Completed`, the workflow SHALL
commit all resulting changes via `IGitService.CommitAsync` with message
`"adding specs for #{issue-number}"`, push the branch via
`IGitService.PushAsync`, read the resolved spec name's current `tasks.md`
content via `ITasksFileReader.ReadCurrentAsync` (using an empty string if no
`tasks.md` is found), and create a draft PR via
`IGitHubService.CreateDraftPullRequestAsync` targeting
`SpecRunnerOptions.BaseBranchName` with that `tasks.md` content as the PR
body, instead of the triggering issue's body.

#### Scenario: Successful session results in a published branch and draft PR sourced from tasks.md
- **WHEN** the CLI agent session for issue `45` reaches state `Completed`
  and `openspec/changes/45-add-login-page/tasks.md` contains a task list
- **THEN** the changes SHALL be committed with message `"adding specs for
  #45"`, the `feature/45` branch SHALL be pushed to `origin`, and a draft
  PR targeting `BaseBranchName` SHALL be created whose body is that
  `tasks.md` content (not the issue body)

#### Scenario: Missing tasks.md results in an empty PR body rather than a failure
- **WHEN** the CLI agent session for issue `45` reaches state `Completed`
  but no `tasks.md` exists for the resolved spec name
- **THEN** the draft PR SHALL still be created, with an empty body, and the
  workflow SHALL NOT fail or skip PR creation
