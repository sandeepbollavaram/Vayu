using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Vayu.Automation.Windows;

namespace Vayu_Desktop.Services;

/// <summary>
/// The real WinUI <see cref="IAutomationConfirmationService"/>: shows a
/// <see cref="ContentDialog"/> describing exactly what Vayu will do — target
/// app/window, action, the <b>exact text preview</b>, risk, and a safety warning
/// — with <b>Approve once</b> / <b>Cancel</b>. Dismissal counts as Cancel; one
/// approval applies only to that single action plan and is never persisted.
/// </summary>
public sealed class WinUiAutomationConfirmationService : IAutomationConfirmationService
{
    /// <inheritdoc />
    public async Task<AutomationConfirmationDecision> RequestAsync(
        AutomationConfirmationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var root = App.MainWindow?.Content?.XamlRoot;
        if (root is null)
        {
            // No UI to confirm against → fail closed (type nothing).
            return AutomationConfirmationDecision.Cancel;
        }

        var tcs = new TaskCompletionSource<AutomationConfirmationDecision>();
        var dispatcher = (App.MainWindow!.Content as FrameworkElement)!.DispatcherQueue;

        var enqueued = dispatcher.TryEnqueue(async () =>
        {
            try
            {
                var body = new StackPanel { Spacing = 8 };
                body.Children.Add(Line("Action", "Type text"));
                body.Children.Add(Line("Target app", request.Target.AppName ?? "(unknown)"));
                body.Children.Add(Line("Window", request.Target.WindowTitle ?? "(unknown)"));
                body.Children.Add(Line("Risk", request.RiskLevel.ToString()));
                body.Children.Add(Line("Text preview", request.TextPreview ?? string.Empty));
                body.Children.Add(new TextBlock
                {
                    Text = request.SafetyWarning,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0),
                    Opacity = 0.85,
                });

                var dialog = new ContentDialog
                {
                    Title = "Approve desktop automation?",
                    Content = body,
                    PrimaryButtonText = "Approve once",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = root,
                };

                var result = await dialog.ShowAsync();
                tcs.TrySetResult(result == ContentDialogResult.Primary
                    ? AutomationConfirmationDecision.ApproveOnce
                    : AutomationConfirmationDecision.Cancel);
            }
#pragma warning disable CA1031 // Dialog boundary: any failure → fail closed (Cancel).
            catch (Exception)
            {
                tcs.TrySetResult(AutomationConfirmationDecision.Cancel);
            }
#pragma warning restore CA1031
        });

        if (!enqueued)
        {
            return AutomationConfirmationDecision.Cancel;
        }

        using (cancellationToken.Register(() => tcs.TrySetResult(AutomationConfirmationDecision.Cancel)))
        {
            return await tcs.Task.ConfigureAwait(false);
        }
    }

    private static StackPanel Line(string label, string value)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = label + ":", Opacity = 0.7, MinWidth = 96 });
        panel.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap });
        return panel;
    }
}
