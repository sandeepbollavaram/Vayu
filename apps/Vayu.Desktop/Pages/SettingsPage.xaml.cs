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

        // M3.5: AI Mode selector (Rule-based / Offline / Online Gemini / Hybrid).
        // Modes stay disabled until SyncPlannerGate proves their readiness.
        _plannerState = App.Services?.GetService<LocalAiPlannerState>();
        if (_plannerState is not null)
        {
            _plannerState.ActiveOnlineProviderId = "gemini";
            SelectModeRadio(_plannerState.Mode);
            UpdateAiModeText();
        }
    }

    private bool _suppressModeChange;

    private void OnAiModeChecked(object sender, RoutedEventArgs e)
    {
        if (_suppressModeChange || _plannerState is null || sender is not RadioButton rb || rb.Tag is not string tag)
        {
            return;
        }
        _plannerState.Mode = tag switch
        {
            "offline" => PlanningMode.Offline,
            "online" => PlanningMode.Online,
            "hybrid" => PlanningMode.Hybrid,
            _ => PlanningMode.RuleBased,
        };
        UpdateAiModeText();
    }

    private void SelectModeRadio(PlanningMode mode)
    {
        _suppressModeChange = true;
        try
        {
            ModeRuleBasedRadio.IsChecked = mode == PlanningMode.RuleBased;
            ModeOfflineRadio.IsChecked = mode == PlanningMode.Offline;
            ModeOnlineRadio.IsChecked = mode == PlanningMode.Online;
            ModeHybridRadio.IsChecked = mode == PlanningMode.Hybrid;
        }
        finally
        {
            _suppressModeChange = false;
        }
    }

    private void UpdateAiModeText()
    {
        AiModeProviderText.Text = (_plannerState?.Mode ?? PlanningMode.RuleBased) switch
        {
            PlanningMode.Offline => $"Provider: Ollama local AI ({_localAi?.ActiveModelTag ?? "local model"}, rule-based fallback)",
            PlanningMode.Online => "Provider: Gemini online AI (consent each request)",
            PlanningMode.Hybrid => "Provider: Hybrid — local first, Gemini with consent fallback",
            _ => "Provider: rule-based parser",
        };
    }

    /// <summary>
    /// M3.5: gate each AI mode on its readiness. Offline needs Ollama reachable +
    /// a curated model; Online needs a configured Gemini key; Hybrid needs both.
    /// If the selected mode loses its readiness, fall back to Rule-based.
    /// </summary>
    private async void SyncPlannerGate()
    {
        if (_localAi is null || _plannerState is null)
        {
            return;
        }

        var offlineReady = _localAi.PlannerReady;
        _plannerState.ActiveModelTag = _localAi.ActiveModelTag;

        var geminiReady = false;
        if (_geminiKeys is not null)
        {
            try
            {
                geminiReady = await _geminiKeys.IsKeyConfiguredAsync().ConfigureAwait(true);
            }
            catch
            {
                geminiReady = false;
            }
        }

        ModeOfflineRadio.IsEnabled = offlineReady;
        ModeOnlineRadio.IsEnabled = geminiReady && _cloudConsent is not null;
        ModeHybridRadio.IsEnabled = offlineReady && geminiReady && _cloudConsent is not null;

        AiModeHint.Text = offlineReady
            ? _localAi.PlannerReadinessText
            : "Offline needs Ollama + a curated model; Online/Hybrid need a saved Gemini key. Cloud calls ask for consent every time.";

        // If the current mode lost its prerequisites, fall back to Rule-based.
        var mode = _plannerState.Mode;
        var stillValid = mode switch
        {
            PlanningMode.Offline => offlineReady,
            PlanningMode.Online => geminiReady && _cloudConsent is not null,
            PlanningMode.Hybrid => offlineReady && geminiReady && _cloudConsent is not null,
            _ => true,
        };
        if (!stillValid)
        {
            _plannerState.Mode = PlanningMode.RuleBased;
            SelectModeRadio(PlanningMode.RuleBased);
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
            // M3.5: a key change can enable/disable Online/Hybrid modes.
            SyncPlannerGate();
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
