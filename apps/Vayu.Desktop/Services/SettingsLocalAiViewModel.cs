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

            var installedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var model in installed)
            {
                if (!string.IsNullOrWhiteSpace(model.Tag))
                {
                    installedTags.Add(model.Tag);
                }
            }
            foreach (var row in Models)
            {
                row.IsInstalled = installedTags.Contains(row.ModelTag);
            }

            DetectionMessage = errorMessage ?? BuildMessage(isExecutable, isReachable, installedTags.Count);
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

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        OnPropertyChanged(propertyName);
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
        set
        {
            if (_isInstalled == value)
            {
                return;
            }
            _isInstalled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstalled)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
        }
    }

    /// <summary>Status pill text bound by the XAML row template.</summary>
    public string StatusText => IsInstalled ? "Installed" : "Missing";
}
