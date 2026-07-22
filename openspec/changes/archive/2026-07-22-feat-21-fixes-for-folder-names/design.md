## Context

`ProposeWorkflowRunner.ProcessCommentAsync` computes `specName` once, via
`ISpecNameResolver.Resolve(issueNumber, issueTitle)`, before the CLI-agent
session starts, and then uses that same in-memory value for everything
after: reading `tasks.md` (`ITasksFileReader.ReadCurrentAsync`), the draft
PR body, and the value persisted into `IStateStore` as
`TrackedIssue.SpecName`. Nothing checks that the CLI agent actually created
`openspec/changes/{specName}/` with that exact name — the assumption is
that the sanitization the agent's OpenSpec CLI applies to the change name
it's given always matches `SpecNameResolver`'s own sanitization exactly.

`FinalizeWorkflowRunner` never recomputes the spec name; it reads
`TrackedIssue.SpecName` from the state store (`FindByPrNumberAsync`) and
passes it straight through to `ITasksFileReader.ReadArchivedAsync`, which
already resolves the archived folder by suffix match
(`*-{spec-name}`) rather than assuming an exact folder name. That part of
the pipeline is already tolerant of the on-disk name differing from what
was originally computed, as long as the *stored* name is correct.

So the only gap is between "CLI agent finishes" and "name gets stored":
`/propose` needs to confirm what got created on disk and store *that*,
instead of trusting the pre-computed name.

## Goals / Non-Goals

**Goals:**
- Change the expected spec/change name format to `feat-{issue-number}-{sanitized-title}`.
- After the `/propose` CLI-agent session completes, confirm the actual
  on-disk folder name under `openspec/changes/` and use it for the rest of
  that run (reading `tasks.md`, PR body, state-store persistence).
- Fail the comment cleanly (existing error-reporting path) if no matching
  folder can be found at all, rather than silently producing an empty PR
  body or a state-store record pointing at a nonexistent folder.

**Non-Goals:**
- Not changing `/finalize`'s archived-folder lookup — `TasksFileReader.ReadArchivedAsync`'s
  suffix-match already satisfies "look for the folder that ends with
  `<spec-name>`" and needs no changes.
- Not changing how `/finalize` resolves the spec name — it already reads
  `TrackedIssue.SpecName` from the state store and never recomputes it.
- Not adding a general-purpose fuzzy-matching or rename-detection system;
  the fallback is a simple `feat-{issue-number}-` prefix scan.

## Decisions

**1. New `ISpecFolderResolver` abstraction, not inline logic in `ProposeWorkflowRunner`.**
Mirrors the existing shape of `ITasksFileReader`/`TasksFileReader`: a small
interface in `SpecRunner.Core.Abstractions`, a filesystem-backed
implementation in `SpecRunner.Console` (constructed from
`SpecRunnerOptions.LocalRepositoryPath`), registered as a singleton in
`Program.cs`. Keeps `ProposeWorkflowRunner` focused on orchestration and
keeps the filesystem-scanning logic independently unit-testable, consistent
with how `TasksFileReader`'s suffix-matching is already isolated and tested.

Alternative considered: fold the check into `ITasksFileReader` (it already
touches the same `openspec/changes/` paths). Rejected because
`ITasksFileReader` reads file *content* given an already-known name;
resolving *which* name is correct is a distinct responsibility, and
`ReadCurrentAsync`/`ReadArchivedAsync`'s contracts (return `null` when
missing) don't fit "resolve or error" semantics.

**2. Resolution API shape: `Task<string> ResolveAsync(string expectedSpecName, int issueNumber, CancellationToken)`.**
Returns the actual spec name to use, or throws when nothing matches
(handled by `ProposeWorkflowRunner`'s existing generic `catch (Exception)`
block — the same path already used for a non-`Completed` CLI-agent
session — so no new error-handling branch is needed in the workflow).

Alternative considered: return a nullable/`Result`-style value and have the
caller branch explicitly. Rejected because every other "this shouldn't
happen" condition in `ProposeWorkflowRunner` (e.g. CLI session ending in a
non-`Completed` state) is already surfaced by throwing and relying on the
shared error-reporting path — matching that convention keeps the one new
failure mode consistent with the rest of the method instead of introducing
a second error-handling shape.

**3. Fallback match: first directory whose name starts with `feat-{issue-number}-`, found via plain `Directory.EnumerateDirectories` + `StartsWith`, no modified-time tie-break.**
Unlike `TasksFileReader.ReadArchivedAsync` (which picks the most-recently
modified match among possible duplicates because archived folders
accumulate over the repo's history), this fallback runs immediately after
a single fresh `/propose` run on a clean, freshly-reset branch — realistic
duplicate matches shouldn't occur. Keeping it a simple first-match avoids
adding unused complexity (per proposal's noted assumption).

**4. Format change is a one-line edit to `SpecNameResolver.Resolve`.**
`return $"feat-{issueNumber}-{collapsed}";` instead of
`$"{issueNumber}-{collapsed}"`. No new sanitization rules — `feat-` is a
fixed, already-valid literal prefix, so the existing regex pipeline
(whitespace → dash, strip invalid chars, collapse dashes) is untouched.

**5. Resolution runs after CLI-agent completion, before commit/push/PR creation.**
Placed right after the existing `session.State != CliAgentSessionState.Completed`
check in `ProcessCommentAsync`, so a folder-resolution failure prevents a
commit, push, or draft PR from being created for a run that produced no
identifiable spec folder — avoiding a half-finished PR that later
`/finalize` calls can't resolve either.

## Risks / Trade-offs

- **[Risk]** The CLI agent creates a folder that matches neither the exact
  expected name nor the `feat-{issue-number}-` prefix (e.g. it ignores the
  `spec_name` argument entirely). → Mitigation: this surfaces as the new
  "no folder found" error path — reported on the issue comment like any
  other processing failure, rather than silently proceeding with a wrong
  name.
- **[Risk]** First-match fallback picks the wrong folder if the agent
  somehow produces two directories with the same `feat-{issue-number}-`
  prefix in one run. → Mitigation: accepted per the proposal's assumption;
  this scenario requires the agent to create multiple change folders for a
  single issue in a single run, which is out of scope for this change to
  guard against.

## Migration Plan

Purely additive/behavioral — no data migration. Existing `TrackedIssue`
rows in the SQLite state store keep whatever `SpecName` they already have
(old format, no `feat-` prefix); only *newly* processed `/propose` runs get
resolved names in the new format. `/finalize` doesn't care about the
format, only the stored value, so old and new rows behave identically
going forward.
