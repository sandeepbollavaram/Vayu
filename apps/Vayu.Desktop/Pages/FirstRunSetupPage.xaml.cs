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

    public FirstRunSetupPage()
    {
        InitializeComponent();

        var runtime = App.Services?.GetService<IOllamaRuntimeService>();
        var setupService = App.Services?.GetService<IFirstRunSetupService>();
        if (runtime is null || setupService is null)
        {
            return;
        }

        _vm = new FirstRunSetupViewModel(setupService, new SettingsLocalAiViewModel(runtime));
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
            FirstRunSetupStep.Welcome => "Step 1 of 5 · Welcome",
            FirstRunSetupStep.Mode    => "Step 2 of 5 · Pick an AI mode",
            FirstRunSetupStep.Ollama  => "Step 3 of 5 · Ollama detection",
            FirstRunSetupStep.Models  => "Step 4 of 5 · Model catalog",
            FirstRunSetupStep.Verify  => "Step 5 of 5 · Verification",
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
}
