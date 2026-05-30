using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

using Vayu.Memory;
using Vayu.Security;

namespace Vayu_Desktop.Pages;

/// <summary>
/// Security page: shows whether a Gemini key is configured (never the
/// value), and where the local audit log lives.
/// </summary>
public sealed partial class SecurityPage : Page
{
    private readonly SecureConfigService _config;
    private readonly MemoryOptions _memory;

    public SecurityPage()
    {
        InitializeComponent();
        _config = App.Services.GetRequiredService<SecureConfigService>();
        _memory = App.Services.GetRequiredService<MemoryOptions>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        AuditPathText.Text = $"Path: {Environment.ExpandEnvironmentVariables(_memory.DatabasePath)}";

        try
        {
            var status = await _config.GetGeminiKeyStatusAsync().ConfigureAwait(true);
            GeminiStatus.Text = status.IsConfigured
                ? $"Status: configured (source: {status.Source})"
                : "Status: not configured";
        }
        catch (Exception ex)
        {
            GeminiStatus.Text = $"Status: check failed ({ex.GetType().Name})";
        }
    }
}
