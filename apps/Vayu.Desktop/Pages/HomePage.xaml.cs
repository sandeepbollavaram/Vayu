using System.Collections.ObjectModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

using Vayu.Core;
using Vayu.Memory;
using Vayu_Desktop.Controls;

namespace Vayu_Desktop.Pages;

/// <summary>
/// Home dashboard: AI sphere, command console, status chips, quick cards,
/// and a compact recent-activity preview from the audit log.
/// </summary>
public sealed partial class HomePage : Page
{
    private readonly IAgentRuntime _runtime;
    private readonly IAuditLogService _audit;

    /// <summary>Bound to the "Recent activity" preview at the bottom of the page.</summary>
    public ObservableCollection<string> RecentRows { get; } = new();

    public HomePage()
    {
        InitializeComponent();
        _runtime = App.Services.GetRequiredService<IAgentRuntime>();
        _audit = App.Services.GetRequiredService<IAuditLogService>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await RefreshRecentAsync().ConfigureAwait(true);
    }

    private async void OnRun(object sender, RoutedEventArgs e)
    {
        await DispatchAsync().ConfigureAwait(true);
    }

    private async void CommandBox_OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            await DispatchAsync().ConfigureAwait(true);
        }
    }

    private async void OnExampleClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string text)
        {
            CommandBox.Text = text;
            await DispatchAsync().ConfigureAwait(true);
        }
    }

    private async Task DispatchAsync()
    {
        var text = CommandBox.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            ResultStatus.Text = "LAST RESULT · EMPTY";
            ResultMessage.Text = "Type a command first.";
            return;
        }

        SetRuntime("PROCESSING", VayuAccent.Blue);
        Sphere.SetState(VayuSphereState.Processing);

        var request = new CommandRequest { Text = text, Source = "text" };
        try
        {
            var result = await _runtime.DispatchAsync(request).ConfigureAwait(true);
            RenderResult(result);
            await RefreshRecentAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Sphere.SetState(VayuSphereState.Error);
            SetRuntime("ERROR", VayuAccent.Red);
            ResultStatus.Text = "LAST RESULT · ERROR";
            ResultIndicator.Fill = SolidBrush(VayuAccent.Red);
            ResultAgent.Text = string.Empty;
            ResultRisk.Text = string.Empty;
            ResultMessage.Text = $"{ex.GetType().Name}: {ex.Message}";
            ResultTimestamp.Text = DateTime.Now.ToString("HH:mm:ss");
        }
    }

    private void RenderResult(CommandResult result)
    {
        VayuAccent accent;
        VayuSphereState sphereState;
        string statusLabel;

        switch (result.Status)
        {
            case CommandStatus.Success:
                accent = VayuAccent.Teal;
                sphereState = VayuSphereState.Success;
                statusLabel = "SUCCESS";
                break;
            case CommandStatus.NeedsClarification:
                accent = VayuAccent.Violet;
                sphereState = VayuSphereState.Idle;
                statusLabel = "NEEDS CLARIFICATION";
                break;
            case CommandStatus.PermissionRequired:
                accent = VayuAccent.Amber;
                sphereState = VayuSphereState.Idle;
                statusLabel = "PERMISSION REQUIRED";
                break;
            case CommandStatus.Cancelled:
                accent = VayuAccent.Amber;
                sphereState = VayuSphereState.Error;
                statusLabel = "CANCELLED";
                break;
            case CommandStatus.Failed:
            default:
                accent = VayuAccent.Red;
                sphereState = VayuSphereState.Error;
                statusLabel = "FAILED";
                break;
        }

        Sphere.SetState(sphereState);
        SetRuntime(statusLabel, accent);

        ResultStatus.Text = $"LAST RESULT · {statusLabel}";
        ResultIndicator.Fill = SolidBrush(accent);
        ResultAgent.Text = result.AgentName is null ? string.Empty : $"agent: {result.AgentName}";
        ResultRisk.Text  = result.Plan is null ? string.Empty : $"risk: {result.Plan.Risk}";
        ResultMessage.Text = result.Message ?? result.ClarificationPrompt ?? string.Empty;
        ResultTimestamp.Text = DateTime.Now.ToString("HH:mm:ss");
    }

    private void SetRuntime(string label, VayuAccent accent)
    {
        RuntimeChipText.Text = $"RUNTIME · {label}";
        RuntimeDot.Fill = SolidBrush(accent);
    }

    private async Task RefreshRecentAsync()
    {
        try
        {
            var rows = await _audit.ListRecentAsync(6).ConfigureAwait(true);
            RecentRows.Clear();
            foreach (var r in rows)
            {
                RecentRows.Add(
                    $"{r.TimestampUtc.UtcDateTime:HH:mm:ss}  {r.RiskLevel,-2}  {r.Status,-19}  {r.AgentName ?? "-",-13}  {r.CommandText ?? string.Empty}");
            }
            RecentCountText.Text = rows.Count == 0 ? string.Empty : $"({rows.Count} most recent)";
            RecentEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            // The audit log is local-only — failures here are non-fatal for the UI.
        }
    }

    private enum VayuAccent { Cyan, Blue, Teal, Violet, Amber, Red }

    private static SolidColorBrush SolidBrush(VayuAccent accent)
    {
        var c = accent switch
        {
            VayuAccent.Cyan   => Windows.UI.Color.FromArgb(0xFF, 0x00, 0xE1, 0xFF),
            VayuAccent.Blue   => Windows.UI.Color.FromArgb(0xFF, 0x4F, 0xA8, 0xFF),
            VayuAccent.Teal   => Windows.UI.Color.FromArgb(0xFF, 0x2E, 0xE6, 0xC9),
            VayuAccent.Violet => Windows.UI.Color.FromArgb(0xFF, 0x9B, 0x7B, 0xFF),
            VayuAccent.Amber  => Windows.UI.Color.FromArgb(0xFF, 0xFF, 0xB8, 0x5C),
            VayuAccent.Red    => Windows.UI.Color.FromArgb(0xFF, 0xFF, 0x5C, 0x7C),
            _ => Colors.White,
        };
        return new SolidColorBrush(c);
    }
}
