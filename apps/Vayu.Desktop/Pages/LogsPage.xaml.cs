using System.Collections.ObjectModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

using Vayu.Memory;

namespace Vayu_Desktop.Pages;

/// <summary>
/// View-model bound to one row in <see cref="LogsPage"/>'s list. Plain
/// get/set so the XAML type-info generator can wire <c>x:Bind</c> against it.
/// </summary>
public sealed class AuditRow
{
    public string Timestamp { get; set; } = string.Empty;
    public string Risk { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string AgentAndError { get; set; } = string.Empty;
}

/// <summary>
/// Audit-log console. Reads <see cref="IAuditLogService.ListRecentAsync"/>
/// and renders each row as a card with chips. All values are already
/// redacted by the writers — no further sanitisation in the UI.
/// </summary>
public sealed partial class LogsPage : Page
{
    private readonly IAuditLogService _audit;

    public ObservableCollection<AuditRow> Rows { get; } = new();

    public LogsPage()
    {
        InitializeComponent();
        _audit = App.Services.GetRequiredService<IAuditLogService>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await RefreshAsync().ConfigureAwait(true);
    }

    private async void OnRefresh(object sender, RoutedEventArgs e)
    {
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task RefreshAsync()
    {
        StatusLine.Text = "LOADING…";
        try
        {
            var entries = await _audit.ListRecentAsync(200).ConfigureAwait(true);
            Rows.Clear();
            foreach (var row in entries)
            {
                Rows.Add(new AuditRow
                {
                    Timestamp = row.TimestampUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    Risk = row.RiskLevel.ToString(),
                    Decision = row.PermissionDecision.ToString().ToUpperInvariant(),
                    Status = row.Status.ToString().ToUpperInvariant(),
                    Command = row.CommandText ?? string.Empty,
                    AgentAndError = BuildAgentLine(row),
                });
            }
            StatusLine.Text = $"{entries.Count} ROW(S)";
            EmptyState.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            StatusLine.Text = $"ERROR · {ex.GetType().Name}";
            EmptyState.Visibility = Visibility.Visible;
        }
    }

    private static string BuildAgentLine(ActionLogEntry row)
    {
        var agent = string.IsNullOrEmpty(row.AgentName) ? "-" : row.AgentName;
        return string.IsNullOrEmpty(row.ErrorMessage)
            ? $"agent · {agent}"
            : $"agent · {agent}    err · {row.ErrorMessage}";
    }
}
