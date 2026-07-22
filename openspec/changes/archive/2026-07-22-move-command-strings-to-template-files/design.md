## Context

Each workflow runner (`ProposeWorkflowRunner`, `ImplementWorkflowRunner`,
`UpdateWorkflowRunner`, `FinalizeWorkflowRunner`) builds the CLI-agent
prompt with a single C# interpolated string passed straight into
`session.StartAsync(...)`, e.g.:

```csharp
await session.StartAsync($"\"/opsx-propose {specName}\n{comment.IssueBody}\"", ...);
```

This is fine for one-line prompts but doesn't scale: adding a shared
multi-line instruction (like the "unattended run" note this change
introduces) means editing four call sites, and any future prompt
addition means another code change and rebuild. `SpecRunner.Console`
already has a precedent for shipping non-code text alongside the
assembly (`appsettings.json`, copied via a `Content` item with
`CopyToOutputDirectory`), and `SpecRunner.Core.Abstractions` /
`SpecRunner.Console` already split interface-from-implementation for
similar single-purpose helpers (`ISpecNameResolver`/`SpecNameResolver`,
`ITasksFileReader`/`TasksFileReader`).

## Goals / Non-Goals

**Goals:**
- Move each workflow's command text out of C# source into its own
  template file, readable and editable without a rebuild's worth of
  ceremony (still requires a rebuild to pick up, since files are copied
  at build time, but no longer requires editing C# string literals).
- Support named placeholders in a template that get substituted with a
  runtime value (e.g. `{{spec_name}}`).
- Give every command template a single trailing block of "unattended
  run" instructions, authored once per file rather than assembled by
  string concatenation in code, so the full prompt text a template
  produces is visible by reading that one file.

**Non-Goals:**
- No general-purpose templating language (conditionals, loops, partials).
  Straight token substitution is all four existing commands need.
- No runtime hot-reloading of templates while the process is running;
  templates are read fresh per render call (simple, and cheap enough at
  this call volume) but ship as build output, not user-editable config.
- Not changing *when* or *why* each workflow runs the CLI agent, only
  *how* the prompt text is produced.

## Decisions

**Template storage: plain-text `Content` files under
`SpecRunner.Console/CommandTemplates/`, copied to output directory.**
Alternative considered: embedded resources (`EmbeddedResource` +
`Assembly.GetManifestResourceStream`). Rejected because the project
already has a working, simple convention for shipping text alongside the
binary (`appsettings.json`), and plain files are easier to open, diff,
and edit than embedded resources, which is the whole point of this
change.

**Placeholder syntax: `{{token_name}}`, replaced via literal
`string.Replace`.** Alternative considered: `{token}` single-brace,
matching the issue's illustrative example (`archive <spec-name>` uses
`<...>`, but the repo's existing interpolated prompts don't use angle
brackets anywhere meaningful). Double curly braces are used because
they're a widely recognized template-placeholder convention and are
extremely unlikely to collide with literal text inside a command (none
of the four existing commands contain `{{` or `}}`). A missing token in
the supplied value set is a programmer error (workflow runner passed the
wrong token set for the template) and SHALL throw, not silently leave
the placeholder in the output.

**One interface, `ICommandTemplateRenderer`, in
`SpecRunner.Core.Abstractions`; one implementation,
`CommandTemplateRenderer`, in `SpecRunner.Console`.** Matches the
existing `ISpecNameResolver`/`SpecNameResolver` and
`ITasksFileReader`/`TasksFileReader` split, keeps `SpecRunner.Core` free
of file-system/build-output concerns, and keeps the workflow runners
(which already live in `SpecRunner.Console` and take constructor
dependencies on sibling interfaces) trivially testable against a fake.

**The trailing "unattended run" text lives inside each template file,
not appended by code.** The issue asks for this text at the end of
"every command"; putting it in each of the four template files (rather
than having `CommandTemplateRenderer` append a shared constant after
rendering) keeps the renderer a single-purpose token substitutor and
keeps each template file's content a complete, readable statement of
what gets sent to the CLI agent for that command — no need to jump to a
second place to see the full prompt. The cost is four copies of the same
block instead of one; that's an acceptable, explicit duplication given
there are only four commands and they change rarely.

**Escaped-double-quote wrapping stays in the workflow runner, not in the
template.** The existing prompt-quoting convention
(`$"\"{prompt}\""`) is about how `ICliAgentSession.StartAsync` expects
its argument, not about command content. Templates hold only the
unquoted command text; each workflow runner still wraps the rendered
result in escaped double quotes before calling `StartAsync`, exactly as
it wraps the interpolated string today.

**Template lookup key: a plain string name (`"propose"`, `"apply"`,
`"update"`, `"archive"`) resolved to
`CommandTemplates/{name}.txt`.** Simpler than an enum for four
call sites, and consistent with how `ITasksFileReader` already takes a
plain spec-name string rather than a typed identifier.

## Risks / Trade-offs

- [Missing/renamed template file at runtime → `FileNotFoundException`
  mid-workflow, after the `eyes` reaction is already posted] →
  `CommandTemplateRenderer` throws a clear, specific exception
  (including the resolved path) on a missing file; this surfaces through
  each workflow runner's existing catch-all error handling, which already
  reports failures back on the comment and records `error` status, so
  behavior on this failure mode is no different from any other
  `CliAgentSession` failure today.
- [Template content edited without updating the token set passed by the
  workflow runner (e.g. a typo in `{{spec_name}}`) → silent placeholder
  left in the prompt] → Mitigated by making `CommandTemplateRenderer`
  throw when the template contains a `{{...}}` token not present in the
  supplied replacement set, rather than leaving it unresolved.
- [Duplicating the unattended-run block across four files → future edits
  to that shared text require four coordinated changes] → Accepted
  trade-off (see Decisions); the four files are all
  `SpecRunner.Console/CommandTemplates/*.txt` and reviewed together, so
  drift is easy to catch in review. Not solving this with a shared
  constant to avoid adding an abstraction only four short files need.

## Migration Plan

- Add `ICommandTemplateRenderer` and `CommandTemplateRenderer`, register
  the implementation in `Program.cs` DI alongside the other singleton
  services.
- Add the four `CommandTemplates/*.txt` files and the corresponding
  `Content`/`CopyToOutputDirectory` entries in
  `SpecRunner.Console.csproj`.
- Update each workflow runner to take `ICommandTemplateRenderer` as a
  constructor dependency and render its prompt from the matching
  template instead of an interpolated string.
- Update existing workflow-runner tests' expected prompt strings to
  include the trailing unattended-run block, and add a fake
  `ICommandTemplateRenderer` (or use the real one against the shipped
  template files) wherever tests currently assert on the exact prompt
  passed to `StartAsync`.
- No data migration or deployment sequencing concerns: this only changes
  process-internal prompt construction, not persisted state or external
  contracts other than the CLI-agent prompt text itself.

## Open Questions

None outstanding — the boilerplate placement (in-file vs. appended by
code) and placeholder syntax are settled above.
