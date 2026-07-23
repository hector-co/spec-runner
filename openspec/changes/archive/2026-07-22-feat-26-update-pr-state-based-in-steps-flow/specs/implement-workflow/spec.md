## ADDED Requirements

### Requirement: A completed run renames the PR title to reflect implementation progress
After the existing commit-and-push step completes, the workflow SHALL
derive `<issue-name>` from the tracked PR's current title (the text
following the literal substring `"#{issue-number}: "` in that title, or the
whole current title if that substring is not found) and call
`IGitHubService.UpdatePullRequestTitleAsync` with the tracked PR number and
the title `"Implementations for #{issue-number}: {issue-name}"`, replacing
the PR's existing title. This rename SHALL happen regardless of whether
`tasks.md` content was found for the description refresh.

#### Scenario: PR title is renamed to reflect implementation after a push
- **WHEN** the workflow pushes changes for a tracked PR numbered `12` with
  issue number `45`, whose current title is `"Proposal for #45: Add login
  page"`
- **THEN** PR `12`'s title SHALL be updated to `"Implementations for #45:
  Add login page"` via `UpdatePullRequestTitleAsync`

#### Scenario: Rename still happens when no tasks.md content is found
- **WHEN** the workflow pushes changes for a tracked PR whose resolved spec
  name has no `tasks.md` on disk
- **THEN** the PR's title SHALL still be renamed to `"Implementations for
  #{issue-number}: {issue-name}"`, even though the description update is
  skipped

#### Scenario: A title with no recognizable "#issue-number:" segment falls back to the whole title
- **WHEN** the tracked PR's current title does not contain the literal
  substring `"#{issue-number}: "` (e.g. it was manually retitled)
- **THEN** the whole current title SHALL be used as `<issue-name>` in the
  new title, and the rename SHALL still be attempted rather than skipped
  or failing the run
