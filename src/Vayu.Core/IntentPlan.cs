using System.Collections.Immutable;

namespace Vayu.Core;

/// <summary>
/// A parsed user intent, ready for the permission engine and the agent
/// runtime. Produced either by the rule-based parser or by an AI planner
/// (Ollama / Gemini).
/// </summary>
/// <remarks>
/// The model never runs an intent directly. It returns a plan; the runtime
/// gates the plan through <c>IPermissionService</c> before any agent sees
/// it. <see cref="Risk"/> is the declared risk of executing this plan; the
/// permission policy uses it to decide whether to ask the user.
/// </remarks>
public sealed record IntentPlan
{
    /// <summary>Dotted intent name, e.g. <c>app.launch</c> or <c>gmail.draft</c>.</summary>
    public required string Intent { get; init; }

    /// <summary>The declared risk level for this plan.</summary>
    public required RiskLevel Risk { get; init; }

    /// <summary>Intent arguments as flat string key/value pairs.</summary>
    public IReadOnlyDictionary<string, string> Args { get; init; } =
        ImmutableDictionary<string, string>.Empty;

    /// <summary>Confidence in this plan, 0..1. Null if the planner did not report one.</summary>
    public double? Confidence { get; init; }

    /// <summary>Who produced the plan, e.g. <c>"rule-based"</c>, <c>"ollama:gemma3:4b"</c>, <c>"gemini"</c>.</summary>
    public string? PlanSource { get; init; }

    /// <summary>Correlates this plan with its originating <see cref="CommandRequest"/>.</summary>
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
}
