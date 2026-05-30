using Vayu.Core;

namespace Vayu.AgentRuntime;

/// <summary>
/// Milestone-1 <see cref="IIntentPlanner"/> implementation that
/// delegates to <see cref="RuleBasedCommandParser"/>. Synchronous under
/// the hood, exposed as <see cref="Task"/> so M2/M3 can swap in
/// async LLM-backed planners without rewriting callers.
/// </summary>
public sealed class IntentPlanner : IIntentPlanner
{
    private readonly RuleBasedCommandParser _parser;

    public IntentPlanner(RuleBasedCommandParser parser)
    {
        ArgumentNullException.ThrowIfNull(parser);
        _parser = parser;
    }

    /// <inheritdoc />
    public Task<IntentPlan> PlanAsync(CommandRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_parser.Parse(request));
    }
}
