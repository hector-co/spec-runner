## MODIFIED Requirements

### Requirement: The CLI coding agent is run with an `/opsx-propose` prompt rendered from the `propose` command template
After creating the issue branch, the workflow SHALL resolve the spec name
via `ISpecNameResolver` from the issue number and title, render the
`propose` command template via `ICommandTemplateRenderer` with
`spec_name` set to the resolved spec name and `issue_body` set to the
triggering issue's body, start a new CLI agent session via
`ICliAgentSessionFactory`, and send it the rendered template's content as
the initial prompt, wrapped in a literal pair of escaped double quotes
(`\"...\"`), then await the session reaching a terminal state
(`Completed` or `Failed`). No part of the prompt SHALL be built via C#
string interpolation.

#### Scenario: Prompt combines the resolved spec name and issue body, plus the standing unattended-run instruction
- **WHEN** the workflow runs the CLI agent for issue `45` titled
  `"Add Login Page"` with body `"We need a login page."`
- **THEN** the `propose` template SHALL be rendered with `spec_name` set
  to `"45-add-login-page"` and `issue_body` set to `"We need a login
  page."`, and the session SHALL be started with an initial prompt whose
  content is that rendered text — beginning
  `"/opsx-propose 45-add-login-page\nWe need a login page."` and ending
  with the standing unattended-run instruction block — wrapped in a
  literal pair of double quotes
