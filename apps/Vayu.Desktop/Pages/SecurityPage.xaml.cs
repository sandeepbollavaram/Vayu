using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

using Vayu.Memory;
using Vayu.Security;

namespace Vayu_Desktop.Pages;

/// <summary>
/// Security page: shows whether a Gemini key is configured (never the
/// value), where the local audit log lives, and the M1 trust principles
/// rendered as cards. The Gemini status badge label/border swap together
/// to communicate state at a glance.
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

        AuditPathText.Text = Environment.ExpandEnvironmentVariables(_memory.DatabasePath);

        SetGeminiBadge("CHECKING…", VayuBadgeAccent.Cyan);

        try
        {
            var status = await _config.GetGeminiKeyStatusAsync().ConfigureAwait(true);
            if (status.IsConfigured)
            {
                SetGeminiBadge($"CONFIGURED · {status.Source?.ToUpperInvariant() ?? "UNKNOWN"}", VayuBadgeAccent.Teal);
            }
            else
            {
                SetGeminiBadge("NOT CONFIGURED", VayuBadgeAccent.Amber);
            }
        }
        catch (Exception ex)
        {
            SetGeminiBadge($"CHECK FAILED · {ex.GetType().Name.ToUpperInvariant()}", VayuBadgeAccent.Red);
        }
    }

    private enum VayuBadgeAccent { Cyan, Teal, Amber, Red }

    private void SetGeminiBadge(string label, VayuBadgeAccent accent)
    {
        GeminiStatusText.Text = label;

        var styleKey = accent switch
        {
            VayuBadgeAccent.Teal => "VayuChipTeal",
            VayuBadgeAccent.Amber => "VayuChipAmber",
            VayuBadgeAccent.Red => "VayuChipRed",
            _ => "VayuChip",
        };

        if (Application.Current.Resources[styleKey] is Style style)
        {
            GeminiStatusBadge.Style = style;
        }
    }
}
