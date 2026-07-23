## Context

`CommandTemplateRenderer.RenderAsync` (`SpecRunner/src/SpecRunner.Console/CommandTemplateRenderer.cs`)
substitutes `{{token}}` placeholders in the four shipped command templates
(`propose.txt`, `apply.txt`, `update.txt`, `archive.txt`) with raw,
unescaped values. Several of those values originate from untrusted GitHub
content: `issue_title`/`issue_body` (from the triggering issue) and
`instructions` (the free-text tail of an `/update`, `/apply`, or archive
comment).

Two places in the rendered output rely on the substituted text staying
inside a quoted boundary:

- `archive.txt` embeds `{{spec_name}}` directly between literal quotes:
  `` Run `openspec archive "{{spec_name}}" --yes`. ``
- All four workflow runners (`ProposeWorkflowRunner`, `UpdateWorkflowRunner`,
  `ImplementWorkflowRunner`, `FinalizeWorkflowRunner`) wrap the entire
  rendered template in a literal quote pair before calling
  `ICliAgentSession.StartAsync`: `` await session.StartAsync($"\"{prompt}\"", ...) ``.

That wrapped string becomes the `text` field of a stream-json user-turn
message (`ClaudeCliAgentSession.BuildUserTurnJson`), written to the
`claude` CLI's stdin. `JsonSerializer.Serialize` correctly JSON-escapes
that field, so the wire protocol itself can't be corrupted by embedded
quotes — the risk is semantic, not structural: an unescaped `"` inside
GitHub content can make the rendered prompt look, to the model reading it,
as if the quoted "issue/comment content" block ended early and a new,
attacker-controlled instruction began outside of it (the "concatenating
commands" scenario from the proposal). Process launch itself
(`SystemChildProcess`, using `ProcessStartInfo.ArgumentList` with
`UseShellExecute = false`) is unaffected — no shell parses these strings —
so this change is scoped entirely to the renderer.

## Goals / Non-Goals

**Goals:**
- Guarantee that any `"` or `\` character present in a value passed to
  `ICommandTemplateRenderer.RenderAsync` cannot terminate or otherwise
  interfere with a quoted block the rendered text is later embedded in,
  wherever that quoting is applied (in-template, like `archive.txt`, or by
  the calling workflow runner, like the `$"\"{prompt}\""` wrap).
- Keep quote characters visible in the rendered output (escaped, not
  stripped) so the model still sees that the source content contained a
  quote.
- Centralize the fix in one place (`CommandTemplateRenderer`) so all
  current and future templates/placeholders get it automatically, rather
  than requiring each workflow runner to remember to sanitize its inputs.

**Non-Goals:**
- OS shell/argv escaping — not applicable, since `SystemChildProcess`
  never invokes a shell.
- General prompt-injection defense (e.g. GitHub content instructing the
  agent to ignore prior instructions via plain English, with no quote
  characters involved) — out of scope for this change, which only
  addresses the quote-character delimiter-breaking mechanism described in
  the proposal.
- Changing the wire protocol between `SpecRunner.Cli` and the `claude` CLI
  (stream-json framing is already correct and untouched).

## Decisions

**Escape at substitution time in `CommandTemplateRenderer`, not at the
GitHub-fetch layer or at each workflow-runner call site.**
Escaping in the renderer means every placeholder value — regardless of
which template or future call site consumes it — is protected by
construction. Escaping instead at the GitHub-fetch layer
(`GitHubService`) would leak a CLI-quoting concern into a layer that has
no notion of "this text will end up inside quotes," and would need to be
duplicated for every future untrusted-content source. Escaping at each of
the four workflow-runner call sites would require remembering to do it
consistently in four places (and in any future workflow runner) and
wouldn't cover the in-template `archive.txt` quoting.

**Escape algorithm: backslash-double, then quote-escape (`\` → `\\`, then
`"` → `\"`), applied per value before substitution.**
This is the standard two-step string-literal escaping convention (the
same one JSON/C-style string literals use) and is the only order that
avoids collisions: escaping `"` first and `\` second would double-escape
backslashes that were only introduced by the quote-escaping step,
corrupting the result. Escaping `\` first means any backslash the
attacker supplied becomes `\\` before any `"` is turned into `\"`, so the
two kinds of escaped sequences never get confused with each other or with
unescaped input.
Alternative considered: only escape `"` and leave `\` untouched. Rejected
because a value ending in a literal `\` immediately before a
quote-escaped `"` (i.e., raw input containing `\"`) would produce `\\"` —
which, if ever unescaped by a downstream consumer using the standard
convention, reads as "escaped backslash" + "unescaped quote", silently
reintroducing an unescaped terminator. Escaping backslashes first closes
this gap.

**Apply escaping uniformly to every value in the `values` dictionary,
not just known-untrusted ones.**
`CommandTemplateRenderer` has no way to know which caller-supplied values
are "trusted" (e.g. `spec_name`, which is already sanitized to be
folder-safe elsewhere) versus untrusted (`issue_title`, `issue_body`,
`instructions`). Values without `"` or `\` are unaffected by escaping, so
uniform application is both simpler and strictly safe.

**Do not remove or change the existing `$"\"{prompt}\""` wrapping in the
four workflow runners, or the `archive.txt` template's inline quoting.**
Both remain valid, useful quoting boundaries once the values placed
inside them are pre-escaped; there's no need to touch four separate call
sites (and risk behavioral drift between them) when the actual defect is
upstream, in the renderer they all share.

## Risks / Trade-offs

- [Risk: a future template or workflow runner embeds a substituted value
  in a context where backslash-escaping is not the right convention (e.g.
  literal shell invocation, HTML, SQL)] → Mitigation: none of the current
  four templates do this; if a future template needs different escaping
  semantics, that should be a separate, explicit change to
  `command-templates`, not a reason to special-case this one.
- [Risk: existing GitHub comments/issue content already containing raw
  backslashes will render slightly differently (doubled) after this
  change ships] → Mitigation: this only affects the text sent to the CLI
  agent as its prompt, not the original GitHub content, PR descriptions,
  or any user-visible surface; the model reading a doubled backslash in
  its own prompt is a negligible, cosmetic side effect of unambiguous
  escaping.

## Open Questions

None — the fix is a self-contained change to a single, well-tested
component (`CommandTemplateRenderer`).
