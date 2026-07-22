## MODIFIED Requirements

### Requirement: A completed CLI-agent run is committed, pushed, and the PR is marked ready for review
When the CLI agent session reaches state `Completed`, the workflow SHALL
commit all resulting changes via `IGitService.CommitAsync` with message
`"finalizing specs for #{issue-number}"` (using the issue number from the
tracked record), push the branch via `IGitService.PushAsync`, read the
resolved spec name's archived `tasks.md` content via
`ITasksFileReader.ReadArchivedAsync`, build a final PR description of that
content with `\n\nCloses #{issue-number}` appended (using an empty content
prefix if no archived `tasks.md` is found, so the `Closes #{issue-number}`
line is still always added), update the PR's description via
`IGitHubService.UpdatePullRequestDescriptionAsync` with that final body, and
then mark the PR ready for review via
`IGitHubService.MarkPrReadyForReviewAsync`. The workflow SHALL NOT create a
new branch or a new pull request, since the PR already exists.

#### Scenario: Successful session results in a push, an updated description with a closing link, and a ready-for-review PR
- **WHEN** the CLI agent session for a tracked PR with issue number `45` on
  branch `"feature/45"` and PR number `12` reaches state `Completed`, and
  `openspec/changes/archive/2026-07-21-45-add-login-page/tasks.md` contains
  the final task list
- **THEN** the changes SHALL be committed with message `"finalizing specs
  for #45"`, the `"feature/45"` branch SHALL be pushed to `origin`, PR
  `12`'s description SHALL be updated to that `tasks.md` content followed
  by `"\n\nCloses #45"`, and PR `12` SHALL then be marked ready for review,
  with no new branch or pull request created

#### Scenario: Missing archived tasks.md still appends the closing link
- **WHEN** the CLI agent session for a tracked PR with issue number `45`
  reaches state `Completed` but no archived `tasks.md` can be found for the
  resolved spec name
- **THEN** PR `12`'s description SHALL still be updated to end with
  `"Closes #45"`, and the PR SHALL still be marked ready for review
