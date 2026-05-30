namespace Vayu.Core;

/// <summary>
/// A named capability registered with the runtime. Each agent declares the
/// highest risk level it is allowed to request, and the set of intents it
/// understands. The runtime refuses to dispatch a plan whose
/// <see cref="IntentPlan.Risk"/> exceeds <see cref="MaxRisk"/>.
/// </summary>
public interface IAgent
{
    /// <summary>Stable agent identifier, e.g. <c>"AppLauncher"</c> or <c>"Gmail"</c>.</summary>
    string Name { get; }

    /// <summary>The highest <see cref="RiskLevel"/> this agent may execute.</summary>
    RiskLevel MaxRisk { get; }

    /// <summary>The dotted intent names this agent handles, e.g. <c>app.launch</c>.</summary>
    IReadOnlyCollection<string> Intents { get; }

    /// <summary>Runs the plan and returns the outcome.</summary>
    Task<CommandResult> ExecuteAsync(IntentPlan plan, CancellationToken cancellationToken = default);
}
