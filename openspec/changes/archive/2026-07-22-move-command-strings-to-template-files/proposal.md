## Why

`ProposeWorkflowRunner`, `ImplementWorkflowRunner`, `UpdateWorkflowRunner`,
and `FinalizeWorkflowRunner` each build the CLI-agent prompt as an inline
C# interpolated string. This makes prompts hard to read, hard to extend
with additional lines or instructions, and impossible to tweak without a
code change and rebuild. Moving each command's text to its own template
file, with named placeholders for the values that get substituted in,
makes prompts easier to author and review, and gives every command a
single place to append shared instructions — starting with a standing
"unattended run" instruction that should apply to every CLI-agent prompt
SpecRunner sends.

## What Changes

- Add a new `ICommandTemplateRenderer` abstraction that loads a named
  command template from disk and substitutes `{{token}}` placeholders
  with supplied values.
- Add one template file per existing command (`propose`, `apply`,
  `update`, `archive`), each containing the command text previously
  built inline, with `{{spec_name}}`, `{{issue_body}}`, or
  `{{instructions}}` placeholders where the workflow runners currently
  interpolate values.
- Every template file ends with a fixed "unattended run" instruction
  block, so the CLI agent is always told not to pause for confirmation
  or clarification and instead to record assumptions in `proposal.md`.
- `ProposeWorkflowRunner`, `ImplementWorkflowRunner`,
  `UpdateWorkflowRunner`, and `FinalizeWorkflowRunner` are updated to
  render their prompt from a template via `ICommandTemplateRenderer`
  instead of building it with a C# interpolated string. The
  escaped-double-quote wrapping applied when the prompt is handed to
  `ICliAgentSession.StartAsync` stays in the workflow runner, not in the
  template.
- **BREAKING**: The exact prompt text sent to the CLI agent gains a
  trailing "unattended run" instruction block on every workflow
  (`propose`, `implement`, `update`, `finalize`). This is an intentional
  behavior change to the agent-facing prompt content, not just an
  internal refactor.

## Capabilities

### New Capabilities
- `command-templates`: File-based command templates with placeholder
  substitution, used by the workflow runners to build CLI-agent prompts.

### Modified Capabilities
- `propose-workflow`: The `/opsx-propose` prompt is now rendered from the
  `propose` command template (via `ICommandTemplateRenderer`) instead of
  built inline, and includes the trailing unattended-run instruction.
- `implement-workflow`: The `/opsx-apply` prompt is now rendered from the
  `apply` command template instead of built inline, and includes the
  trailing unattended-run instruction.
- `update-workflow`: The update instruction prompt is now rendered from
  the `update` command template instead of built inline, and includes the
  trailing unattended-run instruction.
- `finalize-workflow`: The archive instruction prompt is now rendered
  from the `archive` command template instead of built inline, and
  includes the trailing unattended-run instruction.

## Impact

- `SpecRunner.Core.Abstractions`: new `ICommandTemplateRenderer`
  interface.
- `SpecRunner.Console`: new `CommandTemplateRenderer` implementation, new
  `CommandTemplates/*.txt` template files (copied to output directory),
  and updated `ProposeWorkflowRunner`, `ImplementWorkflowRunner`,
  `UpdateWorkflowRunner`, `FinalizeWorkflowRunner` to depend on
  `ICommandTemplateRenderer` and render prompts from templates.
- `SpecRunner.Console.csproj`: new `Content` items for the template
  files, copied to the output directory like `appsettings.json`.
- Tests: workflow runner tests and any DI smoke tests need a fake/real
  `ICommandTemplateRenderer` and updated expected prompt strings
  (including the new trailing instruction block).
