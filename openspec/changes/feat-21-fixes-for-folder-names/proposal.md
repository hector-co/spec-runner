## Why

Resolved spec/change names currently take the form `{issue-number}-{sanitized-title}`
(e.g. `45-add-login-page`) and are trusted blindly by every downstream
workflow step. In practice the CLI coding agent invoked by `/propose` can
create a change folder that doesn't exactly match that computed name (e.g.
it sanitizes the title differently, or truncates it). Nothing today verifies
the folder the agent actually created exists, so a mismatch silently breaks
`ReadCurrentAsync`, the PR body, and every later `/finalize` lookup that
depends on the stored spec name. This change (1) standardizes the expected
name format to `feat-{issue-number}-{spec-name}`, and (2) makes `/propose`
verify the resulting folder on disk — falling back to a prefix match and
persisting whatever name is actually found, so later steps never
recompute or guess it.

## What Changes

- Change `ISpecNameResolver`'s output format from `{issue-number}-{sanitized-title}`
  to `feat-{issue-number}-{sanitized-title}`.
- After the `/propose` CLI-agent session completes, verify that
  `openspec/changes/{expected-spec-name}` exists on disk.
  - If it exists, use it as-is.
  - If it doesn't, look for a directory under `openspec/changes/` whose
    name starts with `feat-{issue-number}-` and use the first one found as
    the actual spec name instead.
  - If no matching directory exists either way, treat this as a
    processing error for the comment (reported via the existing
    error-reporting path: `confused` reaction + human-readable reply +
    state-store status `error`) and stop processing that comment without
    committing, pushing, or opening a PR.
- The spec name resolved this way (not the originally-computed expected
  name) is what gets used to read `tasks.md`, build the PR body, and get
  persisted via `IStateStore.UpsertTrackedIssueAsync`.
- No other workflow step re-derives the spec name from the issue number or
  title: `/finalize` already reads `TrackedIssue.SpecName` from the state
  store rather than recomputing it, so it automatically picks up whatever
  name `/propose` actually resolved and stored.
- `/finalize`'s existing archived-`tasks.md` lookup (suffix-match on
  `openspec/changes/archive/*-{spec-name}/tasks.md`) already satisfies the
  requirement to locate the archived folder by the stored spec name — no
  change needed there.

## Capabilities

### New Capabilities
- `spec-folder-resolution`: verifies the expected `openspec/changes/{spec-name}`
  folder exists after a `/propose` run, falls back to a `feat-{issue-number}-`
  prefix match when it doesn't, and surfaces an error when neither is found.

### Modified Capabilities
- `state-store-schema`: `ISpecNameResolver`'s produced format changes from
  `{issue-number}-{sanitized-title}` to `feat-{issue-number}-{sanitized-title}`.
- `propose-workflow`: after the CLI-agent session completes, the workflow
  resolves the actual on-disk spec name via `spec-folder-resolution` before
  reading `tasks.md`, creating the draft PR, and persisting the tracked
  issue, and treats an unresolvable folder as a reported error rather than
  continuing.

## Impact

- `SpecRunner.Core.SpecNameResolver` (format change).
- `SpecRunner.Core.Abstractions.ISpecNameResolver` unit tests and
  `SpecRunner.Console.ProposeWorkflowRunner` (new resolution step wired in
  after the CLI-agent session, before commit/push/PR creation).
- New `ISpecFolderResolver` abstraction + implementation, registered in
  `Program.cs`.
- No changes required to `FinalizeWorkflowRunner` or `TasksFileReader` —
  their existing behavior already keys off the stored `TrackedIssue.SpecName`
  and already suffix-matches archived folders.

## Assumptions

- "Local storage" in the request refers to the existing SQLite-backed
  `IStateStore` / `TrackedIssue.SpecName` field, which already exists for
  exactly this purpose (avoiding recomputation across workflow steps) — no
  new storage mechanism is introduced.
- "Look for a folder that starts with `feat-<issue-number>-`" is resolved
  against `openspec/changes/` (the pre-archive location), since this check
  runs immediately after the `/propose` CLI-agent session and before
  anything is archived.
- When more than one directory matches the `feat-{issue-number}-` prefix
  fallback, the first one found (alphabetical `Directory.EnumerateDirectories`
  order) is used, since there's no modification-time signal to disambiguate
  freshly-created directories the way `ReadArchivedAsync` does for archived
  ones.
- Task 4.1 ("confirm all tests pass") is interpreted relative to the tests
  this change touches: `dotnet test` on this branch shows 8 pre-existing
  failures (`CommandTemplateRendererTests`, `ImplementWorkflowRunnerTests`,
  `UpdateWorkflowRunnerTests`, `FinalizeWorkflowRunnerTests`, and the
  unrelated assertion in `ProposeWorkflowRunnerTests.SuccessfulRunCommitsPushesCreatesDraftPrAndUpdatesState`)
  caused by `\n` vs `\r\n` in rendered command-template output — reproduced
  identically on a clean stash of this branch before any of this change's
  edits were applied, confirming it predates and is unrelated to this
  change. All tests added or modified for this change (`SpecNameResolverTests`,
  the new `SpecFolderResolverTests`, and the new/updated
  `ProposeWorkflowRunnerTests` cases) pass.
