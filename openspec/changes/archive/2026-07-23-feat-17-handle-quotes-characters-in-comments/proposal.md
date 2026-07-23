## Why

GitHub issue titles/bodies and PR comment instructions flow, unescaped, into
the rendered CLI-agent prompt text. Each of the four workflow runners
(`ProposeWorkflowRunner`, `UpdateWorkflowRunner`, `ImplementWorkflowRunner`,
`FinalizeWorkflowRunner`) wraps the rendered prompt in a literal pair of
double quotes (`$"\"{prompt}\""`) before handing it to
`ICliAgentSession.StartAsync`, and the `archive` template additionally
embeds `{{spec_name}}` directly between literal quotes
(`` "{{spec_name}}" ``). If the untrusted GitHub content substituted into a
template contains a `"` character, that character terminates the intended
quoted block early from the model's perspective, letting the remaining
comment text appear to fall outside the quoted issue/instruction content —
a prompt-injection technique for smuggling additional "commands" into what
should be a single, self-contained CLI-agent prompt. Today nothing escapes
placeholder values before substitution, so this is exploitable by anyone
who can leave a GitHub issue/PR comment.

## What Changes

- `CommandTemplateRenderer.RenderAsync` escapes every substituted
  placeholder value before it is written into the rendered template:
  backslashes are doubled (`\` → `\\`) and then double quotes are escaped
  (`"` → `\"`), so embedded quote characters remain visibly present (never
  stripped) but can no longer terminate a surrounding quoted block early.
- No other call site changes: the existing `$"\"{prompt}\""` wrapping at
  the four workflow-runner call sites and the archive template's
  `"{{spec_name}}"` quoting are left in place and are now safe because the
  values placed inside them are pre-escaped.
- Add renderer test coverage for placeholder values containing `"` and `\`
  characters, confirming the escaped output keeps the quote characters
  (rather than removing them) and keeps the surrounding literal quotes
  balanced.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `command-templates`: `ICommandTemplateRenderer`'s placeholder
  substitution now escapes backslash and double-quote characters in each
  supplied replacement value before substitution, instead of substituting
  raw, unescaped values.

## Impact

- `SpecRunner/src/SpecRunner.Console/CommandTemplateRenderer.cs` — add
  escaping of substituted values.
- `SpecRunner/tests/SpecRunner.Tests/CommandTemplateRendererTests.cs` — add
  coverage for values containing `"` and `\`.
- No changes needed to `ProposeWorkflowRunner.cs`, `UpdateWorkflowRunner.cs`,
  `ImplementWorkflowRunner.cs`, `FinalizeWorkflowRunner.cs`, or the
  `ClaudeCliAgentSession`/`SystemChildProcess` process-launch path (already
  safe: process arguments use `ProcessStartInfo.ArgumentList`, no shell is
  invoked, and stdin user-turn text is JSON-serialized correctly regardless
  of this change).

## Assumptions

- "CLI command" in this change's scope refers to the rendered CLI-agent
  prompt text (the `claude` CLI's stdin user-turn content and the literal
  quoting workflow runners apply around it), not OS shell command-line
  construction — the process launch path already uses
  `ProcessStartInfo.ArgumentList` with `UseShellExecute = false`, so there
  is no separate shell-injection surface to address there.
- Escaping is applied uniformly to every substituted value (not just
  known-untrusted ones like issue title/body/instructions), since
  internally-derived values (e.g. `spec_name`) never contain quotes or
  backslashes and are unaffected by the change.
- Only backslash and double-quote characters are escaped. Other
  characters (newlines, markdown, etc.) are left as-is since they don't
  interact with the quoting boundary this change protects.
