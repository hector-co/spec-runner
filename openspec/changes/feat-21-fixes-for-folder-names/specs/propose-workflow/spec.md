## MODIFIED Requirements

### Requirement: The CLI coding agent is run with an `/opsx-propose` prompt rendered from the `propose` command template
After creating the issue branch, the workflow SHALL resolve the expected
spec name via `ISpecNameResolver` from the issue number and title, render
the `propose` command template via `ICommandTemplateRenderer` with
`spec_name` set to the resolved expected spec name and `issue_body` set to
the triggering issue's body, start a new CLI agent session via
`ICliAgentSessionFactory`, and send it the rendered template's content as
the initial prompt, wrapped in a literal pair of escaped double quotes
(`\"...\"`), then await the session reaching a terminal state
(`Completed` or `Failed`). No part of the prompt SHALL be built via C#
string interpolation.

#### Scenario: Prompt combines the resolved spec name and issue body, plus the standing unattended-run instruction
- **WHEN** the workflow runs the CLI agent for issue `45` titled
  `"Add Login Page"` with body `"We need a login page."`
- **THEN** the `propose` template SHALL be rendered with `spec_name` set
  to `"feat-45-add-login-page"` and `issue_body` set to `"We need a login
  page."`, and the session SHALL be started with an initial prompt whose
  content is that rendered text — beginning
  `"/opsx-propose feat-45-add-login-page\nWe need a login page."` and
  ending with the standing unattended-run instruction block — wrapped in a
  literal pair of double quotes

### Requirement: A completed CLI-agent run is committed, pushed, and opened as a draft PR
When the CLI agent session reaches state `Completed`, the workflow SHALL
resolve the actual on-disk spec name (per the `spec-folder-resolution`
capability's `ISpecFolderResolver.ResolveAsync`, using the expected spec
name and issue number), then commit all resulting changes via
`IGitService.CommitAsync` with message `"adding specs for #{issue-number}"`,
push the branch via `IGitService.PushAsync`, read the resolved actual spec
name's current `tasks.md` content via `ITasksFileReader.ReadCurrentAsync`
(using an empty string if no `tasks.md` is found), and create a draft PR
via `IGitHubService.CreateDraftPullRequestAsync` targeting
`SpecRunnerOptions.BaseBranchName` with that `tasks.md` content as the PR
body, instead of the triggering issue's body. The resolved actual spec name
(not the originally expected one) SHALL be used for all of these steps.

#### Scenario: Successful session results in a published branch and draft PR sourced from tasks.md
- **WHEN** the CLI agent session for issue `45` reaches state `Completed`
  and `openspec/changes/feat-45-add-login-page/tasks.md` contains a task
  list
- **THEN** the changes SHALL be committed with message `"adding specs for
  #45"`, the `feature/45` branch SHALL be pushed to `origin`, and a draft
  PR targeting `BaseBranchName` SHALL be created whose body is that
  `tasks.md` content (not the issue body)

#### Scenario: Missing tasks.md results in an empty PR body rather than a failure
- **WHEN** the CLI agent session for issue `45` reaches state `Completed`
  but no `tasks.md` exists for the resolved actual spec name
- **THEN** the draft PR SHALL still be created, with an empty body, and the
  workflow SHALL NOT fail or skip PR creation

### Requirement: A successful run reports the created PR back on the comment and in the state store
After a draft PR is created, the workflow SHALL add a `rocket` reaction to
the triggering comment, post a reply comment with body `"Created Draft PR
#{pr-number} for this issue."`, and upsert the state store with the
issue number, the resolved actual spec name (as returned by
`ISpecFolderResolver.ResolveAsync`, not the originally expected one), PR
number, and the comment's processing status set to `done`.

#### Scenario: Successful outcome is reflected on GitHub and in the state store
- **WHEN** a draft PR numbered `12` is created for issue `45`
- **THEN** the triggering comment SHALL receive a `rocket` reaction, a
  reply `"Created Draft PR #12 for this issue."` SHALL be posted, and the
  state store SHALL record issue `45` with PR `12` and the comment's
  status as `done`

## ADDED Requirements

### Requirement: An unresolvable spec folder halts the run before any commit, push, or PR
If, after the CLI agent session reaches state `Completed`,
`ISpecFolderResolver.ResolveAsync` cannot find a matching spec folder on
disk for the expected spec name and issue number (per
`spec-folder-resolution`), the workflow SHALL NOT commit, push, or create a
draft PR for that comment. This failure SHALL be reported through the same
error-reporting path as any other unhandled failure during comment
processing: a `confused` reaction, a human-readable reply, and a
state-store status of `error`.

#### Scenario: No matching spec folder stops the run before any git or GitHub write
- **WHEN** the CLI agent session for issue `45` reaches state `Completed`
  but no directory under `openspec/changes/` matches either the expected
  spec name or the `feat-45-` prefix
- **THEN** no commit, push, or draft PR SHALL be created for issue `45`,
  the triggering comment SHALL receive a `confused` reaction and a
  human-readable reply summarizing the failure, and the state-store status
  for that comment SHALL be `error`
