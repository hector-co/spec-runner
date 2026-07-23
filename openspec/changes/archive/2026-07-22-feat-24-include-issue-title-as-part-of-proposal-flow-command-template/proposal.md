## Why

The `propose` command template currently renders the CLI agent's prompt
with only `{{spec_name}}` and `{{issue_body}}`. The triggering GitHub
issue's title is known to the workflow (it's already used to resolve the
spec name) but is never surfaced in the prompt text itself, leaving the
CLI agent to infer the issue's subject from the body alone.

## What Changes

- Add an `{{issue_title}}` placeholder to the `propose` command template
  (`SpecRunner.Console/CommandTemplates/propose.txt`), positioned above
  the existing `{{issue_body}}` placeholder.
- `ProposeWorkflowRunner` supplies `issue_title` (from the triggering
  issue's title) as a replacement value when rendering the `propose`
  template, alongside the existing `spec_name` and `issue_body` values.
- No other command templates (`apply.txt`, `update.txt`, `archive.txt`)
  are changed.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities
- `command-templates`: the `propose` template's fixed content now
  includes an `{{issue_title}}` placeholder above `{{issue_body}}`.
- `propose-workflow`: the requirement describing how the CLI agent
  prompt is rendered now also specifies that `issue_title` is supplied
  from the triggering issue's title.

## Impact

- `SpecRunner.Console/CommandTemplates/propose.txt` — template content.
- `SpecRunner.Console/ProposeWorkflowRunner.cs` — adds `issue_title` to
  the replacement values passed to `ICommandTemplateRenderer.RenderAsync`
  for the `propose` template.
- `SpecRunner.Tests/CommandTemplateRendererTests.cs` and any
  `ProposeWorkflowRunner` tests asserting the rendered prompt's exact
  content — expectations need updating to include the issue title line.

## Assumptions

- The issue title is inserted as a single plain-text line immediately
  above `{{issue_body}}`, with no additional label (e.g. no `Title:`
  prefix), consistent with how `{{issue_body}}` itself is a bare value
  with no surrounding label.
