using System.Collections.ObjectModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

using Vayu.Memory;

namespace Vayu_Desktop.Pages;

/// <summary>
/// Logs page: shows the most recent audit rows from
/// <see cref="IAuditLogService"/>. All values are already redacted by
/// the writers (<c>DefaultPermissionService</c> and <c>AgentRuntime</c>).
/// </summary>
public sealed partial class LogsPage : Page
{
    private readonly IAuditLogService _audit;

    /// <summary>One-line rendering of each audit row, newest first.</summary>
    public ObservableCollection<string> Rows { get; } = new();

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
        StatusLine.Text = "Loading…";
        try
        {
            var entries = await _audit.ListRecentAsync(200).ConfigureAwait(true);
            Rows.Clear();
            foreach (var row in entries)
            {
                Rows.Add(Format(row));
            }
            StatusLine.Text = $"{entries.Count} row(s)";
        }
        catch (Exception ex)
        {
            StatusLine.Text = $"Error: {ex.GetType().Name}";
        }
    }

    private static string Format(ActionLogEntry row)
        => $"{row.TimestampUtc.UtcDateTime:yyyy-MM-dd HH:mm:ss} "
         + $"{row.RiskLevel,-3} "
         + $"{row.PermissionDecision,-22} "
         + $"{row.Status,-19} "
         + $"{row.AgentName ?? "-",-15} "
         + $"{row.CommandText ?? string.Empty}"
         + (string.IsNullOrEmpty(row.ErrorMessage) ? string.Empty : $"  err={row.ErrorMessage}");
}
