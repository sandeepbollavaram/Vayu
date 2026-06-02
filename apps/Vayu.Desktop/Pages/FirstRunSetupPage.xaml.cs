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
    private readonly PersistentFirstRunSetupService? _persistentSetup;
    private CancellationTokenSource? _activePullCts;

    public FirstRunSetupPage()
    {
        InitializeComponent();

        _persistentSetup = App.Services?.GetService<IFirstRunSetupService>() as PersistentFirstRunSetupService;
        _ = PrefillStorageAsync();

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

    // ---- Storage step (M4.12) ----

    private async Task PrefillStorageAsync()
    {
        if (_persistentSetup is null)
        {
            StorageRootBox.Text = DefaultStorageRoot();
            UpdateStoragePreview(StorageRootBox.Text);
            return;
        }
        try
        {
            var state = await _persistentSetup.GetStateAsync().ConfigureAwait(true);
            StorageRootBox.Text = state.StoragePaths?.WorkspaceRoot is { Length: > 0 } ws
                ? Path.GetDirectoryName(ws) ?? DefaultStorageRoot()
                : DefaultStorageRoot();
        }
        catch
        {
            StorageRootBox.Text = DefaultStorageRoot();
        }
        UpdateStoragePreview(StorageRootBox.Text);
    }

    private static string DefaultStorageRoot()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vayu");

    private void OnResetStorageClick(object sender, RoutedEventArgs e)
    {
        StorageRootBox.Text = DefaultStorageRoot();
        StorageMessageText.Text = "Reset to the default location.";
        UpdateStoragePreview(StorageRootBox.Text);
    }

    private async void OnBrowseStorageClick(object sender, RoutedEventArgs e)
    {
        var picker = new global::Windows.Storage.Pickers.FolderPicker
        {
            SuggestedStartLocation = global::Windows.Storage.Pickers.PickerLocationId.ComputerFolder,
        };
        picker.FileTypeFilter.Add("*");

        // WinUI 3 desktop: the picker must be associated with the app window (HWND).
        var window = App.MainWindow;
        if (window is null)
        {
            StorageMessageText.Text = "Folder picker is unavailable; type a path instead.";
            return;
        }
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        global::Windows.Storage.StorageFolder? folder;
        try
        {
            folder = await picker.PickSingleFolderAsync();
        }
#pragma warning disable CA1031 // Picker failure is non-fatal; keep the text field usable.
        catch (Exception)
        {
            StorageMessageText.Text = "Folder picker failed; type a path instead.";
            return;
        }
#pragma warning restore CA1031

        if (folder is not null)
        {
            StorageRootBox.Text = folder.Path;
            UpdateStoragePreview(folder.Path);
        }
    }

    private void UpdateStoragePreview(string? root)
    {
        if (!VayuStoragePaths.IsValidPathShape(root))
        {
            StoragePathsPreview.Text = string.Empty;
            return;
        }
        var full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(root!));
        StoragePathsPreview.Text =
            $"workspace: {Path.Combine(full, "workspace")}\n" +
            $"logs: {Path.Combine(full, "logs")}\n" +
            $"cache: {Path.Combine(full, "cache")}\n" +
            $"models: {Path.Combine(full, "models")}\n" +
            $"assets: {Path.Combine(full, "assets")}";
    }

    private async void OnSaveStorageClick(object sender, RoutedEventArgs e)
    {
        if (_persistentSetup is null)
        {
            StorageMessageText.Text = "Storage settings are not available in this build.";
            return;
        }

        var root = StorageRootBox.Text?.Trim();
        if (!VayuStoragePaths.IsValidPathShape(root))
        {
            StorageMessageText.Text = "Enter a valid absolute folder path (e.g. C:\\Vayu).";
            return;
        }

        root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(root!));
        var paths = new VayuStoragePaths(
            WorkspaceRoot: Path.Combine(root, "workspace"),
            LogsPath: Path.Combine(root, "logs"),
            CachePath: Path.Combine(root, "cache"),
            ModelsPath: Path.Combine(root, "models"),
            AssetsPath: Path.Combine(root, "assets"));

        // Create the folders only now, after the user's explicit Save. Non-destructive.
        try
        {
            foreach (var p in paths.AllPaths)
            {
                Directory.CreateDirectory(p);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StorageMessageText.Text = "That folder is not writable. Pick another location.";
            return;
        }

        try
        {
            var state = await _persistentSetup.GetStateAsync().ConfigureAwait(true);
            await _persistentSetup.UpdateAsync(state with { StoragePaths = paths }).ConfigureAwait(true);
            StorageMessageText.Text = $"Storage saved under {root}. Takes effect on next launch; existing data is not moved.";
            UpdateStoragePreview(root);
        }
        catch
        {
            StorageMessageText.Text = "Could not save storage settings.";
        }
    }
}
