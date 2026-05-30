using Vayu.Core;

namespace Vayu.Permissions;

/// <summary>
/// Test-only <see cref="IConfirmationPrompt"/> that returns a preset
/// decision and records the request it was asked about. Useful for
/// driving <see cref="DefaultPermissionService"/> tests deterministically.
/// </summary>
public sealed class FakeConfirmationService : IConfirmationPrompt
{
    private readonly PermissionDecision _decision;

    /// <summary>How many times <see cref="AskAsync"/> has been called.</summary>
    public int CallCount { get; private set; }

    /// <summary>The last <see cref="PermissionRequest"/> the fake was asked about.</summary>
    public PermissionRequest? LastRequest { get; private set; }

    /// <summary>
    /// Builds the fake with a preset outcome. Throws if asked to return
    /// <see cref="PermissionDecision.RequiresConfirmation"/> — that is not a
    /// valid prompt outcome.
    /// </summary>
    public FakeConfirmationService(PermissionDecision decision = PermissionDecision.Allowed)
    {
        if (decision == PermissionDecision.RequiresConfirmation)
        {
            throw new ArgumentException(
                "Prompt outcomes must be Allowed, Denied, or Cancelled — not RequiresConfirmation.",
                nameof(decision));
        }
        _decision = decision;
    }

    /// <inheritdoc />
    public Task<PermissionDecision> AskAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CallCount++;
        LastRequest = request;
        return Task.FromResult(_decision);
    }
}
