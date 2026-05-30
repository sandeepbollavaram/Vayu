namespace Vayu.Memory;

/// <summary>
/// Configuration for Vayu's local-only memory substrate. Mirrors a
/// future <c>memory</c> section in <c>appsettings.sample.json</c>.
/// </summary>
/// <remarks>
/// <see cref="DatabasePath"/> may contain Windows environment variable
/// placeholders like <c>%LOCALAPPDATA%</c>; <see cref="SqliteAuditLogService"/>
/// expands them at construction time and creates the parent directory
/// if it does not exist. <see cref="EnableAuditLog"/> defaults to true —
/// turning it off does not stop redaction or permission logic; it only
/// suppresses persisting <c>Actions</c> rows to disk.
/// </remarks>
public sealed record MemoryOptions
{
    /// <summary>SQLite file path. Environment variables are expanded.</summary>
    public string DatabasePath { get; init; } = @"%LOCALAPPDATA%\Vayu\vayu.db";

    /// <summary>When true, every dispatched action lands in the <c>Actions</c> table.</summary>
    public bool EnableAuditLog { get; init; } = true;

    /// <summary>Default page size for the Logs page.</summary>
    public int MaxRecentItems { get; init; } = 100;
}
