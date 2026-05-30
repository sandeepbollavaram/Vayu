namespace Vayu.Memory;

/// <summary>
/// Append-only local audit log. Backed by SQLite in production
/// (<see cref="SqliteAuditLogService"/>); test doubles can implement
/// the interface in-memory.
/// </summary>
/// <remarks>
/// Audit data is local-only. Implementations MUST NOT upload to the
/// cloud, MUST NOT train models on it, and MUST support deletion in a
/// later milestone. See the Kairo memory-design decision for the full
/// rule set.
/// </remarks>
public interface IAuditLogService
{
    /// <summary>
    /// Persists <paramref name="entry"/> and returns it with the storage
    /// layer's assigned <see cref="ActionLogEntry.Id"/>.
    /// </summary>
    Task<ActionLogEntry> AppendAsync(ActionLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns up to <paramref name="limit"/> most recent entries,
    /// newest first. Returns an empty list when no entries exist.
    /// </summary>
    Task<IReadOnlyList<ActionLogEntry>> ListRecentAsync(int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up the first entry with the given correlation id, or
    /// <see langword="null"/> if none exists.
    /// </summary>
    Task<ActionLogEntry?> FindByCorrelationAsync(Guid correlationId, CancellationToken cancellationToken = default);
}
