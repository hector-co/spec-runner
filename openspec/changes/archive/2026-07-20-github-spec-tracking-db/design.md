## Context

`SpecRunner.Core` currently defines `IStateStore` as a whole-document
load/save contract (`LoadAsync` / `SaveAsync` of a single
`SpecRunnerState { List<TrackedIssue> Issues }`), plus two read-only
lookups (`FindByIssueNumberAsync`, `FindByPrNumberAsync`).
`SpecRunner.State.JsonFileStateStore` implements this by reading/writing
one JSON file in full on every call. `Program.cs` wires it up by deriving
the file path from `SpecRunnerOptions.LocalRepositoryPath` as
`.specrunner/state.json` — there is no dedicated config key for the
state file path today, and this change keeps that convention (deriving
`.specrunner/state.db` instead).

This change only touches the schema/persistence layer
(`SpecRunner.Core` models + `IStateStore`, and its
`SpecRunner.State` implementation). It does not implement the
comment-driven workflow itself — `IGitService`/`IGitHubService` remain
`NotImplementedException` placeholders per `state-store-schema`'s
existing "Git and GitHub service contracts" requirement, which this
change does not touch.

## Goals / Non-Goals

**Goals:**
- Resolve a GitHub comment id directly to its owning tracked
  issue/spec record, in addition to the existing issue-number and
  PR-number lookups.
- Distinguish issue comments, PR issue comments, and PR review comments
  in the persisted comment record, since each is written back through a
  different GitHub API.
- Move persistence to SQLite so individual record reads/writes are
  atomic and durable, instead of rewriting one JSON file per mutation.
- Record created/updated timestamps on tracked issues and comments.

**Non-Goals:**
- Migrating existing `state.json` data into the new database (proposal
  marks this **BREAKING**; operators start with an empty store).
- Multi-process/multi-instance write concurrency — SpecRunner already
  operates against one local git clone at a time, so one SpecRunner
  process is assumed per database file.
- A general-purpose migration framework (EF Core migrations, DbUp,
  etc.) — schema is small and created idempotently at startup.
- Implementing the comment-processing workflow that will consume this
  store (issue/PR polling, status write-back) — out of scope here.

## Decisions

### 1. Use `Microsoft.Data.Sqlite` directly, not an ORM

Access SQLite via `Microsoft.Data.Sqlite` with hand-written SQL, rather
than `Microsoft.EntityFrameworkCore.Sqlite` or Dapper.

- The schema is two small tables with a fixed, well-understood shape;
  EF Core's change-tracking, migration tooling, and larger dependency
  surface aren't earning their cost here.
- The rest of the codebase has no ORM today (`JsonFileStateStore` uses
  `System.Text.Json` directly) — raw ADO.NET keeps the same
  "explicit, no framework magic" style.
- Alternative considered: EF Core Sqlite — rejected for now as
  heavier than needed; revisit if the schema grows materially (e.g.
  many more entities or relationships).

### 2. Replace whole-state Load/Save with per-record operations

Redesign `IStateStore` from `LoadAsync`/`SaveAsync` of the entire
`SpecRunnerState` graph to targeted operations:

```csharp
Task<TrackedIssue?> FindByIssueNumberAsync(int issueNumber, CancellationToken ct = default);
Task<TrackedIssue?> FindByPrNumberAsync(int prNumber, CancellationToken ct = default);
Task<TrackedIssue?> FindByCommentIdAsync(long commentId, CancellationToken ct = default);
Task<TrackedIssue> UpsertTrackedIssueAsync(TrackedIssue issue, CancellationToken ct = default);
Task<TrackedComment> UpsertCommentAsync(int issueNumber, TrackedComment comment, CancellationToken ct = default);
```

- A relational store is meant to be read/written a row at a time;
  reading every record to mutate one and writing the whole set back
  (the JSON-file pattern) defeats the reliability benefit SQLite is
  being adopted for and reintroduces a whole-document race window.
- `UpsertTrackedIssueAsync` inserts a new tracked issue or updates the
  mutable fields (`PrNumber`, `UpdatedAtUtc`) of an existing one keyed
  by `IssueNumber`. `UpsertCommentAsync` inserts a new comment or
  updates `Status`/`UpdatedAtUtc` of an existing one keyed by
  `CommentId`, under the given issue.
- This is a breaking change to `IStateStore` itself (not just the
  implementation), consistent with the proposal's **BREAKING** marker;
  `SpecRunner.State` and any callers (`Program.cs`, tests) are updated
  together.
- Alternative considered: keep `LoadAsync`/`SaveAsync` and back them
  with SQLite by serializing/deserializing the whole graph in one
  transaction — rejected because it keeps the whole-document race
  window the change is meant to remove, and forfeits row-level
  comment-id lookups without an in-memory scan.

### 3. Schema shape

Two tables, created idempotently (`CREATE TABLE IF NOT EXISTS`) the
first time the store is opened — no external migration step, since
there is no prior release's data to carry forward (see Non-Goals):

```sql
CREATE TABLE IF NOT EXISTS TrackedIssues (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    IssueNumber   INTEGER NOT NULL UNIQUE,
    SpecName      TEXT    NOT NULL,
    PrNumber      INTEGER NULL,
    CreatedAtUtc  TEXT    NOT NULL,
    UpdatedAtUtc  TEXT    NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_TrackedIssues_PrNumber
    ON TrackedIssues (PrNumber) WHERE PrNumber IS NOT NULL;

CREATE TABLE IF NOT EXISTS TrackedComments (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    TrackedIssueId INTEGER NOT NULL REFERENCES TrackedIssues (Id),
    CommentId      INTEGER NOT NULL UNIQUE,
    CommentKind    TEXT    NOT NULL,
    Status         TEXT    NOT NULL,
    CreatedAtUtc   TEXT    NOT NULL,
    UpdatedAtUtc   TEXT    NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_TrackedComments_TrackedIssueId
    ON TrackedComments (TrackedIssueId);
```

- `CommentKind` and `Status` are stored as `TEXT` (enum names, e.g.
  `IssueComment`/`PrIssueComment`/`PrReviewComment` and
  `Pending`/`Working`/`Done`/`Error`) rather than integers, so the
  database file is directly inspectable/debuggable with the `sqlite3`
  CLI — matches the existing `WriteIndented` JSON convention of
  favoring readability over compactness for a low-volume local store.
  `CommentKind` becomes a proper C# enum (currently a bare `string` on
  `TrackedComment`).
  `TrackedIssues.PrNumber` uses a partial unique index (`WHERE PrNumber
  IS NOT NULL`) rather than a plain `UNIQUE` constraint, since SQLite
  treats `NULL` as distinct in `UNIQUE` columns anyway, but the
  `WHERE` clause makes the intent (uniqueness only applies once a PR is
  linked) explicit.
- Timestamps are ISO-8601 UTC strings (`DateTimeOffset.ToString("O")`)
  since SQLite has no native datetime type — consistent, sortable, and
  human-readable in the raw file.
- `PRAGMA journal_mode=WAL` and a `busy_timeout` are set on connection
  open for durability and to avoid `SQLITE_BUSY` errors under the
  sequential-but-possibly-overlapping access pattern of a polling
  console app.

### 4. Connection lifetime

Open a new `SqliteConnection` per store operation (open, execute,
dispose) rather than holding one connection for the process lifetime.

- Matches `JsonFileStateStore`'s existing per-call I/O pattern, keeps
  the store stateless and safe to register as a singleton.
- SQLite connection open/close is cheap for a local file; this is not
  a high-throughput path (one comment/issue event at a time).

## Risks / Trade-offs

- [No formal migration tooling for future schema changes] → Track
  schema shape via `PRAGMA user_version`; keep changes additive
  (new nullable columns/tables) where possible; a breaking schema
  change gets its own future OpenSpec change with an explicit
  migration or reset step, same as this one.
- [Existing tracked issues/comments in `state.json` are dropped on
  upgrade] → Called out as **BREAKING** in the proposal; acceptable
  because the comment-driven workflow that would populate meaningful
  state is not implemented yet (`IGitService`/`IGitHubService` are
  still placeholders).
- [`IStateStore` interface shape changes, not just its implementation]
  → All in-repo callers (`Program.cs`, `SpecRunner.Tests`) are part of
  this same change's task list; no external consumers exist yet.
- [Hand-written SQL has no compile-time check against the schema] →
  Mitigated by test coverage per `IStateStore` member (round-trip,
  each lookup, upsert-updates-existing) against a real temp-file
  SQLite database, matching the existing `JsonFileStateStoreTests`
  style.

## Migration Plan

1. Add the SQLite package reference via `Directory.Packages.props`.
2. Introduce the new schema/model shapes in `SpecRunner.Core`
   (`CommentKind` enum, timestamp fields).
3. Implement the SQLite-backed `IStateStore` in `SpecRunner.State`,
   remove `JsonFileStateStore`.
4. Update `Program.cs` DI registration to construct the SQLite store
   from `.specrunner/state.db` under `LocalRepositoryPath`.
5. Replace `JsonFileStateStoreTests` with tests for the new store.
6. No rollback path is provided for already-deployed `state.json`
   files — this is a local dev-tool cache, not user data; operators
   re-run from a clean state after upgrading.

## Open Questions

None — scope is limited to the persistence layer described above.
