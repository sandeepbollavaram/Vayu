using System.Globalization;

using Microsoft.Data.Sqlite;

using Vayu.Core;

namespace Vayu.Memory;

/// <summary>
/// SQLite-backed implementation of <see cref="IAuditLogService"/>. The
/// schema is bootstrapped idempotently on first use; the database file
/// and its parent directory are created if missing.
/// </summary>
/// <remarks>
/// One connection per operation — Microsoft.Data.Sqlite pools the
/// underlying handle so the cost is small, and this avoids long-lived
/// locks that would block readers in the WinUI Logs page.
/// </remarks>
public sealed class SqliteAuditLogService : IAuditLogService
{
    private const string CreateSchemaSql = """
        CREATE TABLE IF NOT EXISTS Actions (
            Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
            TimestampUtc       TEXT    NOT NULL,
            CorrelationId      TEXT    NOT NULL,
            AgentName          TEXT    NULL,
            CommandText        TEXT    NULL,
            RiskLevel          INTEGER NOT NULL,
            PermissionDecision INTEGER NOT NULL,
            Status             INTEGER NOT NULL,
            RedactedDetails    TEXT    NULL,
            ErrorMessage       TEXT    NULL
        );
        CREATE INDEX IF NOT EXISTS IX_Actions_TimestampUtc  ON Actions (TimestampUtc);
        CREATE INDEX IF NOT EXISTS IX_Actions_CorrelationId ON Actions (CorrelationId);
        """;

    private const string SelectColumnsSql =
        "Id, TimestampUtc, CorrelationId, AgentName, CommandText, RiskLevel, PermissionDecision, Status, RedactedDetails, ErrorMessage";

    private readonly string _connectionString;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private bool _schemaReady;

    /// <summary>
    /// Constructs the service from <paramref name="options"/>. Expands
    /// environment variables in the database path and creates the
    /// parent directory if it does not exist.
    /// </summary>
    public SqliteAuditLogService(MemoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var path = Environment.ExpandEnvironmentVariables(options.DatabasePath);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
    }

    /// <inheritdoc />
    public async Task<ActionLogEntry> AppendAsync(ActionLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await using var conn = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Actions
                (TimestampUtc, CorrelationId, AgentName, CommandText, RiskLevel, PermissionDecision, Status, RedactedDetails, ErrorMessage)
            VALUES
                ($ts, $corr, $agent, $cmd, $risk, $perm, $status, $details, $err)
            RETURNING Id;
            """;
        cmd.Parameters.AddWithValue("$ts",      entry.TimestampUtc.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$corr",    entry.CorrelationId.ToString("D"));
        cmd.Parameters.AddWithValue("$agent",   (object?)entry.AgentName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cmd",     (object?)entry.CommandText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$risk",    (int)entry.RiskLevel);
        cmd.Parameters.AddWithValue("$perm",    (int)entry.PermissionDecision);
        cmd.Parameters.AddWithValue("$status",  (int)entry.Status);
        cmd.Parameters.AddWithValue("$details", (object?)entry.RedactedDetails ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$err",     (object?)entry.ErrorMessage ?? DBNull.Value);

        var idObj = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var id = Convert.ToInt64(idObj, CultureInfo.InvariantCulture);
        return entry with { Id = id };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActionLogEntry>> ListRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Array.Empty<ActionLogEntry>();
        }

        await using var conn = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {SelectColumnsSql} FROM Actions ORDER BY Id DESC LIMIT $limit;";
        cmd.Parameters.AddWithValue("$limit", limit);

        var results = new List<ActionLogEntry>(capacity: Math.Min(limit, 64));
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(MapRow(reader));
        }
        return results;
    }

    /// <inheritdoc />
    public async Task<ActionLogEntry?> FindByCorrelationAsync(Guid correlationId, CancellationToken cancellationToken = default)
    {
        await using var conn = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT {SelectColumnsSql} FROM Actions WHERE CorrelationId = $corr ORDER BY Id ASC LIMIT 1;";
        cmd.Parameters.AddWithValue("$corr", correlationId.ToString("D"));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? MapRow(reader) : null;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(conn, cancellationToken).ConfigureAwait(false);
        return conn;
    }

    private async Task EnsureSchemaAsync(SqliteConnection conn, CancellationToken cancellationToken)
    {
        if (_schemaReady)
        {
            return;
        }
        await _schemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaReady)
            {
                return;
            }
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = CreateSchemaSql;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            _schemaReady = true;
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    private static ActionLogEntry MapRow(SqliteDataReader r) => new()
    {
        Id                 = r.GetInt64(0),
        TimestampUtc       = DateTimeOffset.Parse(r.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        CorrelationId      = Guid.Parse(r.GetString(2)),
        AgentName          = r.IsDBNull(3) ? null : r.GetString(3),
        CommandText        = r.IsDBNull(4) ? null : r.GetString(4),
        RiskLevel          = (RiskLevel)r.GetInt32(5),
        PermissionDecision = (PermissionDecision)r.GetInt32(6),
        Status             = (CommandStatus)r.GetInt32(7),
        RedactedDetails    = r.IsDBNull(8) ? null : r.GetString(8),
        ErrorMessage       = r.IsDBNull(9) ? null : r.GetString(9),
    };
}
