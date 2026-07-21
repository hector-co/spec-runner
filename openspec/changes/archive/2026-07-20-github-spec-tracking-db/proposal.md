## Why

The current `IStateStore` schema (`SpecRunnerState` / `TrackedIssue` /
`TrackedComment`) and its `JsonFileStateStore` implementation can only
resolve a record by issue number or PR number, and a whole-file
read-modify-write on a single JSON document is not safe against partial
writes or concurrent access. The comment-driven workflow needs to resolve
an inbound GitHub issue comment, PR comment, or PR review comment directly
to its tracked spec/change and current status, and needs that lookup and
each status update to be reliable. A local relational store (SQLite)
gives per-row lookups by comment id, atomic updates, and durability that
a flat JSON file cannot.

## What Changes

- Replace `JsonFileStateStore` with a SQLite-backed `IStateStore`
  implementation in `SpecRunner.State`, using a configurable database
  file path. **BREAKING**: existing `state.json` files are not migrated;
  the JSON-file store is removed.
- Extend the tracked-record schema so a comment (issue comment, PR issue
  comment, or PR review comment) can be looked up directly by its GitHub
  comment id and resolved to its owning spec/change record, without first
  knowing the issue or PR number.
- Add a distinct comment-kind classification (issue comment vs. PR
  comment vs. PR review comment) to the persisted comment record, so the
  correct GitHub API is used when a status indicator is later written
  back to that comment.
- Add created/updated timestamps to tracked records and tracked comments
  so history and staleness can be inspected.
- Preserve the existing `IStateStore` lookups by issue number and by PR
  number, and add a lookup by comment id.
- Update dependency injection registration in `SpecRunner.Console` to
  configure and register the SQLite-backed store in place of the
  JSON-file store.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `state-store-schema`: the persistence interface and schema gain a
  comment-id lookup, comment-kind classification, and timestamps, and the
  default implementation moves from a JSON file to a local SQLite
  database.

## Impact

- Affected code: `SpecRunner.Core.Abstractions.IStateStore`,
  `SpecRunner.Core.Models.SpecRunnerState`/`TrackedIssue`/`TrackedComment`,
  `SpecRunner.State.JsonFileStateStore` (removed), new
  `SpecRunner.State` SQLite implementation, DI registration in
  `SpecRunner.Console`.
- Affected config: none — the store continues to derive its file path
  from `LocalRepositoryPath` (e.g. `.specrunner/state.db` instead of
  `.specrunner/state.json`), no new configuration key is introduced.
- New dependency: a SQLite access package (e.g. `Microsoft.Data.Sqlite`
  or `Microsoft.EntityFrameworkCore.Sqlite`), added via
  `SpecRunner/Directory.Packages.props` per the solution's central
  package management convention.
- Test impact: `SpecRunner.Tests` coverage for `JsonFileStateStore` is
  replaced with coverage for the SQLite-backed implementation, including
  the new comment-id lookup.
