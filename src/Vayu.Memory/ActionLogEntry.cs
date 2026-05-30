using Vayu.Core;

namespace Vayu.Memory;

/// <summary>
/// One row of the <c>Actions</c> table — the audit log of every command
/// Vayu dispatched, its permission decision, and its outcome.
/// </summary>
/// <remarks>
/// <see cref="RedactedDetails"/> is JSON produced by the caller after
/// running <c>SecretRedactor</c> on whatever context the audit row should
/// preserve. This record carries no secrets; the storage layer trusts the
/// caller to have already redacted. <see cref="Id"/> is <c>0</c> for
/// entries that have not yet been persisted; the storage layer returns
/// a new <see cref="ActionLogEntry"/> with the assigned id after
/// <c>AppendAsync</c>.
/// </remarks>
public sealed record ActionLogEntry
{
    /// <summary>Autoincrement primary key. <c>0</c> on entries that have not been appended yet.</summary>
    public long Id { get; init; }

    /// <summary>When the action ran. Stored in ISO-8601 round-trip format.</summary>
    public required DateTimeOffset TimestampUtc { get; init; }

    /// <summary>Correlates this row with the originating <see cref="CommandRequest"/> and downstream logs.</summary>
    public required Guid CorrelationId { get; init; }

    /// <summary>The agent that handled the action, or <see langword="null"/> when no agent claimed it.</summary>
    public string? AgentName { get; init; }

    /// <summary>The raw user command text. The caller is responsible for redaction before storing.</summary>
    public string? CommandText { get; init; }

    /// <summary>The declared risk of the planned action.</summary>
    public required RiskLevel RiskLevel { get; init; }

    /// <summary>Outcome of the permission engine for this action.</summary>
    public required PermissionDecision PermissionDecision { get; init; }

    /// <summary>Overall status of the command after planning / permission / execution.</summary>
    public required CommandStatus Status { get; init; }

    /// <summary>Already-redacted JSON blob with whatever context the row should preserve. May be null.</summary>
    public string? RedactedDetails { get; init; }

    /// <summary>Already-redacted error message for failed actions. May be null.</summary>
    public string? ErrorMessage { get; init; }
}
