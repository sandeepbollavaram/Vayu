using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Vayu.AI.Gemini;
using Vayu.AI.Local;
using Vayu.AI.Online;
using Vayu.Core.Setup;
using Vayu.Voice;

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

        // Voice Setup: local STT provider status + model-path configuration.
        _sttProvider = App.Services?.GetService<ISpeechToTextProvider>();
        _sttState = App.Services?.GetService<VoiceSttState>();
        _audioCapture = App.Services?.GetService<IAudioCaptureService>();
        _modelDownload = App.Services?.GetService<WhisperModelDownloadService>();
        PopulateModelCatalog();
        PopulateStorageSummary();
        if (_sttProvider is not null || _audioCapture is not null)
        {
            Loaded += OnSttStatusLoaded;
        }

        // M4.4: TTS opt-in toggle (off by default).
        _ttsState = App.Services?.GetService<VoiceTtsState>();
        if (_ttsState is not null)
        {
            TtsToggle.IsOn = _ttsState.Enabled;
        }
    }

    private readonly ISpeechToTextProvider? _sttProvider;
    private readonly VoiceSttState? _sttState;
    private readonly IAudioCaptureService? _audioCapture;
    private readonly VoiceTtsState? _ttsState;
    private readonly WhisperModelDownloadService? _modelDownload;
    private CancellationTokenSource? _downloadCts;
    private CancellationTokenSource? _testSttCts;
    private bool _suppressModeChange;

    private void OnTtsToggled(object sender, RoutedEventArgs e)
    {
        if (_ttsState is null)
        {
            return;
        }
        _ttsState.Enabled = TtsToggle.IsOn;
        VoiceTtsStatusText.Text = _ttsState.Enabled
            ? "Text-to-speech: on. Short neutral phrases only — Vayu never speaks secrets or user content."
            : "Text-to-speech: off.";
    }

    private async void OnSttStatusLoaded(object sender, RoutedEventArgs e)
    {
        if (_sttState?.Options.ModelPath is { Length: > 0 } existingPath)
        {
            SttModelPathBox.Text = existingPath;
        }

        if (_audioCapture is not null)
        {
            try
            {
                var mic = await _audioCapture.GetMicrophoneStatusAsync().ConfigureAwait(true);
                VoiceMicStatusText.Text = $"Microphone: {mic.Message}";
            }
            catch
            {
                VoiceMicStatusText.Text = "Microphone: status unavailable.";
            }
        }

        await RefreshSttStatusAsync().ConfigureAwait(true);
    }

    private async Task RefreshSttStatusAsync()
    {
        // Re-resolve so the status reflects any model path just saved (the STT
        // provider is transient over the live VoiceSttState).
        var provider = App.Services?.GetService<ISpeechToTextProvider>() ?? _sttProvider;
        if (provider is null)
        {
            return;
        }
        try
        {
            var status = await provider.GetStatusAsync().ConfigureAwait(true);
            VoiceSttStatusText.Text = $"Local STT ({status.ProviderName}): {status.Message}";
        }
        catch
        {
            VoiceSttStatusText.Text = "Local STT: status unavailable.";
        }
    }

    private async void OnSaveSttModelClick(object sender, RoutedEventArgs e)
    {
        if (_sttState is null)
        {
            SttModelMessageText.Text = "Local STT is not available in this build.";
            return;
        }

        var path = SttModelPathBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            SttModelMessageText.Text = "Enter the path to a local STT model file.";
            return;
        }

        bool exists;
        try
        {
            exists = File.Exists(path);
        }
        catch
        {
            exists = false;
        }

        if (!exists)
        {
            SttModelMessageText.Text = "That file was not found. Check the path and try again.";
            return;
        }

        _sttState.Configure(path);
        SttModelMessageText.Text = "Model saved. Local STT is enabled.";
        await RefreshSttStatusAsync().ConfigureAwait(true);
    }

    private async void OnDisableSttClick(object sender, RoutedEventArgs e)
    {
        if (_sttState is null)
        {
            return;
        }
        _sttState.Disable();
        SttModelPathBox.Text = string.Empty;
        SttModelMessageText.Text = "Local STT disabled.";
        await RefreshSttStatusAsync().ConfigureAwait(true);
    }

    private void PopulateModelCatalog()
    {
        foreach (var model in WhisperModelCatalog.All)
        {
            ModelCatalogCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{model.DisplayName} · ~{model.ApproxSizeMb} MB — {model.Note}",
                Tag = model.Id,
            });
        }
        ModelCatalogCombo.SelectedIndex = 1; // base.en (recommended)
        DownloadModelButton.IsEnabled = _modelDownload is not null;
    }

    private void PopulateStorageSummary()
    {
        var provider = App.Services?.GetService<IVayuStoragePathProvider>();
        if (provider is null)
        {
            StorageSummaryText.Text = "Storage paths are not available in this build.";
            return;
        }
        var p = provider.ActivePaths;
        StorageSourceText.Text = provider.UsingCustomPaths ? "CUSTOM" : "DEFAULT";
        StorageSummaryText.Text =
            $"Data root: {Path.GetDirectoryName(p.WorkspaceRoot)}\n" +
            $"Workspace: {p.WorkspaceRoot}\n" +
            $"Logs: {p.LogsPath}\n" +
            $"Cache: {p.CachePath}\n" +
            $"Models: {p.ModelsPath}\n" +
            $"Assets: {p.AssetsPath}";
    }

    private async void OnOpenDataFolderClick(object sender, RoutedEventArgs e)
    {
        var provider = App.Services?.GetService<IVayuStoragePathProvider>();
        var root = provider is not null
            ? Path.GetDirectoryName(provider.ActivePaths.WorkspaceRoot)
            : null;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return;
        }
        try
        {
            var folder = await global::Windows.Storage.StorageFolder
                .GetFolderFromPathAsync(root);
            await global::Windows.System.Launcher.LaunchFolderAsync(folder);
        }
#pragma warning disable CA1031 // Opening a folder is best-effort; never crash.
        catch (Exception)
        {
            // Ignore — the folder simply doesn't open.
        }
#pragma warning restore CA1031
    }

    private static string ModelsDirectory()
        => App.Services?.GetService<IVayuStoragePathProvider>()?.ActivePaths.ModelsPath
           ?? Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
               "Vayu", "models");

    private async void OnDownloadModelClick(object sender, RoutedEventArgs e)
    {
        if (_modelDownload is null || _sttState is null)
        {
            DownloadMessageText.Text = "Model download is not available in this build.";
            return;
        }
        if (ModelCatalogCombo.SelectedItem is not ComboBoxItem item || item.Tag is not string id
            || WhisperModelCatalog.TryGet(id) is not { } model)
        {
            DownloadMessageText.Text = "Pick a model to download.";
            return;
        }

        var destination = ModelsDirectory();
        var consent = new ContentDialog
        {
            Title = $"Download {model.DisplayName}?",
            Content = $"Source: {model.DownloadUrl}\nSize: ~{model.ApproxSizeMb} MB\nSaved to: {destination}\n\nThe download runs only on your confirmation.",
            PrimaryButtonText = "Download",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await consent.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _downloadCts = new CancellationTokenSource();
        DownloadModelButton.IsEnabled = false;
        CancelDownloadButton.IsEnabled = true;
        DownloadProgressBar.Visibility = Visibility.Visible;
        DownloadProgressBar.Value = 0;
        DownloadMessageText.Text = "Starting download…";

        var progress = new Progress<ModelDownloadProgress>(p =>
        {
            if (p.Fraction is { } f)
            {
                DownloadProgressBar.IsIndeterminate = false;
                DownloadProgressBar.Value = f * 100;
                DownloadMessageText.Text = $"Downloading… {f * 100:0}%";
            }
            else
            {
                DownloadProgressBar.IsIndeterminate = true;
                DownloadMessageText.Text = $"Downloading… {p.BytesReceived / (1024 * 1024)} MB";
            }
        });

        ModelDownloadResult result;
        try
        {
            result = await _modelDownload
                .DownloadAsync(model, destination, progress, _downloadCts.Token)
                .ConfigureAwait(true);
        }
        finally
        {
            _downloadCts?.Dispose();
            _downloadCts = null;
            DownloadModelButton.IsEnabled = true;
            CancelDownloadButton.IsEnabled = false;
            DownloadProgressBar.Visibility = Visibility.Collapsed;
        }

        DownloadMessageText.Text = result.Message;
        if (result.Status == ModelDownloadStatus.Completed && result.FilePath is not null)
        {
            _sttState.Configure(result.FilePath);
            SttModelPathBox.Text = result.FilePath;
            await RefreshSttStatusAsync().ConfigureAwait(true);
        }
    }

    private void OnCancelDownloadClick(object sender, RoutedEventArgs e)
        => _downloadCts?.Cancel();

    private async void OnTestSttClick(object sender, RoutedEventArgs e)
    {
        if (_audioCapture is null)
        {
            TestSttResultText.Text = "Microphone capture is not available in this build.";
            return;
        }

        _testSttCts = new CancellationTokenSource();
        TestSttButton.IsEnabled = false;
        StopTestSttButton.IsEnabled = true;
        TestSttResultText.Text = "Listening… speak, then press Stop test.";

        AudioCaptureResult capture;
        try
        {
            capture = await _audioCapture.StartCaptureAsync(_testSttCts.Token).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // UI boundary: surface capture failure safely.
        catch (Exception)
        {
            capture = AudioCaptureResult.Failed("Microphone capture failed.");
        }
#pragma warning restore CA1031
        finally
        {
            _testSttCts?.Dispose();
            _testSttCts = null;
            TestSttButton.IsEnabled = true;
            StopTestSttButton.IsEnabled = false;
        }

        if (!capture.HasAudio)
        {
            TestSttResultText.Text = capture.Status == AudioCaptureStatus.Failed
                ? capture.ErrorMessage ?? "Microphone capture failed."
                : "No audio captured.";
            return;
        }

        // Transcribe only — a test never dispatches a command.
        var provider = App.Services?.GetService<ISpeechToTextProvider>() ?? _sttProvider;
        if (provider is null)
        {
            TestSttResultText.Text = "Local STT provider is not available.";
            return;
        }
        try
        {
            var result = await provider.TranscribeAsync(capture.Pcm16).ConfigureAwait(true);
            TestSttResultText.Text = result.Success
                ? $"Transcript: {result.Transcript}"
                : result.ErrorMessage ?? "Transcription failed.";
        }
#pragma warning disable CA1031 // UI boundary: surface transcription failure safely.
        catch (Exception)
        {
            TestSttResultText.Text = "Transcription failed.";
        }
#pragma warning restore CA1031
    }

    private void OnStopTestSttClick(object sender, RoutedEventArgs e)
        => _testSttCts?.Cancel();

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
    {
        // M3.6: show the registry immediately (Gemini may not be configured yet).
        RefreshProviderRegistry(geminiConfigured: false);
        await RefreshGeminiKeyStatusAsync().ConfigureAwait(true);
    }

    private void RefreshProviderRegistry(bool geminiConfigured)
        => ProviderRegistryItems.ItemsSource = ProviderRegistryViewModel.BuildCards(geminiConfigured);

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
            // M3.6: reflect Gemini configured state in the provider registry grid.
            RefreshProviderRegistry(status.IsConfigured);
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
