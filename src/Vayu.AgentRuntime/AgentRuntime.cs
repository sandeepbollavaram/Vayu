using Vayu.Core;
using Vayu.Memory;
using Vayu.Permissions;
using Vayu.Security;

namespace Vayu.AgentRuntime;

/// <summary>
/// Vayu's <see cref="IAgentRuntime"/> implementation. Plans the command,
/// resolves an agent, runs the permission engine, and (when allowed)
/// executes the agent — always returning a unified
/// <see cref="CommandResult"/> and always writing an execution audit row.
/// </summary>
/// <remarks>
/// The runtime never invokes agents directly until the permission
/// engine has approved the plan. Two audit rows are written per
/// command: one by <see cref="DefaultPermissionService"/> at the
/// permission step, and one by the runtime at the execution step.
/// Both share the same <see cref="IntentPlan.CorrelationId"/> so the
/// Logs page can group them.
/// </remarks>
public sealed class AgentRuntime : IAgentRuntime
{
    private readonly IIntentPlanner _planner;
    private readonly AgentRegistry _registry;
    private readonly IPermissionService _permissions;
    private readonly IClock _clock;
    private readonly IAuditLogService? _auditLog;
    private readonly AgentRuntimeOptions _options;

    public AgentRuntime(
        IIntentPlanner planner,
        AgentRegistry registry,
        IPermissionService permissions,
        IClock clock,
        IAuditLogService? auditLog = null,
        AgentRuntimeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(planner);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(clock);
        _planner = planner;
        _registry = registry;
        _permissions = permissions;
        _clock = clock;
        _auditLog = auditLog;
        _options = options ?? new AgentRuntimeOptions();
    }

    /// <inheritdoc />
    public async Task<CommandResult> DispatchAsync(CommandRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var plan = await _planner.PlanAsync(request, cancellationToken).ConfigureAwait(false);

        if (string.Equals(plan.Intent, RuleBasedCommandParser.UnknownIntent, StringComparison.Ordinal))
        {
            return CommandResult.NeedsClarification(_options.UnknownCommandMessage, plan);
        }

        var agent = _registry.FindByIntent(plan.Intent);
        if (agent is null)
        {
            return CommandResult.Failed(
                $"No agent is registered for intent '{plan.Intent}'.",
                errorCode: "NO_AGENT_FOR_INTENT",
                plan: plan);
        }

        var permissionResult = await _permissions
            .EvaluateAsync(
                new PermissionRequest
                {
                    Plan = plan,
                    Origin = request,
                    AgentName = agent.Name,
                },
                cancellationToken)
            .ConfigureAwait(false);

        switch (permissionResult.Decision)
        {
            case PermissionDecision.Denied:
            case PermissionDecision.Cancelled:
                return CommandResult.Cancelled(permissionResult.Reason, plan);

            case PermissionDecision.RequiresConfirmation:
                return CommandResult.PermissionRequired(plan, permissionResult.Reason);

            case PermissionDecision.Allowed:
                break;
        }

        if (plan.Risk > agent.MaxRisk)
        {
            var result = CommandResult.Failed(
                $"Plan risk {plan.Risk} exceeds agent '{agent.Name}' max risk {agent.MaxRisk}.",
                errorCode: "AGENT_RISK_EXCEEDED",
                agentName: agent.Name,
                plan: plan);
            await WriteExecutionAuditAsync(request, agent, plan, result, cancellationToken).ConfigureAwait(false);
            return result;
        }

        CommandResult execResult;
        try
        {
            execResult = await agent.ExecuteAsync(plan, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types — runtime is the boundary; agents should not be able to crash the dispatcher.
        catch (Exception ex)
        {
            execResult = CommandResult.Failed(
                SecretRedactor.Redact(ex.Message),
                errorCode: ex.GetType().Name,
                agentName: agent.Name,
                plan: plan);
        }
#pragma warning restore CA1031

        await WriteExecutionAuditAsync(request, agent, plan, execResult, cancellationToken).ConfigureAwait(false);
        return execResult;
    }

    private async Task WriteExecutionAuditAsync(
        CommandRequest request,
        IAgent agent,
        IntentPlan plan,
        CommandResult result,
        CancellationToken cancellationToken)
    {
        if (_auditLog is null || !_options.EnableAuditLog)
        {
            return;
        }

        var entry = new ActionLogEntry
        {
            TimestampUtc = _clock.UtcNow,
            CorrelationId = plan.CorrelationId,
            AgentName = agent.Name,
            CommandText = SecretRedactor.Redact(request.Text),
            RiskLevel = plan.Risk,
            PermissionDecision = PermissionDecision.Allowed,
            Status = result.Status,
            ErrorMessage = result.Message is null ? null : SecretRedactor.Redact(result.Message),
        };

        await _auditLog.AppendAsync(entry, cancellationToken).ConfigureAwait(false);
    }
}
