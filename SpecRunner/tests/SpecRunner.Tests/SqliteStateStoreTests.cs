using Microsoft.Data.Sqlite;
using SpecRunner.Core.Models;
using SpecRunner.State;

namespace SpecRunner.Tests;

public class SqliteStateStoreTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _filePath;

    public SqliteStateStoreTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _filePath = Path.Combine(_tempDirectory, "state.db");
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools native connections by default, which keeps
        // the WAL/SHM files locked after a SqliteConnection is disposed.
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task UpsertThenFindByIssueNumberRoundTripsTrackedIssue()
    {
        var store = new SqliteStateStore(_filePath);
        var issue = new TrackedIssue(45, "45-add-login-page") { PrNumber = 12, BranchName = "feature/45" };

        await store.UpsertTrackedIssueAsync(issue);
        var found = await store.FindByIssueNumberAsync(45);

        Assert.NotNull(found);
        Assert.Equal(45, found!.IssueNumber);
        Assert.Equal("45-add-login-page", found.SpecName);
        Assert.Equal("feature/45", found.BranchName);
        Assert.Equal(12, found.PrNumber);
        Assert.Empty(found.Comments);
    }

    [Fact]
    public async Task FindByPrNumberReturnsMatchingRecord()
    {
        var store = new SqliteStateStore(_filePath);
        var issue = new TrackedIssue(45, "45-add-login-page") { PrNumber = 12 };
        await store.UpsertTrackedIssueAsync(issue);

        var found = await store.FindByPrNumberAsync(12);

        Assert.NotNull(found);
        Assert.Equal(45, found!.IssueNumber);
    }

    [Fact]
    public async Task FindByCommentIdReturnsOwningTrackedIssue()
    {
        var store = new SqliteStateStore(_filePath);
        await store.UpsertTrackedIssueAsync(new TrackedIssue(45, "45-add-login-page") { PrNumber = 12 });
        var comment = new TrackedComment(9001, CommentKind.PrReviewComment, CommentStatus.Working);
        await store.UpsertCommentAsync(12, comment);

        var found = await store.FindByCommentIdAsync(9001);

        Assert.NotNull(found);
        Assert.Equal(45, found!.IssueNumber);
        var foundComment = Assert.Single(found.Comments);
        Assert.Equal(9001, foundComment.CommentId);
    }

    [Fact]
    public async Task UpsertingExistingTrackedIssueUpdatesInPlace()
    {
        var store = new SqliteStateStore(_filePath);
        await store.UpsertTrackedIssueAsync(new TrackedIssue(45, "45-add-login-page"));

        var updated = await store.UpsertTrackedIssueAsync(new TrackedIssue(45, "45-add-login-page") { PrNumber = 12 });

        Assert.Equal(12, updated.PrNumber);
        var found = await store.FindByIssueNumberAsync(45);
        Assert.Equal(12, found!.PrNumber);
        Assert.True(found.UpdatedAtUtc >= found.CreatedAtUtc);
    }

    [Fact]
    public async Task UpsertingExistingTrackedIssueUpdatesSpecNameAndBranchName()
    {
        var store = new SqliteStateStore(_filePath);
        await store.UpsertTrackedIssueAsync(new TrackedIssue(45, "feat-45-add-login-page") { BranchName = "feature/45" });

        var updated = await store.UpsertTrackedIssueAsync(
            new TrackedIssue(45, "45-add-login-page") { BranchName = "feature/45-2" });

        Assert.Equal("45-add-login-page", updated.SpecName);
        Assert.Equal("feature/45-2", updated.BranchName);
        var found = await store.FindByIssueNumberAsync(45);
        Assert.Equal("45-add-login-page", found!.SpecName);
        Assert.Equal("feature/45-2", found.BranchName);
    }

    [Fact]
    public async Task PreMigrationDatabaseFileWithoutBranchNameColumnIsUpgradedInPlace()
    {
        Directory.CreateDirectory(_tempDirectory);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _filePath }.ToString();
        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE TrackedIssues (
                    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    IssueNumber   INTEGER NOT NULL UNIQUE,
                    SpecName      TEXT    NOT NULL,
                    PrNumber      INTEGER NULL,
                    CreatedAtUtc  TEXT    NOT NULL,
                    UpdatedAtUtc  TEXT    NOT NULL
                );

                INSERT INTO TrackedIssues (IssueNumber, SpecName, PrNumber, CreatedAtUtc, UpdatedAtUtc)
                VALUES (45, '45-add-login-page', NULL, '2026-01-01T00:00:00+00:00', '2026-01-01T00:00:00+00:00');
                """;
            await command.ExecuteNonQueryAsync();
        }

        SqliteConnection.ClearAllPools();

        var store = new SqliteStateStore(_filePath);
        var found = await store.FindByIssueNumberAsync(45);

        Assert.NotNull(found);
        Assert.Equal("45-add-login-page", found!.SpecName);
        Assert.Equal(string.Empty, found.BranchName);

        var updated = await store.UpsertTrackedIssueAsync(
            new TrackedIssue(45, "45-add-login-page") { BranchName = "feature/45" });
        Assert.Equal("feature/45", updated.BranchName);
    }

    [Fact]
    public async Task UpsertingExistingTrackedCommentUpdatesStatus()
    {
        var store = new SqliteStateStore(_filePath);
        await store.UpsertTrackedIssueAsync(new TrackedIssue(45, "45-add-login-page") { PrNumber = 12 });
        await store.UpsertCommentAsync(12, new TrackedComment(9001, CommentKind.IssueComment, CommentStatus.Pending));

        await store.UpsertCommentAsync(12, new TrackedComment(9001, CommentKind.IssueComment, CommentStatus.Done));

        var found = await store.FindByIssueNumberAsync(45);
        var comment = Assert.Single(found!.Comments);
        Assert.Equal(CommentStatus.Done, comment.Status);
    }

    [Fact]
    public async Task CommentKindIsPersistedAndReadBackCorrectly()
    {
        var store = new SqliteStateStore(_filePath);
        await store.UpsertTrackedIssueAsync(new TrackedIssue(45, "45-add-login-page") { PrNumber = 12 });
        await store.UpsertCommentAsync(12, new TrackedComment(1, CommentKind.IssueComment, CommentStatus.Pending));
        await store.UpsertCommentAsync(12, new TrackedComment(2, CommentKind.PrIssueComment, CommentStatus.Pending));
        await store.UpsertCommentAsync(12, new TrackedComment(3, CommentKind.PrReviewComment, CommentStatus.Pending));

        var found = await store.FindByIssueNumberAsync(45);

        Assert.Equal(CommentKind.IssueComment, found!.Comments.Single(c => c.CommentId == 1).CommentKind);
        Assert.Equal(CommentKind.PrIssueComment, found.Comments.Single(c => c.CommentId == 2).CommentKind);
        Assert.Equal(CommentKind.PrReviewComment, found.Comments.Single(c => c.CommentId == 3).CommentKind);
    }

    [Fact]
    public async Task UpsertTrackedIssueSupportsARecordWithNoIssueNumber()
    {
        var store = new SqliteStateStore(_filePath);
        var issue = new TrackedIssue(null, "add-csv-export") { PrNumber = 12, BranchName = "contributor/csv-export" };

        await store.UpsertTrackedIssueAsync(issue);
        var found = await store.FindByPrNumberAsync(12);

        Assert.NotNull(found);
        Assert.Null(found!.IssueNumber);
        Assert.Equal("add-csv-export", found.SpecName);
        Assert.Equal("contributor/csv-export", found.BranchName);
    }

    [Fact]
    public async Task UpsertCommentIsKeyedByPrNumberForARecordWithNoIssueNumber()
    {
        var store = new SqliteStateStore(_filePath);
        await store.UpsertTrackedIssueAsync(new TrackedIssue(null, "add-csv-export") { PrNumber = 12 });

        await store.UpsertCommentAsync(12, new TrackedComment(9001, CommentKind.PrIssueComment, CommentStatus.Working));

        var found = await store.FindByPrNumberAsync(12);
        var comment = Assert.Single(found!.Comments);
        Assert.Equal(9001, comment.CommentId);
    }

    [Fact]
    public async Task PreMigrationDatabaseFileWithNotNullIssueNumberIsUpgradedInPlace()
    {
        Directory.CreateDirectory(_tempDirectory);
        var connectionString = new SqliteConnectionStringBuilder { DataSource = _filePath }.ToString();
        await using (var connection = new SqliteConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE TrackedIssues (
                    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    IssueNumber   INTEGER NOT NULL UNIQUE,
                    SpecName      TEXT    NOT NULL,
                    BranchName    TEXT    NOT NULL DEFAULT '',
                    PrNumber      INTEGER NULL,
                    CreatedAtUtc  TEXT    NOT NULL,
                    UpdatedAtUtc  TEXT    NOT NULL
                );

                INSERT INTO TrackedIssues (IssueNumber, SpecName, BranchName, PrNumber, CreatedAtUtc, UpdatedAtUtc)
                VALUES (45, '45-add-login-page', 'feature/45', 12, '2026-01-01T00:00:00+00:00', '2026-01-01T00:00:00+00:00');
                """;
            await command.ExecuteNonQueryAsync();
        }

        SqliteConnection.ClearAllPools();

        var store = new SqliteStateStore(_filePath);
        var existing = await store.FindByIssueNumberAsync(45);

        Assert.NotNull(existing);
        Assert.Equal(45, existing!.IssueNumber);
        Assert.Equal("feature/45", existing.BranchName);

        var adopted = await store.UpsertTrackedIssueAsync(
            new TrackedIssue(null, "add-csv-export") { PrNumber = 13, BranchName = "contributor/csv-export" });

        Assert.Null(adopted.IssueNumber);
        var found = await store.FindByPrNumberAsync(13);
        Assert.NotNull(found);
        Assert.Null(found!.IssueNumber);
    }
}
