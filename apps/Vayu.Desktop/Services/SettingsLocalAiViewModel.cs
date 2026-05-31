using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
                // A pull in flight is owned by the wizard's PullAsync helper.
                if (!row.IsDownloading)
                {
                    row.CanDownload = isReachable && !row.IsInstalled;
                }
            }
            InstalledCuratedCount = curatedHits;
            InstalledUnknownCount = Math.Max(0, byTag.Count - curatedHits);

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
/// Mutable row backing one entry in the curated model catalog. Class
/// rather than record because WinUI <c>x:Bind</c> requires get/set
/// properties (CS8852 on init-only records).
/// </summary>
public sealed class LocalModelRow : INotifyPropertyChanged
{
    private bool _isInstalled;
    private string _displaySize = string.Empty;
    private string _familyLabel = string.Empty;
    private string _parameterSize = string.Empty;
    private bool _isDownloading;
    private string _downloadStatus = string.Empty;
    private bool _canDownload;

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

    /// <summary>True when this catalog entry is present in Ollama's <c>/api/tags</c> response.</summary>
    public bool IsInstalled
    {
        get => _isInstalled;
        private set
        {
            if (_isInstalled == value)
            {
                return;
            }
            _isInstalled = value;
            Raise(nameof(IsInstalled));
            Raise(nameof(StatusText));
        }
    }

    /// <summary>Status pill text bound by the XAML row template.</summary>
    public string StatusText => IsInstalled ? "Installed" : "Missing";

    /// <summary>Size string for installed models (e.g. <c>"3.6 GB"</c>); empty otherwise.</summary>
    public string DisplaySize
    {
        get => _displaySize;
        private set => SetField(ref _displaySize, value, nameof(DisplaySize));
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

    /// <summary>Apply an installed-model match. Pulls size/family/parameter-size from the runtime payload.</summary>
    public void ApplyInstalled(OllamaModelInfo match)
    {
        ArgumentNullException.ThrowIfNull(match);
        DisplaySize = match.DisplaySize;
        FamilyLabel = match.Family ?? string.Empty;
        ParameterSize = match.ParameterSize ?? string.Empty;
        IsInstalled = true;
        CanDownload = false;
        DownloadStatus = string.Empty;
    }

    /// <summary>Reset to the "missing" state — clears all installed-only metadata.</summary>
    public void ApplyMissing()
    {
        DisplaySize = string.Empty;
        FamilyLabel = string.Empty;
        ParameterSize = string.Empty;
        IsInstalled = false;
    }

    /// <summary>True while an M2.6 pull is in flight for this row.</summary>
    public bool IsDownloading
    {
        get => _isDownloading;
        set => SetField(ref _isDownloading, value, nameof(IsDownloading));
    }

    /// <summary>Per-row download status (Ollama's <c>downloading</c> line, percent, or final error/cancelled).</summary>
    public string DownloadStatus
    {
        get => _downloadStatus;
        set => SetField(ref _downloadStatus, value, nameof(DownloadStatus));
    }

    /// <summary>True when the Download affordance should be enabled (missing + Ollama reachable + not already downloading).</summary>
    public bool CanDownload
    {
        get => _canDownload;
        set
        {
            if (_canDownload == value)
            {
                return;
            }
            _canDownload = value;
            Raise(nameof(CanDownload));
        }
    }

    private void SetField(ref string field, string value, string propertyName)
    {
        if (string.Equals(field, value, StringComparison.Ordinal))
        {
            return;
        }
        field = value;
        Raise(propertyName);
    }

    private void SetField(ref bool field, bool value, string propertyName)
    {
        if (field == value)
        {
            return;
        }
        field = value;
        Raise(propertyName);
    }

    private void Raise(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
