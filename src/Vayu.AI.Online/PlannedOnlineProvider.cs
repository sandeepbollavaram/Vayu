using Vayu.Core;

namespace Vayu.AI.Online;

/// <summary>
/// A safe no-op <see cref="IOnlineAiProvider"/> for a provider whose real
/// connector is planned but not yet implemented (M3.7 shells). It answers the
/// contract so the architecture is ready to flip on a real connector later —
/// but it makes <b>no HTTP call</b>, reads <b>no key</b>, stores nothing, and
/// requires no consent.
/// </summary>
/// <remarks>
/// Gemini remains the only working online connector through the M3 release.
/// Every shell returns a redaction-safe "planned, not implemented" failure
/// from <see cref="PlanAsync"/>, so even if one were wired into the router it
/// could only fall back — never call out.
/// </remarks>
public sealed class PlannedOnlineProvider : IOnlineAiProvider
{
    private readonly string _displayName;

    public PlannedOnlineProvider(string providerId, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ProviderId = providerId;
        _displayName = displayName ?? providerId;
    }

    /// <summary>Builds a shell from a catalog descriptor.</summary>
    public PlannedOnlineProvider(OnlineProviderDescriptor descriptor)
        : this((descriptor ?? throw new ArgumentNullException(nameof(descriptor))).ProviderId, descriptor.DisplayName)
    {
    }

    /// <inheritdoc />
    public string ProviderId { get; }

    /// <summary>Human-facing name for UI; carries no secret.</summary>
    public string DisplayName => _displayName;

    /// <inheritdoc />
    public Task<OnlineProviderKeyStatus> GetKeyStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(OnlineProviderKeyStatus.NotConfigured(
            ProviderId,
            "This provider's connector is planned but not implemented yet."));

    /// <inheritdoc />
    public Task<OnlineAiPlanningResult> PlanAsync(
        CommandRequest request,
        CloudConsentDecision consent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // No HTTP, no key, no consent dependency — just a safe failure.
        return Task.FromResult(OnlineAiPlanningResult.Failed(
            $"The {_displayName} connector is planned but not implemented yet.",
            ProviderId));
    }
}
