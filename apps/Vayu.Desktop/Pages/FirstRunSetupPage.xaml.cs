using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Vayu.AI.Local;
using Vayu.Core.Setup;

using Vayu_Desktop.Services;

namespace Vayu_Desktop.Pages;

/// <summary>
/// First Run Setup Wizard page (M2.5). Walks the user through Welcome →
/// Mode → Ollama Status → Models → Verification using read-only detection.
/// Never installs Ollama; never pulls a model.
/// </summary>
public sealed partial class FirstRunSetupPage : Page
{
    private readonly FirstRunSetupViewModel? _vm;
    private CancellationTokenSource? _activePullCts;

    public FirstRunSetupPage()
    {
        InitializeComponent();

        var runtime = App.Services?.GetService<IOllamaRuntimeService>();
        var setupService = App.Services?.GetService<IFirstRunSetupService>();
        var pullService = App.Services?.GetService<IOllamaModelPullService>();
        if (runtime is null || setupService is null || pullService is null)
        {
            return;
        }

        _vm = new FirstRunSetupViewModel(setupService, new SettingsLocalAiViewModel(runtime), pullService);
        ModelsItems.ItemsSource = _vm.Detection.Models;

        _vm.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(SyncFromViewModel);
        _vm.Detection.PropertyChanged += (_, _) => DispatcherQueue.TryEnqueue(SyncDetection);

        SyncFromViewModel();
        SyncDetection();
    }

    private void SyncFromViewModel()
    {
        if (_vm is null)
        {
            return;
        }

        StepIndicatorText.Text = _vm.CurrentStep switch
        {
            FirstRunSetupStep.Welcome => "Welcome · storage defaults to local app data",
            FirstRunSetupStep.Mode    => "AI Mode · Local, Online, or Hybrid",
            FirstRunSetupStep.Ollama  => "Local AI · detect your offline runtime",
            FirstRunSetupStep.Models  => "Local AI · model download (with permission)",
            FirstRunSetupStep.Verify  => "Trust & Finish · readiness summary",
            _ => string.Empty,
        };

        WelcomePanel.Visibility = _vm.IsWelcome ? Visibility.Visible : Visibility.Collapsed;
        ModePanel.Visibility    = _vm.IsModeStep ? Visibility.Visible : Visibility.Collapsed;
        OllamaPanel.Visibility  = _vm.IsOllamaStep ? Visibility.Visible : Visibility.Collapsed;
        ModelsPanel.Visibility  = _vm.IsModelStep ? Visibility.Visible : Visibility.Collapsed;
        VerifyPanel.Visibility  = _vm.IsVerifyStep ? Visibility.Visible : Visibility.Collapsed;

        BackButton.IsEnabled = _vm.CanGoBack;
        NextButton.Content = _vm.IsFinalStep ? "Finish" : "Continue";
        SkipButton.Visibility = _vm.IsCompleted ? Visibility.Collapsed : Visibility.Visible;
        CompletedStatusText.Text = _vm.IsCompleted ? "Setup recorded. You can revisit any time." : string.Empty;

        VerifyHeadlineText.Text = _vm.VerificationHeadline;
        VerifyGuidanceText.Text = _vm.VerificationGuidance;
    }

    private void SyncDetection()
    {
        if (_vm is null)
        {
            return;
        }
        OllamaEndpointText.Text = _vm.Detection.EndpointDisplay;
        OllamaExecutableText.Text = _vm.Detection.ExecutableStatusText;
        OllamaServerText.Text = _vm.Detection.EndpointStatusText;
        OllamaMessageText.Text = _vm.Detection.DetectionMessage;
        ModelsSummaryText.Text = _vm.Detection.CuratedSummaryText;
        VerifyHeadlineText.Text = _vm.VerificationHeadline;
        VerifyGuidanceText.Text = _vm.VerificationGuidance;
    }

    private async void OnNextClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }
        NextButton.IsEnabled = false;
        try
        {
            if (_vm.IsFinalStep)
            {
                await _vm.CompleteAsync().ConfigureAwait(true);
            }
            else
            {
                await _vm.GoNextAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            NextButton.IsEnabled = true;
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => _vm?.GoBack();

    private async void OnSkipClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }
        SkipButton.IsEnabled = false;
        try
        {
            await _vm.SkipAsync().ConfigureAwait(true);
        }
        finally
        {
            SkipButton.IsEnabled = true;
        }
    }

    private async void OnOllamaRefreshClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null)
        {
            return;
        }
        OllamaRefreshButton.IsEnabled = false;
        try
        {
            await _vm.RefreshAsync().ConfigureAwait(true);
        }
        finally
        {
            OllamaRefreshButton.IsEnabled = true;
        }
    }

    private void OnModeOfflineChecked(object sender, RoutedEventArgs e)
        => _vm?.SetMode(FirstRunSetupMode.OfflineOnly);

    private async void OnDownloadRowClick(object sender, RoutedEventArgs e)
    {
        if (_vm is null || sender is not FrameworkElement fe || fe.Tag is not string tag)
        {
            return;
        }
        var row = _vm.Detection.Models.FirstOrDefault(m => m.ModelTag == tag);
        if (row is null || !row.CanDownload)
        {
            return;
        }
        await StartPullWithConsentAsync(row).ConfigureAwait(false);
    }

    private async Task StartPullWithConsentAsync(LocalModelRow row)
    {
        if (_vm is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = $"Download {row.ModelTag} with Ollama?",
            Content = BuildConsentBody(row),
            PrimaryButtonText = "Download",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };

        var choice = await dialog.ShowAsync();
        if (choice != ContentDialogResult.Primary)
        {
            return;
        }

        // Only one pull at a time; cancel any straggler first.
        _activePullCts?.Cancel();
        _activePullCts?.Dispose();
        _activePullCts = new CancellationTokenSource();

        try
        {
            await _vm.PullAsync(row, _activePullCts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Row already marked Cancelled by the view model.
        }
        finally
        {
            _activePullCts?.Dispose();
            _activePullCts = null;
        }
    }

    private static string BuildConsentBody(LocalModelRow row)
    {
        var sizeHint = string.IsNullOrEmpty(row.DisplaySize) ? "Size is not known until Ollama responds." : $"Estimated size: {row.DisplaySize}.";
        return string.Join(System.Environment.NewLine, new[]
        {
            $"Vayu will ask your local Ollama to pull '{row.ModelTag}'.",
            "This is a local download via http://localhost:11434/api/pull.",
            "Vayu will NOT send your data to any cloud provider.",
            "No installer runs. No system files are changed outside Ollama's model store.",
            sizeHint,
            "The download may take time and disk space. You can cancel at any time.",
        });
    }

    private void OnCancelDownloadClick(object sender, RoutedEventArgs e)
    {
        // Per-row Cancel button (visible only while that row is downloading).
        _activePullCts?.Cancel();
        if (sender is Control c)
        {
            c.IsEnabled = false;
        }
    }
}
