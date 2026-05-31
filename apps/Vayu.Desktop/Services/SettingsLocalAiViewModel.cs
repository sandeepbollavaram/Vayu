using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Microsoft.UI.Xaml;

using Vayu.AI.Local;

namespace Vayu_Desktop.Services;

/// <summary>
/// Backing view model for the Offline AI card group on the Settings page.
/// Encapsulates "is Ollama detected / reachable" detection plus the
/// curated <see cref="LocalModelCatalog"/> with per-model installed/missing
/// state. Detection is read-only — the view model never installs, pulls,
/// or talks to the cloud. Refresh re-runs probes only.
/// </summary>
/// <remarks>
/// The view model is testable on its own: the constructor takes an
/// <see cref="IOllamaRuntimeService"/> so tests can supply a fake
/// runtime. The Settings page resolves the real implementation via DI.
/// </remarks>
public sealed class SettingsLocalAiViewModel : INotifyPropertyChanged
{
    private readonly IOllamaRuntimeService _runtime;
    private bool _isProbing;
    private bool _executableDetected;
    private bool _endpointReachable;
    private string _detectionMessage = "Not yet probed.";
    private string _endpointDisplay = "http://localhost:11434";
    private int _installedCuratedCount;
    private int _installedUnknownCount;
    private string? _activeModelTag;
    private bool _plannerReady;

    public SettingsLocalAiViewModel(IOllamaRuntimeService runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
        _endpointDisplay = runtime.Endpoint.ToString();

        Models = new ObservableCollection<LocalModelRow>();
        foreach (var descriptor in LocalModelCatalog.ListAll())
        {
            Models.Add(new LocalModelRow(descriptor));
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Curated catalog rows in display order, each carrying its installed/missing badge.</summary>
    public ObservableCollection<LocalModelRow> Models { get; }

    /// <summary>True while a detection round-trip is in flight.</summary>
    public bool IsProbing
    {
        get => _isProbing;
        private set => SetField(ref _isProbing, value);
    }

    /// <summary>True when the Ollama executable was found on <c>PATH</c>.</summary>
    public bool ExecutableDetected
    {
        get => _executableDetected;
        private set => SetField(ref _executableDetected, value);
    }

    /// <summary>True when <c>GET /api/tags</c> succeeded.</summary>
    public bool EndpointReachable
    {
        get => _endpointReachable;
        private set => SetField(ref _endpointReachable, value);
    }

    /// <summary>Endpoint URL as displayed (e.g. <c>http://localhost:11434</c>).</summary>
    public string EndpointDisplay
    {
        get => _endpointDisplay;
        private set => SetField(ref _endpointDisplay, value);
    }

    /// <summary>Short, redaction-safe human message. Always set — never <see langword="null"/>.</summary>
    public string DetectionMessage
    {
        get => _detectionMessage;
        private set => SetField(ref _detectionMessage, value);
    }

    /// <summary>Friendly status pill text ("Reachable" / "Not reachable" / "Probing…").</summary>
    public string EndpointStatusText => IsProbing
        ? "Probing…"
        : EndpointReachable ? "Reachable" : "Not reachable";

    /// <summary>Friendly status pill text for the executable probe.</summary>
    public string ExecutableStatusText => IsProbing
        ? "Probing…"
        : ExecutableDetected ? "Detected" : "Not detected";

    /// <summary>Total curated catalog size (denominator for the summary).</summary>
    public int CuratedCatalogCount => Models.Count;

    /// <summary>How many curated models are present in Ollama right now.</summary>
    public int InstalledCuratedCount
    {
        get => _installedCuratedCount;
        private set
        {
            if (SetField(ref _installedCuratedCount, value))
            {
                OnPropertyChanged(nameof(CuratedSummaryText));
            }
        }
    }

    /// <summary>How many installed models are outside the curated catalog (informational only).</summary>
    public int InstalledUnknownCount
    {
        get => _installedUnknownCount;
        private set
        {
            if (SetField(ref _installedUnknownCount, value))
            {
                OnPropertyChanged(nameof(CuratedSummaryText));
            }
        }
    }

    /// <summary>
    /// The installed curated model the offline planner would use, chosen by
    /// <see cref="LocalAiReadiness.SelectActiveModel"/>. <see langword="null"/>
    /// when no curated model is installed.
    /// </summary>
    public string? ActiveModelTag
    {
        get => _activeModelTag;
        private set
        {
            if (SetField(ref _activeModelTag, value))
            {
                OnPropertyChanged(nameof(PlannerReadinessText));
            }
        }
    }

    /// <summary>True when the offline planner toggle may be enabled (Ollama reachable + a curated model installed).</summary>
    public bool PlannerReady
    {
        get => _plannerReady;
        private set
        {
            if (SetField(ref _plannerReady, value))
            {
                OnPropertyChanged(nameof(PlannerReadinessText));
            }
        }
    }

    /// <summary>User-facing readiness hint for the AI Mode toggle.</summary>
    public string PlannerReadinessText
        => LocalAiReadiness.DescribeReadiness(EndpointReachable, ActiveModelTag);

    /// <summary>Single-line summary surfaced in the Settings card above the row list.</summary>
    public string CuratedSummaryText
    {
        get
        {
            var curated = $"Installed curated models: {InstalledCuratedCount} / {CuratedCatalogCount}";
            if (InstalledUnknownCount > 0)
            {
                var noun = InstalledUnknownCount == 1 ? "model" : "models";
                return $"{curated} · {InstalledUnknownCount} other {noun} present (not in curated catalog)";
            }
            return curated;
        }
    }

    /// <summary>
    /// Re-runs Ollama detection: PATH probe, HTTP probe, and <c>/api/tags</c>
    /// reconciliation against <see cref="LocalModelCatalog"/>. Never throws —
    /// failures degrade to "not detected" with a friendly message.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsProbing = true;
        try
        {
            EndpointDisplay = _runtime.Endpoint.ToString();

            var isExecutable = false;
            var isReachable = false;
            IReadOnlyList<OllamaModelInfo> installed = Array.Empty<OllamaModelInfo>();
            string? errorMessage = null;

            try
            {
                isExecutable = await _runtime.IsInstalledAsync(cancellationToken).ConfigureAwait(true);
                isReachable = await _runtime.IsReachableAsync(cancellationToken).ConfigureAwait(true);
                if (isReachable)
                {
                    installed = await _runtime.ListLocalModelsAsync(cancellationToken).ConfigureAwait(true);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Settings UI must never surface raw exceptions.
            catch (Exception)
            {
                errorMessage = "Could not probe Ollama runtime. Detection will retry on next refresh.";
            }
#pragma warning restore CA1031

            ExecutableDetected = isExecutable;
            EndpointReachable = isReachable;

            var byTag = new Dictionary<string, OllamaModelInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var model in installed)
            {
                if (!string.IsNullOrWhiteSpace(model.Tag))
                {
                    byTag[model.Tag] = model;
                }
            }

            var curatedHits = 0;
            foreach (var row in Models)
            {
                // A pull in flight owns its own row state (BeginDownload/ApplyProgress).
                // The detection refresh only reconciles idle/installed rows.
                if (row.IsDownloading)
                {
                    row.SetServerReachable(isReachable);
                    if (byTag.ContainsKey(row.ModelTag))
                    {
                        curatedHits++;
                    }
                    continue;
                }

                if (byTag.TryGetValue(row.ModelTag, out var match))
                {
                    row.ApplyInstalled(match);
                    curatedHits++;
                }
                else
                {
                    row.ApplyMissing();
                }
                // Download is offered only when the server can answer pull requests.
                row.SetServerReachable(isReachable);
            }
            InstalledCuratedCount = curatedHits;
            InstalledUnknownCount = Math.Max(0, byTag.Count - curatedHits);

            // M2.8: pick the active planner model and recompute readiness.
            ActiveModelTag = LocalAiReadiness.SelectActiveModel(byTag.Keys);
            PlannerReady = LocalAiReadiness.CanEnablePlanner(isReachable, ActiveModelTag);

            DetectionMessage = errorMessage ?? BuildMessage(isExecutable, isReachable, byTag.Count);
            // Status text properties are derived; raise change so XAML updates.
            OnPropertyChanged(nameof(EndpointStatusText));
            OnPropertyChanged(nameof(ExecutableStatusText));
        }
        finally
        {
            IsProbing = false;
            OnPropertyChanged(nameof(EndpointStatusText));
            OnPropertyChanged(nameof(ExecutableStatusText));
        }
    }

    private static string BuildMessage(bool isExecutable, bool isReachable, int installedCount)
    {
        if (isReachable && installedCount > 0)
        {
            return "Ollama is running. Curated models below show installed / missing.";
        }
        if (isReachable)
        {
            return "Ollama is running but no models are installed yet. Model download arrives in M2.6 with explicit permission.";
        }
        if (isExecutable)
        {
            return "Ollama is installed but the local server is not running. Start it with 'ollama serve' or restart Ollama.";
        }
        return "Ollama is not detected yet. The First Run Setup Wizard will guide installation in M2.5. No installation happens automatically.";
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

/// <summary>
/// The single source of truth for a catalog row's UI. Exactly one state is
/// active at a time, so the XAML can never show contradictory affordances
/// (e.g. a "Missing" chip next to a live download, or a Download button on an
/// installed model).
/// </summary>
public enum ModelRowState
{
    /// <summary>Not installed, idle. Shows the Missing chip and an actionable Download button.</summary>
    MissingIdle = 0,

    /// <summary>A pull is in flight. Shows the Downloading chip, progress, and Cancel — no Download button.</summary>
    Downloading = 1,

    /// <summary>The last pull was cancelled. Shows a retry hint and re-enables Download.</summary>
    Cancelled = 2,

    /// <summary>The last pull failed. Shows the error and re-enables Download for retry.</summary>
    Failed = 3,

    /// <summary>Installed and confirmed by <c>/api/tags</c>. Shows the Installed chip and metadata — no Download button.</summary>
    Installed = 4,
}

/// <summary>
/// Mutable row backing one entry in the curated model catalog. Class
/// rather than record because WinUI <c>x:Bind</c> requires get/set
/// properties (CS8852 on init-only records).
/// </summary>
/// <remarks>
/// M2.9 collapsed the previous bag of booleans (<c>IsInstalled</c> /
/// <c>IsDownloading</c> / <c>CanDownload</c>) into a single
/// <see cref="ModelRowState"/>. Every visibility/text/enabled property below
/// is <i>derived</i> from <see cref="State"/>, so a state change raises one
/// coherent set of UI updates and contradictory combinations are impossible.
/// </remarks>
public sealed class LocalModelRow : INotifyPropertyChanged
{
    private ModelRowState _state = ModelRowState.MissingIdle;
    private string _displaySize = string.Empty;
    private string _familyLabel = string.Empty;
    private string _parameterSize = string.Empty;
    private double _progressPercent;
    private bool _progressIsIndeterminate = true;
    private string _statusDetail = string.Empty;
    private bool _serverReachable;

    public LocalModelRow(LocalModelDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ModelTag = descriptor.ModelTag;
        DisplayName = descriptor.DisplayName;
        Description = descriptor.Description;
        HardwareNote = descriptor.HardwareNote;
        IsRecommended = descriptor.IsRecommended;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    public string ModelTag { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string HardwareNote { get; }
    public bool IsRecommended { get; }

    /// <summary>The single row state. Setting it refreshes every derived UI property.</summary>
    public ModelRowState State
    {
        get => _state;
        private set
        {
            if (_state == value)
            {
                return;
            }
            _state = value;
            Raise(nameof(State));
            RaiseDerived();
        }
    }

    // ---- derived UI (all read-only, single source of truth = State) ----

    /// <summary>True only in <see cref="ModelRowState.Installed"/>.</summary>
    public bool IsInstalled => State == ModelRowState.Installed;

    /// <summary>True only while a pull is in flight.</summary>
    public bool IsDownloading => State == ModelRowState.Downloading;

    /// <summary>The status chip text, one per state.</summary>
    public string StatusText => State switch
    {
        ModelRowState.Installed => "Installed",
        ModelRowState.Downloading => "Downloading",
        ModelRowState.Cancelled => "Cancelled",
        ModelRowState.Failed => "Failed",
        _ => "Missing",
    };

    /// <summary>The Download button is actionable only when missing/cancelled/failed AND the server is reachable.</summary>
    public bool CanDownload
        => _serverReachable && State is ModelRowState.MissingIdle or ModelRowState.Cancelled or ModelRowState.Failed;

    /// <summary>Show the Download button (hidden entirely once installing/installed).</summary>
    public Visibility DownloadButtonVisibility
        => State is ModelRowState.Installed or ModelRowState.Downloading ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Show the progress + cancel block only while downloading.</summary>
    public Visibility ProgressVisibility
        => State == ModelRowState.Downloading ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Show the status-detail line when there is something worth saying (downloading / cancelled / failed).</summary>
    public Visibility StatusDetailVisibility
        => string.IsNullOrEmpty(StatusDetail) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Show the installed-metadata chips only when installed and a size is known.</summary>
    public Visibility MetadataVisibility
        => State == ModelRowState.Installed && !string.IsNullOrEmpty(DisplaySize) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>0–100 progress for the bar; meaningful only while downloading.</summary>
    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetField(ref _progressPercent, value, nameof(ProgressPercent));
    }

    /// <summary>True when Ollama has not reported byte totals yet (manifest/verify phases).</summary>
    public bool ProgressIsIndeterminate
    {
        get => _progressIsIndeterminate;
        private set => SetField(ref _progressIsIndeterminate, value, nameof(ProgressIsIndeterminate));
    }

    /// <summary>Human-readable status line (e.g. <c>"downloading · 14%"</c>, <c>"Cancelled. You can retry."</c>).</summary>
    public string StatusDetail
    {
        get => _statusDetail;
        private set
        {
            if (SetField(ref _statusDetail, value, nameof(StatusDetail)))
            {
                Raise(nameof(StatusDetailVisibility));
            }
        }
    }

    /// <summary>Size string for installed models (e.g. <c>"3.6 GB"</c>); empty otherwise.</summary>
    public string DisplaySize
    {
        get => _displaySize;
        private set
        {
            if (SetField(ref _displaySize, value, nameof(DisplaySize)))
            {
                Raise(nameof(MetadataVisibility));
            }
        }
    }

    /// <summary>Family label parsed from Ollama details (e.g. <c>"gemma3"</c>); empty when unavailable.</summary>
    public string FamilyLabel
    {
        get => _familyLabel;
        private set => SetField(ref _familyLabel, value, nameof(FamilyLabel));
    }

    /// <summary>Parameter-size label (e.g. <c>"4B"</c>); empty when unavailable.</summary>
    public string ParameterSize
    {
        get => _parameterSize;
        private set => SetField(ref _parameterSize, value, nameof(ParameterSize));
    }

    // ---- transitions (called by the detection refresh and the wizard pull) ----

    /// <summary>Apply an installed-model match. Pulls size/family/parameter-size from the runtime payload.</summary>
    public void ApplyInstalled(OllamaModelInfo match)
    {
        ArgumentNullException.ThrowIfNull(match);
        DisplaySize = match.DisplaySize;
        FamilyLabel = match.Family ?? string.Empty;
        ParameterSize = match.ParameterSize ?? string.Empty;
        StatusDetail = string.Empty;
        ProgressPercent = 0;
        State = ModelRowState.Installed;
    }

    /// <summary>
    /// Reset to a not-installed state, preserving a Cancelled/Failed status (so a
    /// post-pull refresh that still shows "missing" keeps the retry hint).
    /// </summary>
    public void ApplyMissing()
    {
        DisplaySize = string.Empty;
        FamilyLabel = string.Empty;
        ParameterSize = string.Empty;
        // Don't clobber a just-set Cancelled/Failed state during the post-pull refresh.
        if (State is not (ModelRowState.Cancelled or ModelRowState.Failed))
        {
            State = ModelRowState.MissingIdle;
        }
    }

    /// <summary>Sets whether Ollama is reachable, which gates the Download button.</summary>
    public void SetServerReachable(bool reachable)
    {
        if (_serverReachable == reachable)
        {
            return;
        }
        _serverReachable = reachable;
        Raise(nameof(CanDownload));
    }

    /// <summary>Enter the downloading state. Clears prior status and resets progress.</summary>
    public void BeginDownload()
    {
        StatusDetail = "Starting…";
        ProgressPercent = 0;
        ProgressIsIndeterminate = true;
        State = ModelRowState.Downloading;
    }

    /// <summary>Apply a streaming progress tick while downloading.</summary>
    public void ApplyProgress(OllamaModelPullProgress update)
    {
        ArgumentNullException.ThrowIfNull(update);
        StatusDetail = update.DisplayText;
        if (update.Percent is { } pct)
        {
            ProgressIsIndeterminate = false;
            ProgressPercent = pct;
        }
        else
        {
            ProgressIsIndeterminate = true;
        }
    }

    /// <summary>Move to the cancelled state with a retry hint.</summary>
    public void MarkCancelled()
    {
        StatusDetail = "Cancelled. You can retry.";
        ProgressPercent = 0;
        State = ModelRowState.Cancelled;
    }

    /// <summary>Move to the failed state with a redaction-safe error.</summary>
    public void MarkFailed(string errorMessage)
    {
        StatusDetail = $"Download failed: {errorMessage}";
        ProgressPercent = 0;
        State = ModelRowState.Failed;
    }

    private void RaiseDerived()
    {
        Raise(nameof(IsInstalled));
        Raise(nameof(IsDownloading));
        Raise(nameof(StatusText));
        Raise(nameof(CanDownload));
        Raise(nameof(DownloadButtonVisibility));
        Raise(nameof(ProgressVisibility));
        Raise(nameof(StatusDetailVisibility));
        Raise(nameof(MetadataVisibility));
    }

    private bool SetField(ref string field, string value, string propertyName)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return false;
        }
        field = value;
        Raise(propertyName);
        return true;
    }

    private bool SetField(ref double field, double value, string propertyName)
    {
        if (field.Equals(value))
        {
            return false;
        }
        field = value;
        Raise(propertyName);
        return true;
    }

    private bool SetField(ref bool field, bool value, string propertyName)
    {
        if (field == value)
        {
            return false;
        }
        field = value;
        Raise(propertyName);
        return true;
    }

    private void Raise(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
