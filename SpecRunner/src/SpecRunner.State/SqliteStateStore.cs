using Microsoft.Data.Sqlite;
using SpecRunner.Core.Abstractions;
using SpecRunner.Core.Models;

namespace SpecRunner.State;

public class SqliteStateStore : IStateStore
{
    private readonly string _connectionString;

    public SqliteStateStore(string databaseFilePath)
    {
        var directory = Path.GetDirectoryName(databaseFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder { DataSource = databaseFilePath }.ToString();
    }

    public async Task<TrackedIssue?> FindByIssueNumberAsync(int issueNumber, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT Id, IssueNumber, SpecName, PrNumber, CreatedAtUtc, UpdatedAtUtc
            FROM TrackedIssues
            WHERE IssueNumber = $value
            """;
        return await LoadIssueAsync(connection, sql, "$value", issueNumber, cancellationToken);
    }

    public async Task<TrackedIssue?> FindByPrNumberAsync(int prNumber, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT Id, IssueNumber, SpecName, PrNumber, CreatedAtUtc, UpdatedAtUtc
            FROM TrackedIssues
            WHERE PrNumber = $value
            """;
        return await LoadIssueAsync(connection, sql, "$value", prNumber, cancellationToken);
    }

    public async Task<TrackedIssue?> FindByCommentIdAsync(long commentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT ti.Id, ti.IssueNumber, ti.SpecName, ti.PrNumber, ti.CreatedAtUtc, ti.UpdatedAtUtc
            FROM TrackedIssues ti
            INNER JOIN TrackedComments tc ON tc.TrackedIssueId = ti.Id
            WHERE tc.CommentId = $value
            """;
        return await LoadIssueAsync(connection, sql, "$value", commentId, cancellationToken);
    }

    public async Task<TrackedIssue> UpsertTrackedIssueAsync(TrackedIssue issue, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        const string sql = """
            INSERT INTO TrackedIssues (IssueNumber, SpecName, PrNumber, CreatedAtUtc, UpdatedAtUtc)
            VALUES ($issueNumber, $specName, $prNumber, $now, $now)
            ON CONFLICT(IssueNumber) DO UPDATE SET
                PrNumber = excluded.PrNumber,
                UpdatedAtUtc = excluded.UpdatedAtUtc
            """;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = sql;
            command.Parameters.AddWithValue("$issueNumber", issue.IssueNumber);
            command.Parameters.AddWithValue("$specName", issue.SpecName);
            command.Parameters.AddWithValue("$prNumber", (object?)issue.PrNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("$now", now.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        const string selectSql = """
            SELECT Id, IssueNumber, SpecName, PrNumber, CreatedAtUtc, UpdatedAtUtc
            FROM TrackedIssues
            WHERE IssueNumber = $value
            """;
        var result = await LoadIssueAsync(connection, selectSql, "$value", issue.IssueNumber, cancellationToken);
        return result ?? throw new InvalidOperationException($"Upserted tracked issue {issue.IssueNumber} could not be reloaded.");
    }

    public async Task<TrackedComment> UpsertCommentAsync(int issueNumber, TrackedComment comment, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        long trackedIssueId;
        await using (var lookupCommand = connection.CreateCommand())
        {
            lookupCommand.CommandText = "SELECT Id FROM TrackedIssues WHERE IssueNumber = $issueNumber";
            lookupCommand.Parameters.AddWithValue("$issueNumber", issueNumber);
            var result = await lookupCommand.ExecuteScalarAsync(cancellationToken);
            if (result is not long id)
            {
                throw new InvalidOperationException($"No tracked issue found for issue number {issueNumber}.");
            }

            trackedIssueId = id;
        }

        const string sql = """
            INSERT INTO TrackedComments (TrackedIssueId, CommentId, CommentKind, Status, CreatedAtUtc, UpdatedAtUtc)
            VALUES ($trackedIssueId, $commentId, $commentKind, $status, $now, $now)
            ON CONFLICT(CommentId) DO UPDATE SET
                Status = excluded.Status,
                UpdatedAtUtc = excluded.UpdatedAtUtc
            """;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = sql;
            command.Parameters.AddWithValue("$trackedIssueId", trackedIssueId);
            command.Parameters.AddWithValue("$commentId", comment.CommentId);
            command.Parameters.AddWithValue("$commentKind", comment.CommentKind.ToString());
            command.Parameters.AddWithValue("$status", comment.Status.ToString());
            command.Parameters.AddWithValue("$now", now.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return await LoadCommentByCommentIdAsync(connection, comment.CommentId, cancellationToken)
            ?? throw new InvalidOperationException($"Upserted comment {comment.CommentId} could not be reloaded.");
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await ExecuteNonQueryAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken);
        await ExecuteNonQueryAsync(connection, "PRAGMA busy_timeout=5000;", cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        return connection;
    }

    private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
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
            """;

        await ExecuteNonQueryAsync(connection, sql, cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<TrackedIssue?> LoadIssueAsync(
        SqliteConnection connection,
        string sql,
        string paramName,
        object paramValue,
        CancellationToken cancellationToken)
    {
        long trackedIssueId;
        TrackedIssue issue;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = sql;
            command.Parameters.AddWithValue(paramName, paramValue);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            trackedIssueId = reader.GetInt64(0);
            issue = ReadTrackedIssue(reader);
        }

        var comments = await LoadCommentsAsync(connection, trackedIssueId, cancellationToken);
        return issue with { Comments = comments };
    }

    private static async Task<List<TrackedComment>> LoadCommentsAsync(
        SqliteConnection connection,
        long trackedIssueId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CommentId, CommentKind, Status, CreatedAtUtc, UpdatedAtUtc
            FROM TrackedComments
            WHERE TrackedIssueId = $trackedIssueId
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$trackedIssueId", trackedIssueId);

        var comments = new List<TrackedComment>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            comments.Add(ReadTrackedComment(reader));
        }

        return comments;
    }

    private static async Task<TrackedComment?> LoadCommentByCommentIdAsync(
        SqliteConnection connection,
        long commentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CommentId, CommentKind, Status, CreatedAtUtc, UpdatedAtUtc
            FROM TrackedComments
            WHERE CommentId = $commentId
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$commentId", commentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadTrackedComment(reader) : null;
    }

    private static TrackedIssue ReadTrackedIssue(SqliteDataReader reader)
    {
        var issueNumber = reader.GetInt32(1);
        var specName = reader.GetString(2);
        var prNumber = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);
        var createdAtUtc = DateTimeOffset.Parse(reader.GetString(4));
        var updatedAtUtc = DateTimeOffset.Parse(reader.GetString(5));

        return new TrackedIssue(issueNumber, specName)
        {
            PrNumber = prNumber,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc
        };
    }

    private static TrackedComment ReadTrackedComment(SqliteDataReader reader)
    {
        var commentId = reader.GetInt64(0);
        var commentKind = Enum.Parse<CommentKind>(reader.GetString(1));
        var status = Enum.Parse<CommentStatus>(reader.GetString(2));
        var createdAtUtc = DateTimeOffset.Parse(reader.GetString(3));
        var updatedAtUtc = DateTimeOffset.Parse(reader.GetString(4));

        return new TrackedComment(commentId, commentKind, status)
        {
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = updatedAtUtc
        };
    }
}
