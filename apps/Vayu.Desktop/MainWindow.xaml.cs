using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

        // Default to Home.
        Nav.SelectedItem = Nav.MenuItems[0];
        ContentFrame.Navigate(typeof(HomePage));
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
