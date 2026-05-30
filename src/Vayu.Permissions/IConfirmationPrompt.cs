using Vayu.Core;

namespace Vayu.Permissions;

/// <summary>
/// Asks the user about a planned action. The WinUI shell implements this
/// by showing a dialog; tests use <see cref="FakeConfirmationService"/>.
/// </summary>
/// <remarks>
/// Implementations MUST return one of <see cref="PermissionDecision.Allowed"/>,
/// <see cref="PermissionDecision.Denied"/>, or <see cref="PermissionDecision.Cancelled"/>.
/// Returning <see cref="PermissionDecision.RequiresConfirmation"/> here would
/// mean "ask again" — which is not yet supported.
/// </remarks>
public interface IConfirmationPrompt
{
    /// <summary>Asks the user about <paramref name="request"/> and returns their decision.</summary>
    Task<PermissionDecision> AskAsync(PermissionRequest request, CancellationToken cancellationToken = default);
}
