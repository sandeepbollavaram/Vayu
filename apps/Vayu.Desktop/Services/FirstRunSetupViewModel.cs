using System.ComponentModel;
using System.Runtime.CompilerServices;

using Vayu.AI.Local;
using Vayu.Core.Setup;

namespace Vayu_Desktop.Services;

/// <summary>
/// Drives the five-step First Run Setup Wizard (M2.5). UI-shape only:
/// the heavy lifting (Ollama detection, model listing) is delegated to
/// <see cref="SettingsLocalAiViewModel"/> so the wizard and the Settings
/// card stay in sync.
/// </summary>
/// <remarks>
/// The wizard is read-only in M2.5: it never installs Ollama and never
/// pulls a model. Those affordances arrive in M2.6 behind explicit
/// consent. The view model holds Step state, the chosen
/// <see cref="FirstRunSetupMode"/>, and a derived final-verification
/// summary; persistence is delegated to <see cref="IFirstRunSetupService"/>
/// which is in-memory at M2.5 and SQLite-backed in M2.8.
/// </remarks>
public sealed class FirstRunSetupViewModel : INotifyPropertyChanged
{
    private readonly IFirstRunSetupService _setupService;
    private readonly IOllamaModelPullService _pullService;
    private FirstRunSetupStep _currentStep = FirstRunSetupStep.Welcome;
    private FirstRunSetupMode _mode = FirstRunSetupMode.OfflineOnly;
    private bool _isCompleted;

    public FirstRunSetupViewModel(
        IFirstRunSetupService setupService,
        SettingsLocalAiViewModel detection,
        IOllamaModelPullService pullService)
    {
        ArgumentNullException.ThrowIfNull(setupService);
        ArgumentNullException.ThrowIfNull(detection);
        ArgumentNullException.ThrowIfNull(pullService);
        _setupService = setupService;
        _pullService = pullService;
        Detection = detection;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Shared detection view model — same instance the Settings card uses.</summary>
    public SettingsLocalAiViewModel Detection { get; }

    /// <summary>Wizard step currently visible. Use <see cref="GoNextAsync"/> / <see cref="GoBack"/>.</summary>
    public FirstRunSetupStep CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (_currentStep == value)
            {
                return;
            }
            _currentStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsWelcome));
            OnPropertyChanged(nameof(IsModeStep));
            OnPropertyChanged(nameof(IsOllamaStep));
            OnPropertyChanged(nameof(IsModelStep));
            OnPropertyChanged(nameof(IsVerifyStep));
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(IsFinalStep));
        }
    }

    /// <summary>True once <see cref="CompleteAsync"/> has succeeded at least once.</summary>
    public bool IsCompleted
    {
        get => _isCompleted;
        private set => SetField(ref _isCompleted, value);
    }

    /// <summary>The mode the user chose. Only <see cref="FirstRunSetupMode.OfflineOnly"/> is active in M2.5.</summary>
    public FirstRunSetupMode Mode
    {
        get => _mode;
        private set => SetField(ref _mode, value);
    }

    public bool IsWelcome => CurrentStep == FirstRunSetupStep.Welcome;
    public bool IsModeStep => CurrentStep == FirstRunSetupStep.Mode;
    public bool IsOllamaStep => CurrentStep == FirstRunSetupStep.Ollama;
    public bool IsModelStep => CurrentStep == FirstRunSetupStep.Models;
    public bool IsVerifyStep => CurrentStep == FirstRunSetupStep.Verify;
    public bool IsFinalStep => CurrentStep == FirstRunSetupStep.Verify;
    public bool CanGoBack => CurrentStep != FirstRunSetupStep.Welcome;
    public bool CanGoNext => CurrentStep != FirstRunSetupStep.Verify;

    /// <summary>Human-readable verification summary line, derived from current detection state.</summary>
    public string VerificationHeadline
    {
        get
        {
            if (Detection.EndpointReachable && Detection.InstalledCuratedCount > 0)
            {
                return "Vayu is ready for offline AI.";
            }
            if (Detection.EndpointReachable)
            {
                return "Vayu is partially ready: Ollama is running, but no curated model is installed.";
            }
            return "Vayu is not ready yet: Ollama was not detected.";
        }
    }

    /// <summary>Bullet-style follow-up guidance shown on the verification step.</summary>
    public string VerificationGuidance
    {
        get
        {
            if (Detection.EndpointReachable && Detection.InstalledCuratedCount > 0)
            {
                return "You're set. The rule-based parser (M1) still handles known commands; the local AI planner lands in M2.7.";
            }
            if (Detection.EndpointReachable)
            {
                return "Model download is deferred to M2.6 and will require explicit permission. Until then, you can install models manually with 'ollama pull gemma3:4b'.";
            }
            return "Vayu does not install Ollama automatically in M2.5. Visit https://ollama.com/download/windows or wait for the M2.6 guided flow with explicit consent.";
        }
    }

    /// <summary>Advance to the next step. Triggers a probe when entering the Ollama step.</summary>
    public async Task GoNextAsync()
    {
        switch (CurrentStep)
        {
            case FirstRunSetupStep.Welcome:
                CurrentStep = FirstRunSetupStep.Mode;
                await _setupService.StartAsync(Mode).ConfigureAwait(true);
                break;
            case FirstRunSetupStep.Mode:
                CurrentStep = FirstRunSetupStep.Ollama;
                await Detection.RefreshAsync().ConfigureAwait(true);
                OnPropertyChanged(nameof(VerificationHeadline));
                OnPropertyChanged(nameof(VerificationGuidance));
                break;
            case FirstRunSetupStep.Ollama:
                CurrentStep = FirstRunSetupStep.Models;
                break;
            case FirstRunSetupStep.Models:
                CurrentStep = FirstRunSetupStep.Verify;
                OnPropertyChanged(nameof(VerificationHeadline));
                OnPropertyChanged(nameof(VerificationGuidance));
                break;
        }
    }

    /// <summary>Step back. Welcome cannot go back.</summary>
    public void GoBack()
    {
        if (CurrentStep == FirstRunSetupStep.Welcome)
        {
            return;
        }
        CurrentStep = CurrentStep - 1;
    }

    /// <summary>Pick the wizard mode. M2.5 only enables Offline-only; the rest are previewed.</summary>
    public void SetMode(FirstRunSetupMode mode) => Mode = mode;

    /// <summary>
    /// Pull a curated model via Ollama, streaming progress into <paramref name="row"/>.
    /// Caller must have shown an explicit consent dialog before invoking this method.
    /// </summary>
    public async Task<OllamaModelPullProgress> PullAsync(
        LocalModelRow row,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        row.IsDownloading = true;
        row.CanDownload = false;
        row.DownloadStatus = "Starting…";

        var progress = new Progress<OllamaModelPullProgress>(update =>
        {
            row.DownloadStatus = update.DisplayText;
        });

        OllamaModelPullProgress final;
        try
        {
            final = await _pullService
                .PullModelAsync(new OllamaModelPullRequest(row.ModelTag), progress, cancellationToken)
                .ConfigureAwait(true);
        }
        finally
        {
            row.IsDownloading = false;
        }

        // Confirm completion via a fresh detection probe — we never flip
        // IsInstalled on the basis of the pull stream alone.
        await Detection.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
        OnPropertyChanged(nameof(VerificationHeadline));
        OnPropertyChanged(nameof(VerificationGuidance));

        if (!row.IsInstalled)
        {
            // Detection still says missing — keep the row visible as a Download target
            // unless Ollama is unreachable.
            row.CanDownload = Detection.EndpointReachable;
            if (final.IsCancelled)
            {
                row.DownloadStatus = "Cancelled. You can retry.";
            }
            else if (final.ErrorMessage is not null)
            {
                row.DownloadStatus = $"Download failed: {final.ErrorMessage}";
            }
        }

        return final;
    }

    /// <summary>Re-run Ollama detection. Surface for the Ollama step's Refresh button.</summary>
    public Task RefreshAsync() => Detection.RefreshAsync();

    /// <summary>Skip the remaining steps. Records "completed" so the wizard does not auto-reopen.</summary>
    public async Task SkipAsync()
    {
        await _setupService.CompleteAsync().ConfigureAwait(true);
        IsCompleted = true;
    }

    /// <summary>Mark the wizard as completed after the user advanced through every step.</summary>
    public async Task CompleteAsync()
    {
        await _setupService.CompleteAsync().ConfigureAwait(true);
        IsCompleted = true;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>Wizard step identifier. Ordered so <c>+1</c> / <c>-1</c> walks the wizard.</summary>
public enum FirstRunSetupStep
{
    Welcome = 0,
    Mode = 1,
    Ollama = 2,
    Models = 3,
    Verify = 4,
}
