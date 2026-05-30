using Vayu.Core;

namespace Vayu.Permissions;

/// <summary>
/// Input to <see cref="IPermissionService.EvaluateAsync"/>. Carries the
/// planned action and the originating command so the engine and any
/// confirmation UI have full context.
/// </summary>
public sealed record PermissionRequest
{
    /// <summary>The plan whose risk level the engine is evaluating.</summary>
    public required IntentPlan Plan { get; init; }

    /// <summary>The user command that produced <see cref="Plan"/>.</summary>
    public required CommandRequest Origin { get; init; }

    /// <summary>The agent that would handle <see cref="Plan"/>, if known.</summary>
    public string? AgentName { get; init; }
}
