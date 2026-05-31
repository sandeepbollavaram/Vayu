using Vayu.Core;

namespace Vayu.AI.Online;

/// <summary>
/// A cloud AI provider that can turn a command into an <see cref="IntentPlan"/>.
/// M3.1 defines the contract; concrete connectors (Gemini first, M3.2) plug in
/// behind it.
/// </summary>
/// <remarks>
/// Hard rules every implementation inherits:
/// <list type="bullet">
/// <item>Off by default; only reached when the user enabled online AI and selected this provider.</item>
/// <item><see cref="PlanAsync"/> requires a <see cref="CloudConsentDecision.AllowOnce"/> — it must refuse to send anything on any other decision.</item>
/// <item>The provider produces a plan only; it never executes a tool. The plan still flows through AgentRuntime → IPermissionService → agent → audit.</item>
/// <item>Plans are validated against <see cref="OnlineAiSafetyPolicy"/> before being returned.</item>
/// <item>Never log the prompt or the key.</item>
/// </list>
/// </remarks>
public interface IOnlineAiProvider
{
    /// <summary>Stable provider id, e.g. <c>"gemini"</c>.</summary>
    string ProviderId { get; }

    /// <summary>Reports whether a key is configured for this provider — never the key value.</summary>
    Task<OnlineProviderKeyStatus> GetKeyStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Plans <paramref name="request"/> using the cloud model. Must send nothing
    /// unless <paramref name="consent"/> is <see cref="CloudConsentDecision.AllowOnce"/>.
    /// </summary>
    Task<OnlineAiPlanningResult> PlanAsync(
        CommandRequest request,
        CloudConsentDecision consent,
        CancellationToken cancellationToken = default);
}
