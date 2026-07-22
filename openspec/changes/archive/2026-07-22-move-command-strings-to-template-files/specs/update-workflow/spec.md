## MODIFIED Requirements

### Requirement: The CLI coding agent is run with a natural-language update instruction rendered from the `update` command template
After refreshing the branch, the workflow SHALL render the `update`
command template via `ICommandTemplateRenderer` with `spec_name` set to
the tracked record's spec/change name and `instructions` set to the
triggering comment's trimmed body with the leading `/update` token and
its separating whitespace removed, start a new CLI agent session via
`ICliAgentSessionFactory`, and send it the rendered template's content as
the initial prompt, wrapped in a literal pair of escaped double quotes
(`\"...\"`), matching `propose-workflow` and `implement-workflow`'s
existing prompt-quoting convention. Unlike `propose-workflow` and
`implement-workflow`, the `update` template's rendered content SHALL NOT
be an `/opsx-*` slash command. The workflow SHALL then await the session
reaching a terminal state (`Completed` or `Failed`). No part of the
prompt SHALL be built via C# string interpolation.

#### Scenario: Prompt combines the resolved spec name and stripped comment body, plus the standing unattended-run instruction
- **WHEN** the workflow runs the CLI agent for a tracked PR with spec name
  `"45-add-login-page"` and a triggering comment body of `"/update the
  export button must also support CSV"`
- **THEN** the `update` template SHALL be rendered with `spec_name` set
  to `"45-add-login-page"` and `instructions` set to `"the export button
  must also support CSV"`, and the session SHALL be started with an
  initial prompt whose content is that rendered text — beginning
  `"Update the OpenSpec change \"45-add-login-page\" to reflect the
  following new requirement/information:\nthe export button must also
  support CSV"` and ending with the standing unattended-run instruction
  block — wrapped in a literal pair of double quotes
