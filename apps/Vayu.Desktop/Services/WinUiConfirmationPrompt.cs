using Vayu.Core;
using Vayu.Permissions;

namespace Vayu_Desktop.Services;

/// <summary>
/// Placeholder <see cref="IConfirmationPrompt"/> for Milestone 1. M1's only
/// agent (<c>AppLauncherAgent</c>) declares <see cref="RiskLevel.L1"/>, which
/// the policy auto-allows — so this prompt is never invoked during the M1
/// demo. A real WinUI <c>ContentDialog</c>-driven implementation lands in
/// Milestone 5 alongside the L3+ Windows automation features.
/// </summary>
public sealed class WinUiConfirmationPrompt : IConfirmationPrompt
{
    /// <inheritdoc />
    public Task<PermissionDecision> AskAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        // M1: no L3+ flows exist yet. Default deny so we never silently approve
        // an unexpected high-risk plan that slips through. M5 replaces this
        // with a real ContentDialog showing Allow / Edit / Cancel.
        return Task.FromResult(PermissionDecision.Cancelled);
    }
}
