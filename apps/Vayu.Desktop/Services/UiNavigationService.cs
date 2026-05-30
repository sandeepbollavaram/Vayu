namespace Vayu_Desktop.Services;

/// <summary>
/// Bridge between UI-routing intents (<c>ui.show_logs</c>, <c>ui.show_settings</c>)
/// and the shell. <c>ShowLogsAgent</c> / <c>ShowSettingsAgent</c> raise
/// <see cref="NavigationRequested"/>; <c>MainWindow</c> subscribes and
/// marshals the navigation onto the UI thread.
/// </summary>
public sealed class UiNavigationService
{
    /// <summary>Fires when an agent requests a page change. The payload is a stable page key.</summary>
    public event EventHandler<string>? NavigationRequested;

    /// <summary>Asks the shell to navigate to <paramref name="pageKey"/> (e.g. <c>"logs"</c>, <c>"settings"</c>).</summary>
    public void RequestNavigate(string pageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageKey);
        NavigationRequested?.Invoke(this, pageKey);
    }
}
