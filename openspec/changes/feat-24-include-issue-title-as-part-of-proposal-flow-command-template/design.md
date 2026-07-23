## Context

The `propose` command template (`SpecRunner.Console/CommandTemplates/propose.txt`)
is rendered by `ICommandTemplateRenderer` and currently has two
placeholders: `{{spec_name}}` (on the first line, part of the
`/opsx-propose` invocation) and `{{issue_body}}` (on its own line below).
`ProposeWorkflowRunner.RunOnceAsync` already carries the triggering
issue's title in scope at the render call site (`comment.IssueTitle`,
used moments earlier to resolve `spec_name` via `ISpecNameResolver`), so
no new data needs to be fetched — only threaded through as an additional
template replacement value.

## Goals / Non-Goals

**Goals:**
- Surface the issue title in the `propose` template's rendered prompt,
  positioned above `{{issue_body}}`.
- Keep `ICommandTemplateRenderer`'s existing unknown-placeholder and
  missing-value error behavior unchanged — `issue_title` is just another
  named replacement value.

**Non-Goals:**
- No changes to `apply.txt`, `update.txt`, or `archive.txt`.
- No change to the standing unattended-run instruction block.
- No change to how `spec_name` is resolved or embedded.

## Decisions

- **Placeholder name**: `{{issue_title}}`, matching the existing
  `snake_case` convention used by `{{spec_name}}` and `{{issue_body}}`.
- **Placement**: its own line between the `/opsx-propose {{spec_name}}`
  line and `{{issue_body}}`, with no label prefix (e.g. not `Title:
  {{issue_title}}`) — consistent with `{{issue_body}}` being a bare
  value with no label of its own. Alternative considered: prefixing with
  `Title: `; rejected to keep the template's existing unlabeled style and
  because the CLI agent can infer the line's role from position and
  content.
- **Value source**: `ProposeWorkflowRunner` passes `comment.IssueTitle`
  (already available, already used for `ISpecNameResolver.Resolve`) as
  the `issue_title` replacement value — no new data fetch.

## Risks / Trade-offs

- [Existing rendered-prompt assertions in tests break] → Update
  `CommandTemplateRendererTests` and any `ProposeWorkflowRunner` tests
  that assert the full rendered prompt text to include the new
  `issue_title` line.
- [Downstream `openspec instructions` output.] Not applicable — this
  changes SpecRunner's own `propose.txt`, not any `openspec` CLI
  template.
