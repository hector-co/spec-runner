## Context

SpecRunner today has no root-level `README.md`. Everything an operator
needs — how to build it, where to run it from, what `appsettings.json`
needs, and how the comment-driven flow works — is scattered across
`build/build.ps1`, `SpecRunner/src/SpecRunner.Console/appsettings.json`,
and the individual workflow runners
(`ProposeWorkflowRunner`, `ImplementWorkflowRunner`, `UpdateWorkflowRunner`,
`FinalizeWorkflowRunner`) plus their `openspec/specs/*` capability docs.
This is a documentation-only change: no code, build script, or
configuration schema changes.

Two existing facts constrain the README's "where to deploy" section:
- `build/build.ps1` always publishes into `build/.specrunner` (see
  `openspec/specs/build-script/spec.md`); it never writes to the
  repository root.
- `appsettings.json` ships with `LocalRepositoryPath: "../"`
  (`SpecRunner/src/SpecRunner.Console/appsettings.json`), and
  `Program.cs` resolves the runtime state DB at
  `Path.Combine(options.LocalRepositoryPath, ".specrunner", "state.db")`.
  `"../"` only resolves to the repository root if the running executable
  itself sits one level below the root — i.e. in a `.specrunner` folder at
  the repository root, sibling to `openspec/`. This confirms the issue's
  stated expectation and is the deployment layout the README documents.
- The root `.gitignore` already contains both `/.specrunner` and
  `/build/.specrunner` (confirmed by inspection), so no `.gitignore` edit
  is needed — the README documents the existing entry rather than adding
  one.

## Goals / Non-Goals

**Goals:**
- Give a new operator everything needed to go from a clean clone to a
  running SpecRunner instance: build, deploy location, configuration,
  prerequisites (PAT, authenticated `claude` CLI, `openspec` CLI on
  `PATH`), and the meaning/order of each comment trigger.
- Keep the README's technical claims (file paths, config keys, defaults,
  triggers) accurate to the current source, so it doesn't drift into
  aspirational documentation.
- Make explicit where automation stops (`/finalize` marks the PR ready
  for review) and that merging is a manual, human step.

**Non-Goals:**
- No changes to `build/build.ps1`, `appsettings.json`, `.gitignore`, or
  any source file.
- Not a full contributor/architecture guide — that can follow later;
  this covers operating the shipped tool.
- Not a description of the OpenSpec CLI itself or how to author
  `openspec/specs/*` — only how SpecRunner invokes it.

## Decisions

- **Single root `README.md`, no separate `docs/` tree.** The task asks
  for one root-level file; the content (build, deploy, configure, flow)
  fits comfortably in one page with headers. Alternative considered:
  splitting into `docs/operations.md` — rejected as unnecessary
  indirection for a single-audience document at this stage.

- **Document the two-step build→deploy path rather than changing
  `build.ps1`.** `build.ps1` is spec'd (`build-script`) to always output
  to `build/.specrunner`. Rather than propose a build script change (out
  of scope for a README task, and a behavior change to a spec'd
  component), the README instructs the operator to copy/move
  `build/.specrunner` to `<repo-root>/.specrunner` after building. This
  keeps the change doc-only while still satisfying the issue's stated
  expectation that the running app lives at
  `<repo-root>/.specrunner`.

- **Configuration section enumerates every key actually bound**, sourced
  from `SpecRunnerOptions`, `CliAgentOptions`, and `OpenSpecCliOptions`
  (`SpecRunner.Core/Configuration/*.cs`), not just the four the issue
  calls out (PAT, repo URL, local path, `claude` CLI). Listing the full
  set (`BaseBranchName`, `TaskTimeout`, `PollingInterval`,
  `CliAgent:Executable`, `OpenSpecCli:Executable`) avoids a README that
  is technically incomplete the moment someone reads `appsettings.json`
  next to it.

- **Flow section is ordered as: `/propose` → `/implement`/`/update`
  (repeatable, any order, PR already open) → `/finalize`**, matching the
  four `IProposeWorkflowRunner`/`IImplementWorkflowRunner`/
  `IUpdateWorkflowRunner`/`IFinalizeWorkflowRunner` runners and their
  GitHub trigger comments (`/propose` on an issue; `/implement`,
  `/update`, `/finalize` on the PR it opens). The README states plainly
  that `/finalize` marks the PR ready for review and that merging is
  manual — SpecRunner performs no merge operation anywhere in the
  codebase.

- **New `readme` capability spec** rather than folding this into
  `build-script` or `app-configuration`. The README's content spans
  multiple existing capabilities (build, config, all four workflows); a
  dedicated capability keeps the requirement ("a root README describing
  X, Y, Z exists") independent of any one of them and avoids modifying
  requirements that aren't actually changing.

## Risks / Trade-offs

- [README drifts from source as workflows evolve] → Mitigation: the
  README's config table and flow section are written to name the actual
  option/class/trigger identifiers, so future changes to those areas
  (e.g. adding a new trigger) are easy to grep for and cross-check
  against the README during review.
- [Deploy-location instructions (copy `build/.specrunner` to repo root)
  are a manual step, not enforced by tooling] → Mitigation: documented
  as an explicit, single instruction with the exact target path
  (`<repo-root>/.specrunner`, sibling to `openspec/`), matching the
  already-`.gitignore`d path so nothing needs to change there.
