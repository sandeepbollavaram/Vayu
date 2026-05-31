using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Vayu.AI.Gemini;
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
    private readonly GeminiKeySetupService? _geminiKeys;
    private readonly GeminiProvider? _geminiProvider;
    private readonly DesktopCloudConsentService? _cloudConsent;

    public SettingsPage()
    {
        InitializeComponent();

        // M3.3: Gemini key setup. Available when the secret stores are wired.
        _geminiKeys = App.Services?.GetService<GeminiKeySetupService>();
        // M3.4: cloud consent + connector for the consented Test-key round-trip.
        _geminiProvider = App.Services?.GetService<GeminiProvider>();
        _cloudConsent = App.Services?.GetService<DesktopCloudConsentService>();
        if (_geminiKeys is not null)
        {
            Loaded += OnGeminiKeyLoaded;
        }

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

    // ---- M3.3: Gemini key setup ----

    private async void OnGeminiKeyLoaded(object sender, RoutedEventArgs e)
        => await RefreshGeminiKeyStatusAsync().ConfigureAwait(true);

    private async Task RefreshGeminiKeyStatusAsync()
    {
        if (_geminiKeys is null)
        {
            return;
        }
        try
        {
            var status = await _geminiKeys.GetKeyStatusAsync().ConfigureAwait(true);
            var canRemove = await _geminiKeys.CanRemoveKeyAsync().ConfigureAwait(true);

            if (status.IsConfigured)
            {
                GeminiKeyStatusText.Text = $"CONFIGURED · {SourceLabel(status.Source)}";
                ApplyChipStyle(GeminiKeyStatusBadge, "VayuChipTeal");
            }
            else
            {
                GeminiKeyStatusText.Text = "NOT CONFIGURED";
                ApplyChipStyle(GeminiKeyStatusBadge, "VayuChipAmber");
            }

            RemoveGeminiKeyButton.IsEnabled = canRemove;
            // M3.4: Test is enabled only when a key is configured AND the consent
            // path is available. Tapping it asks for explicit per-call consent.
            TestGeminiKeyButton.IsEnabled = status.IsConfigured
                && _geminiProvider is not null
                && _cloudConsent is not null;
        }
        catch (Exception ex)
        {
            GeminiKeyStatusText.Text = $"CHECK FAILED · {ex.GetType().Name.ToUpperInvariant()}";
            ApplyChipStyle(GeminiKeyStatusBadge, "VayuChipRed");
        }
    }

    private async void OnTestGeminiKeyClick(object sender, RoutedEventArgs e)
    {
        if (_geminiProvider is null || _cloudConsent is null)
        {
            return;
        }

        // Ask for explicit, per-call consent before any cloud request.
        _cloudConsent.XamlRoot = this.XamlRoot;
        var request = new Vayu.AI.Online.CloudConsentRequest(
            ProviderId: "gemini",
            ProviderDisplayName: "Gemini",
            Purpose: "Test Gemini provider key",
            DataSummary: "A minimal provider health-check prompt; no private files, no command history, no secrets.",
            EstimatedPromptChars: 8,
            RequiresSensitiveContext: false,
            CreatedAtUtc: DateTimeOffset.UtcNow);

        var decision = await _cloudConsent.RequestConsentAsync(request).ConfigureAwait(true);

        switch (decision)
        {
            case Vayu.AI.Online.CloudConsentDecision.Cancel:
                GeminiKeyMessageText.Text = "Test cancelled.";
                return;
            case Vayu.AI.Online.CloudConsentDecision.UseLocalInstead:
                GeminiKeyMessageText.Text = "Skipped cloud test; local AI remains available.";
                return;
        }

        TestGeminiKeyButton.IsEnabled = false;
        GeminiKeyMessageText.Text = "Testing…";
        try
        {
            var result = await _geminiProvider
                .TestKeyAsync(Vayu.AI.Online.CloudConsentDecision.AllowOnce)
                .ConfigureAwait(true);
            GeminiKeyMessageText.Text = result.Message;
        }
        finally
        {
            TestGeminiKeyButton.IsEnabled = true;
        }
    }

    private async void OnSaveGeminiKeyClick(object sender, RoutedEventArgs e)
    {
        if (_geminiKeys is null)
        {
            return;
        }

        var key = GeminiKeyInput.Password;
        if (string.IsNullOrWhiteSpace(key))
        {
            GeminiKeyMessageText.Text = "Enter a key first.";
            return;
        }

        SaveGeminiKeyButton.IsEnabled = false;
        try
        {
            await _geminiKeys.SaveKeyAsync(key).ConfigureAwait(true);
            // Clear the secret from the UI immediately; it is never shown again.
            GeminiKeyInput.Password = string.Empty;
            GeminiKeyMessageText.Text = "Saved securely. Your key is never shown after saving.";
            await RefreshGeminiKeyStatusAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // The exception message is redaction-safe (no key value).
            GeminiKeyInput.Password = string.Empty;
            GeminiKeyMessageText.Text = $"Could not save the key: {ex.Message}";
        }
        finally
        {
            SaveGeminiKeyButton.IsEnabled = true;
        }
    }

    private async void OnRemoveGeminiKeyClick(object sender, RoutedEventArgs e)
    {
        if (_geminiKeys is null)
        {
            return;
        }
        RemoveGeminiKeyButton.IsEnabled = false;
        try
        {
            await _geminiKeys.RemoveKeyAsync().ConfigureAwait(true);
            GeminiKeyMessageText.Text = "Key removed.";
            await RefreshGeminiKeyStatusAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            GeminiKeyMessageText.Text = $"Could not remove the key: {ex.Message}";
            RemoveGeminiKeyButton.IsEnabled = true;
        }
    }

    private static string SourceLabel(Vayu.AI.Online.OnlineProviderKeySource source) => source switch
    {
        Vayu.AI.Online.OnlineProviderKeySource.WindowsCredentialManager => "WINDOWS CREDENTIAL MANAGER",
        Vayu.AI.Online.OnlineProviderKeySource.EnvironmentVariable => "ENVIRONMENT VARIABLE",
        Vayu.AI.Online.OnlineProviderKeySource.DpapiEncryptedConfig => "DPAPI CONFIG",
        _ => "NOT CONFIGURED",
    };

    private static void ApplyChipStyle(Border badge, string styleKey)
    {
        if (Application.Current.Resources[styleKey] is Style style)
        {
            badge.Style = style;
        }
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
