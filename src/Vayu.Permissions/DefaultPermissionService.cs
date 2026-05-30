using Vayu.Core;
using Vayu.Memory;
using Vayu.Security;

namespace Vayu.Permissions;

/// <summary>
/// Default <see cref="IPermissionService"/>. Auto-allows risk levels
/// below the policy's confirmation threshold, asks an
/// <see cref="IConfirmationPrompt"/> for levels at or above it, and
/// outright denies levels at or above the policy's disabled floor.
/// Every decision writes an <see cref="ActionLogEntry"/> when an
/// <see cref="IAuditLogService"/> is configured.
/// </summary>
/// <remarks>
/// Audit row Status mapping (this is the permission step, not execution):
/// <list type="bullet">
/// <item><see cref="PermissionDecision.Allowed"/> → <see cref="CommandStatus.Success"/> (permission granted)</item>
/// <item><see cref="PermissionDecision.Denied"/> or <see cref="PermissionDecision.Cancelled"/> → <see cref="CommandStatus.Cancelled"/></item>
/// <item><see cref="PermissionDecision.RequiresConfirmation"/> → <see cref="CommandStatus.PermissionRequired"/> (defensive; not returned by this implementation)</item>
/// </list>
/// <see cref="CommandRequest.Text"/> is run through
/// <see cref="SecretRedactor"/> before being stored, in case the user
/// typed a credential into the command box.
/// </remarks>
public sealed class DefaultPermissionService : IPermissionService
{
    private readonly PermissionPolicy _policy;
    private readonly IConfirmationPrompt _prompt;
    private readonly IClock _clock;
    private readonly IAuditLogService? _auditLog;

    /// <summary>
    /// Constructs the service. <paramref name="auditLog"/> is optional;
    /// pass <see langword="null"/> in tests or in early bootstrap before
    /// the SQLite store is ready.
    /// </summary>
    public DefaultPermissionService(
        PermissionPolicy policy,
        IConfirmationPrompt prompt,
        IClock clock,
        IAuditLogService? auditLog = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(clock);
        _policy = policy;
        _prompt = prompt;
        _clock = clock;
        _auditLog = auditLog;
    }

    /// <inheritdoc />
    public async Task<PermissionResult> EvaluateAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var risk = request.Plan.Risk;
        PermissionResult result;

        if (risk >= _policy.DisabledAtOrAbove)
        {
            result = new PermissionResult
            {
                Decision = PermissionDecision.Denied,
                Reason = $"Risk level {risk} is disabled by policy.",
                Plan = request.Plan,
            };
        }
        else if (risk >= _policy.AlwaysConfirmAtOrAbove)
        {
            var promptDecision = await _prompt.AskAsync(request, cancellationToken).ConfigureAwait(false);
            result = new PermissionResult
            {
                Decision = promptDecision,
                Reason = $"User {promptDecision.ToString().ToLowerInvariant()} at risk level {risk}.",
                Plan = request.Plan,
            };
        }
        else
        {
            result = new PermissionResult
            {
                Decision = PermissionDecision.Allowed,
                Reason = $"Auto-allowed at risk level {risk} (below confirmation threshold).",
                Plan = request.Plan,
            };
        }

        await WriteAuditAsync(request, result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private async Task WriteAuditAsync(PermissionRequest request, PermissionResult result, CancellationToken cancellationToken)
    {
        if (_auditLog is null)
        {
            return;
        }

        var redactedCommand = SecretRedactor.Redact(request.Origin.Text);
        var status = result.Decision switch
        {
            PermissionDecision.Allowed => CommandStatus.Success,
            PermissionDecision.Denied => CommandStatus.Cancelled,
            PermissionDecision.Cancelled => CommandStatus.Cancelled,
            PermissionDecision.RequiresConfirmation => CommandStatus.PermissionRequired,
            _ => CommandStatus.Failed,
        };
        var errorMessage = result.Decision is PermissionDecision.Denied or PermissionDecision.Cancelled
            ? result.Reason
            : null;

        var entry = new ActionLogEntry
        {
            TimestampUtc = _clock.UtcNow,
            CorrelationId = request.Plan.CorrelationId,
            AgentName = request.AgentName,
            CommandText = redactedCommand,
            RiskLevel = request.Plan.Risk,
            PermissionDecision = result.Decision,
            Status = status,
            ErrorMessage = errorMessage,
        };

        await _auditLog.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
    }
}
