namespace Vayu.Core;

/// <summary>
/// A lightweight handler that can take an <see cref="IntentPlan"/> and
/// execute it. Used by the agent runtime to dispatch a plan to whatever
/// concrete handler claims it.
/// </summary>
/// <remarks>
/// Distinct from <see cref="IAgent"/>: an agent owns a name, a max risk
/// declaration, and a list of intents it supports — a command handler is
/// just the execution surface. Agents implement <see cref="ICommandHandler"/>
/// in practice.
/// </remarks>
public interface ICommandHandler
{
    /// <summary>Returns true if this handler is willing to execute the plan.</summary>
    bool CanHandle(IntentPlan plan);

    /// <summary>Executes the plan and returns the result.</summary>
    Task<CommandResult> HandleAsync(IntentPlan plan, CancellationToken cancellationToken = default);
}
