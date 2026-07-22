## MODIFIED Requirements

### Requirement: The CLI coding agent is run with a natural-language archive instruction rendered from the `archive` command template
After refreshing the branch, the workflow SHALL render the `archive`
command template via `ICommandTemplateRenderer` with `spec_name` set to
the tracked record's spec/change name and `instructions` set to the
triggering comment's trimmed body with the leading `/finalize` token and
its separating whitespace removed, start a new CLI agent session via
`ICliAgentSessionFactory`, and send it the rendered template's content as
the initial prompt, wrapped in a literal pair of escaped double quotes
(`\"...\"`), matching `propose-workflow`, `implement-workflow`, and
`update-workflow`'s existing prompt-quoting convention. Like
`update-workflow`, the `archive` template's rendered content SHALL NOT be
an `/opsx-*` slash command. The workflow SHALL then await the session
reaching a terminal state (`Completed` or `Failed`). No part of the
prompt SHALL be built via C# string interpolation.

#### Scenario: Prompt combines the resolved spec name and stripped comment body, plus the standing unattended-run instruction
- **WHEN** the workflow runs the CLI agent for a tracked PR with spec name
  `"45-add-login-page"` and a triggering comment body of `"/finalize the
  export button task was implemented under a different name"`
- **THEN** the `archive` template SHALL be rendered with `spec_name` set
  to `"45-add-login-page"` and `instructions` set to `"the export button
  task was implemented under a different name"`, and the session SHALL be
  started with an initial prompt whose content is that rendered text —
  beginning `"Run \`openspec archive \"45-add-login-page\" --yes\`. Mark
  missing tasks as completed and continue.\nthe export button task was
  implemented under a different name"` and ending with the standing
  unattended-run instruction block — wrapped in a literal pair of double
  quotes
