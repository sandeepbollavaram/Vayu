using Vayu.Core;

namespace Vayu.Permissions;

/// <summary>
/// Outcome of evaluating a <see cref="PermissionRequest"/>. Carries the
/// <see cref="Decision"/> the engine reached, a short, redaction-safe
/// <see cref="Reason"/>, and the plan the engine acted on.
/// </summary>
public sealed record PermissionResult
{
    /// <summary>What the engine decided.</summary>
    public required PermissionDecision Decision { get; init; }

    /// <summary>Human-readable reason for <see cref="Decision"/>. Already redacted upstream; safe to log.</summary>
    public required string Reason { get; init; }

    /// <summary>The plan the engine evaluated. Returned for the UI to render alongside the decision.</summary>
    public required IntentPlan Plan { get; init; }
}
