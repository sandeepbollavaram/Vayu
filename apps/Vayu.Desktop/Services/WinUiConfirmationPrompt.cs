using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Vayu.Core;
using Vayu.Permissions;

namespace Vayu_Desktop.Services;

/// <summary>
/// Real WinUI <see cref="IConfirmationPrompt"/>: the permission engine calls this
/// for L3+ plans. It shows a <see cref="ContentDialog"/> describing the action
/// and risk with <b>Allow</b> / <b>Cancel</b>. This is the <em>first</em> of two
/// safety layers for desktop automation — the second is the detailed
/// <see cref="WinUiAutomationConfirmationService"/> (with the exact text preview)
/// shown by the automation agent before typing.
/// </summary>
/// <remarks>
/// Fail-closed: if there is no UI to prompt against, the dialog cannot be shown,
/// or it is dismissed, the decision is <see cref="PermissionDecision.Cancelled"/>
/// — Vayu never silently approves a high-risk action.
/// </remarks>
public sealed class WinUiConfirmationPrompt : IConfirmationPrompt
{
    /// <inheritdoc />
    public async Task<PermissionDecision> AskAsync(PermissionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var root = App.MainWindow?.Content?.XamlRoot;
        var content = App.MainWindow?.Content as FrameworkElement;
        if (root is null || content is null)
        {
            return PermissionDecision.Cancelled; // fail closed
        }

        var tcs = new TaskCompletionSource<PermissionDecision>();
        var enqueued = content.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = "Allow this action?",
                    Content = $"Vayu wants to run a {request.Plan.Risk} action " +
                              $"({request.Plan.Intent}) via {request.AgentName}. " +
                              "You will confirm the exact details next.",
                    PrimaryButtonText = "Allow",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = root,
                };
                var result = await dialog.ShowAsync();
                tcs.TrySetResult(result == ContentDialogResult.Primary
                    ? PermissionDecision.Allowed
                    : PermissionDecision.Cancelled);
            }
#pragma warning disable CA1031 // Dialog boundary: any failure → fail closed.
            catch (Exception)
            {
                tcs.TrySetResult(PermissionDecision.Cancelled);
            }
#pragma warning restore CA1031
        });

        if (!enqueued)
        {
            return PermissionDecision.Cancelled;
        }

        using (cancellationToken.Register(() => tcs.TrySetResult(PermissionDecision.Cancelled)))
        {
            return await tcs.Task.ConfigureAwait(false);
        }
    }
}
