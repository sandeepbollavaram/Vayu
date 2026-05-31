using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Vayu.AI.Online;

namespace Vayu_Desktop.Services;

/// <summary>
/// WinUI implementation of <see cref="ICloudConsentService"/> (M3.4). Shows a
/// <see cref="ContentDialog"/> describing the intended cloud call and returns
/// the user's decision. <b>Dismiss / close = Cancel</b> — the safe default.
/// </summary>
/// <remarks>
/// The dialog shows the provider, purpose, data summary, estimated prompt size,
/// and sensitive-context flag, plus a fixed safety note. It never receives a
/// key or a raw prompt (the <see cref="CloudConsentRequest"/> carries neither),
/// so nothing secret can be rendered. M3.4 does not persist consent — every
/// cloud call asks again.
///
/// A page assigns <see cref="XamlRoot"/> before requesting consent so the dialog
/// can attach to the live window.
/// </remarks>
public sealed class DesktopCloudConsentService : ICloudConsentService
{
    /// <summary>The XamlRoot the dialog attaches to. Set by the active page before use.</summary>
    public XamlRoot? XamlRoot { get; set; }

    /// <inheritdoc />
    public async Task<CloudConsentDecision> RequestConsentAsync(
        CloudConsentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (XamlRoot is null)
        {
            // No window to attach to — fail safe.
            return CloudConsentDecision.Cancel;
        }

        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(Line($"Provider: {request.ProviderDisplayName}"));
        body.Children.Add(Line($"Purpose: {request.Purpose}"));
        body.Children.Add(Line($"Data: {request.DataSummary}"));
        body.Children.Add(Line($"Estimated prompt size: ~{request.EstimatedPromptChars} characters"));
        body.Children.Add(Line($"Includes sensitive context: {(request.RequiresSensitiveContext ? "Yes" : "No")}"));
        body.Children.Add(Line(
            "Vayu will not send secrets or API keys. This request is logged without the prompt body.",
            muted: true));

        var dialog = new ContentDialog
        {
            Title = "Allow cloud AI request?",
            Content = body,
            PrimaryButtonText = "Allow once",
            SecondaryButtonText = "Use local instead",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        return result switch
        {
            ContentDialogResult.Primary => CloudConsentDecision.AllowOnce,
            ContentDialogResult.Secondary => CloudConsentDecision.UseLocalInstead,
            _ => CloudConsentDecision.Cancel, // includes dismiss / Esc / Cancel
        };
    }

    private static TextBlock Line(string text, bool muted = false)
    {
        var tb = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        if (muted && Application.Current.Resources["VayuMutedText"] is Style mutedStyle)
        {
            tb.Style = mutedStyle;
        }
        return tb;
    }
}
