using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Vayu.AI.Local;

using Vayu_Desktop.Services;

namespace Vayu_Desktop.Pages;

/// <summary>
/// Settings page. M1 cards stay read-only. M2.3 adds the Offline AI card
/// powered by <see cref="SettingsLocalAiViewModel"/> — read-only detection
/// of the local Ollama runtime, with a Refresh button that re-runs probes.
/// No install, no model pull, no cloud calls.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly SettingsLocalAiViewModel? _localAi;
    private readonly LocalAiPlannerState? _plannerState;

    public SettingsPage()
    {
        InitializeComponent();

        // Resolve through DI when available. App.Services is set in OnLaunched;
        // unit-tests can construct the page in isolation without DI.
        var runtime = App.Services?.GetService<IOllamaRuntimeService>();
        if (runtime is not null)
        {
            _localAi = new SettingsLocalAiViewModel(runtime);
            ModelsItems.ItemsSource = _localAi.Models;
            EndpointDisplayText.Text = _localAi.EndpointDisplay;
            ExecutableStatusText.Text = _localAi.ExecutableStatusText;
            EndpointStatusText.Text = _localAi.EndpointStatusText;
            DetectionMessageText.Text = _localAi.DetectionMessage;
            CuratedSummaryText.Text = _localAi.CuratedSummaryText;
            Loaded += OnPageLoaded;
        }

        // M2.7/M2.8: reflect and control the offline AI planner opt-in.
        // Start disabled until the first detection refresh proves readiness.
        _plannerState = App.Services?.GetService<LocalAiPlannerState>();
        if (_plannerState is not null)
        {
            OfflineAiToggle.IsEnabled = false;
            OfflineAiToggle.IsOn = _plannerState.OfflinePlanningEnabled;
            UpdateAiModeText();
        }
    }

    private void OnOfflineAiToggled(object sender, RoutedEventArgs e)
    {
        if (_plannerState is null)
        {
            return;
        }
        // Never allow ON when the runtime is not ready, even if the control
        // somehow reports IsOn (e.g. programmatic flips). Readiness wins.
        var ready = _localAi?.PlannerReady ?? false;
        _plannerState.OfflinePlanningEnabled = OfflineAiToggle.IsOn && ready;
        UpdateAiModeText();
    }

    private void UpdateAiModeText()
    {
        AiModeProviderText.Text = (_plannerState?.OfflinePlanningEnabled ?? false)
            ? $"Provider: Ollama local AI ({_localAi?.ActiveModelTag ?? "local model"}, rule-based fallback)"
            : "Provider: rule-based parser";
    }

    /// <summary>
    /// M2.8: gate the planner toggle on runtime readiness. Disables the toggle
    /// (with a reason) when Ollama is unreachable or no curated model is
    /// installed, and force-disables an already-enabled planner that has lost
    /// its runtime so Vayu cannot keep trying a dead model.
    /// </summary>
    private void SyncPlannerGate()
    {
        if (_localAi is null || _plannerState is null)
        {
            return;
        }

        var ready = _localAi.PlannerReady;
        // Keep the planner's active model in sync with what's installed.
        _plannerState.ActiveModelTag = _localAi.ActiveModelTag;

        OfflineAiToggle.IsEnabled = ready;
        OfflineAiToggleHint.Text = _localAi.PlannerReadinessText;

        if (!ready && _plannerState.OfflinePlanningEnabled)
        {
            // Runtime went away while enabled — fail safe to OFF.
            _plannerState.OfflinePlanningEnabled = false;
            OfflineAiToggle.IsOn = false;
        }
        else
        {
            OfflineAiToggle.IsOn = _plannerState.OfflinePlanningEnabled;
        }

        UpdateAiModeText();
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        // Probe once on first visit so the user sees real state, not "Not yet probed".
        await RunRefreshAsync().ConfigureAwait(false);
    }

    private async void OnRefreshOfflineAiClick(object sender, RoutedEventArgs e)
    {
        await RunRefreshAsync().ConfigureAwait(false);
    }

    private void OnOpenFirstRunSetupClick(object sender, RoutedEventArgs e)
    {
        var nav = App.Services?.GetService<UiNavigationService>();
        nav?.RequestNavigate("setup");
    }

    private async Task RunRefreshAsync()
    {
        if (_localAi is null)
        {
            return;
        }

        RefreshOfflineAiButton.IsEnabled = false;
        try
        {
            await _localAi.RefreshAsync().ConfigureAwait(true);
            EndpointDisplayText.Text = _localAi.EndpointDisplay;
            ExecutableStatusText.Text = _localAi.ExecutableStatusText;
            EndpointStatusText.Text = _localAi.EndpointStatusText;
            DetectionMessageText.Text = _localAi.DetectionMessage;
            CuratedSummaryText.Text = _localAi.CuratedSummaryText;
            SyncPlannerGate();
        }
        finally
        {
            RefreshOfflineAiButton.IsEnabled = true;
        }
    }
}
