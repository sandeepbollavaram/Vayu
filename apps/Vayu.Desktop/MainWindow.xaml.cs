using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Vayu.Core.Setup;
using Vayu_Desktop.Pages;
using Vayu_Desktop.Services;

namespace Vayu_Desktop;

/// <summary>
/// Shell window. Hosts the <see cref="NavigationView"/> and routes
/// <see cref="UiNavigationService.NavigationRequested"/> events from
/// agents back to the right page.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly UiNavigationService _navigation;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");

        _navigation = App.Services.GetRequiredService<UiNavigationService>();
        _navigation.NavigationRequested += OnNavigationRequested;

        // Default to Home; the first-run check below may redirect to Setup.
        Nav.SelectedItem = Nav.MenuItems[0];
        ContentFrame.Navigate(typeof(HomePage));

        _ = RouteFirstLaunchAsync();
    }

    /// <summary>
    /// On a fresh install, opens the first-run setup experience before the main
    /// Command Center. <see cref="FirstRunExperience"/> decides purely from the
    /// persisted setup state; completing OR skipping the wizard suppresses this
    /// on later launches. This is a strong default redirect, not a hard lock —
    /// Vayu stays usable if the user navigates away.
    /// </summary>
    private async Task RouteFirstLaunchAsync()
    {
        FirstRunSetupState state;
        try
        {
            var setup = App.Services.GetRequiredService<IFirstRunSetupService>();
            state = await setup.GetStateAsync().ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Startup routing must never crash the shell; fall back to Home.
        catch (Exception)
        {
            return;
        }
#pragma warning restore CA1031

        if (FirstRunExperience.ShouldShowFirstRunSetup(state))
        {
            NavigateTo("setup");
        }
    }

    private void OnNavigationRequested(object? sender, string pageKey)
    {
        DispatcherQueue.TryEnqueue(() => NavigateTo(pageKey));
    }

    private void Nav_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private void NavigateTo(string pageKey)
    {
        Type pageType = pageKey switch
        {
            "logs" => typeof(LogsPage),
            "settings" => typeof(SettingsPage),
            "security" => typeof(SecurityPage),
            "setup" => typeof(FirstRunSetupPage),
            _ => typeof(HomePage),
        };

        if (ContentFrame.SourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }

        foreach (var item in Nav.MenuItems)
        {
            if (item is NavigationViewItem ni && (ni.Tag as string) == pageKey)
            {
                Nav.SelectedItem = ni;
                break;
            }
        }
    }
}
