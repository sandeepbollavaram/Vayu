using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

using Vayu.Core;

namespace Vayu_Desktop.Pages;

/// <summary>
/// Home page: command input, last result, and the M1 mode/provider chips.
/// Resolves <see cref="IAgentRuntime"/> from <see cref="App.Services"/>.
/// </summary>
public sealed partial class HomePage : Page
{
    private readonly IAgentRuntime _runtime;

    public HomePage()
    {
        InitializeComponent();
        _runtime = App.Services.GetRequiredService<IAgentRuntime>();
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

    private async Task DispatchAsync()
    {
        var text = CommandBox.Text?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            ResultText.Text = "(empty command)";
            return;
        }

        var request = new CommandRequest { Text = text, Source = "text" };
        try
        {
            var result = await _runtime.DispatchAsync(request).ConfigureAwait(true);
            var detail = result.Message ?? result.ClarificationPrompt ?? string.Empty;
            ResultText.Text =
                $"[{result.Status}] agent={result.AgentName ?? "-"} risk={result.Plan?.Risk}{Environment.NewLine}{detail}";
        }
        catch (Exception ex)
        {
            ResultText.Text = $"Error: {ex.GetType().Name} — {ex.Message}";
        }
    }
}
