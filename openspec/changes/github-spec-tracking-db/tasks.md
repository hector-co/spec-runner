## 1. Dependencies

- [ ] 1.1 Add `Microsoft.Data.Sqlite` to `SpecRunner/Directory.Packages.props` and reference it from `SpecRunner.State.csproj`

## 2. Core model changes

- [ ] 2.1 Add a `CommentKind` enum (`IssueComment`, `PrIssueComment`, `PrReviewComment`) to `SpecRunner.Core.Models`, replacing the bare `string CommentKind` on `TrackedComment`
- [ ] 2.2 Add `CreatedAtUtc`/`UpdatedAtUtc` (`DateTimeOffset`) to `TrackedIssue` and `TrackedComment`
- [ ] 2.3 Remove `SpecRunnerState` if no longer referenced once `IStateStore` no longer exposes whole-state load/save (keep if still used elsewhere)

## 3. IStateStore interface

- [ ] 3.1 Replace `LoadAsync`/`SaveAsync` on `IStateStore` with `UpsertTrackedIssueAsync(TrackedIssue, CancellationToken)` and `UpsertCommentAsync(int issueNumber, TrackedComment, CancellationToken)`
- [ ] 3.2 Add `FindByCommentIdAsync(long commentId, CancellationToken)` to `IStateStore`
- [ ] 3.3 Keep `FindByIssueNumberAsync` and `FindByPrNumberAsync` on `IStateStore`, updated to return the new `TrackedIssue` shape

## 4. SQLite-backed implementation

- [ ] 4.1 Delete `SpecRunner.State.JsonFileStateStore`
- [ ] 4.2 Add a `SqliteStateStore` in `SpecRunner.State` that opens a `SqliteConnection` per operation against a configurable database file path, creating the parent directory if needed
- [ ] 4.3 Implement idempotent schema creation (`CREATE TABLE IF NOT EXISTS` for `TrackedIssues` and `TrackedComments`, plus the `IX_TrackedIssues_PrNumber` and `IX_TrackedComments_TrackedIssueId` indexes) run on first connection open
- [ ] 4.4 Set `PRAGMA journal_mode=WAL` and a `busy_timeout` on connection open
- [ ] 4.5 Implement `FindByIssueNumberAsync`, `FindByPrNumberAsync`, and `FindByCommentIdAsync`, each returning a `TrackedIssue` with its full `Comments` collection populated via a join
- [ ] 4.6 Implement `UpsertTrackedIssueAsync` (insert on new `IssueNumber`, update `PrNumber`/`UpdatedAtUtc` on existing)
- [ ] 4.7 Implement `UpsertCommentAsync` (insert on new `CommentId` under the given issue, update `Status`/`UpdatedAtUtc` on existing)

## 5. Wiring

- [ ] 5.1 Update `Program.cs` DI registration to construct `SqliteStateStore` from `.specrunner/state.db` under `SpecRunnerOptions.LocalRepositoryPath`, replacing the `JsonFileStateStore` registration
- [ ] 5.2 Update `DependencyInjectionSmokeTests` to register `SqliteStateStore` instead of `JsonFileStateStore`

## 6. Tests

- [ ] 6.1 Delete `JsonFileStateStoreTests`
- [ ] 6.2 Add `SqliteStateStoreTests` covering: upsert-then-find-by-issue-number round trip, find-by-PR-number, find-by-comment-id, upsert-updates-existing-issue (PR number change), upsert-updates-existing-comment (status change), and comment-kind persisted/read back correctly
- [ ] 6.3 Run `dotnet test SpecRunner/SpecRunner.sln` and confirm all tests pass

## 7. Verification

- [ ] 7.1 Run `dotnet build SpecRunner/SpecRunner.sln` and confirm a clean build with no compilation errors
- [ ] 7.2 Confirm no `PackageReference` in `SpecRunner.State.csproj` declares a `Version` attribute (central package management still respected)
