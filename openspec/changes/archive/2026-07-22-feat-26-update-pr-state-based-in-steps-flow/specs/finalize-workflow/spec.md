## ADDED Requirements

### Requirement: A completed run renames the PR title to reflect the finalized state
The workflow SHALL, after the existing commit-and-push step and the
existing description update, and before marking the PR ready for review,
derive `<issue-name>` from the tracked PR's current title (the text
following the literal substring `"#{issue-number}: "` in that title, or the
whole current title if that substring is not found) and call
`IGitHubService.UpdatePullRequestTitleAsync` with the tracked PR number and
the title `"#{issue-number}: {issue-name}"`, replacing the PR's existing
title.

#### Scenario: PR title is renamed to its finalized form after archiving
- **WHEN** the workflow finalizes a tracked PR numbered `12` with issue
  number `45`, whose current title is `"Implementations for #45: Add login
  page"`
- **THEN** PR `12`'s title SHALL be updated to `"#45: Add login page"` via
  `UpdatePullRequestTitleAsync`, before the PR is marked ready for review

#### Scenario: A title with no recognizable "#issue-number:" segment falls back to the whole title
- **WHEN** the tracked PR's current title does not contain the literal
  substring `"#{issue-number}: "` (e.g. it was manually retitled)
- **THEN** the whole current title SHALL be used as `<issue-name>` in the
  new title `"#{issue-number}: {issue-name}"`, and the rename SHALL still
  be attempted rather than skipped or failing the run
