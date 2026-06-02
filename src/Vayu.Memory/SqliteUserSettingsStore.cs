using System.Globalization;

using Microsoft.Data.Sqlite;

namespace Vayu.Memory;

/// <summary>
/// SQLite-backed <see cref="IUserSettingsStore"/>. Persists small key/value
/// settings in a <c>UserSettings</c> table in the same local <c>vayu.db</c>
/// the audit log uses. Schema is bootstrapped idempotently on first use.
/// </summary>
/// <remarks>
/// This store is for non-secret configuration only (setup state, chosen paths).
/// API keys are never written here — they belong in the secure secret store.
/// </remarks>
public sealed class SqliteUserSettingsStore : IUserSettingsStore
{
    private const string CreateSchemaSql = """
        CREATE TABLE IF NOT EXISTS UserSettings (
            Key         TEXT PRIMARY KEY,
            Value       TEXT NOT NULL,
            UpdatedUtc  TEXT NOT NULL
        );
        """;

    private readonly string _connectionString;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);
    private bool _schemaReady;

    public SqliteUserSettingsStore(MemoryOptions options)
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
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await using var conn = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM UserSettings WHERE Key = $key LIMIT 1;";
        cmd.Parameters.AddWithValue("$key", key);

        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        await using var conn = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO UserSettings (Key, Value, UpdatedUtc)
            VALUES ($key, $value, $ts)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value, UpdatedUtc = excluded.UpdatedUtc;
            """;
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.Parameters.AddWithValue("$ts", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await using var conn = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM UserSettings WHERE Key = $key;";
        cmd.Parameters.AddWithValue("$key", key);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
}
