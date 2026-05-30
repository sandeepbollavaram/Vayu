using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

using Vayu.AgentRuntime;
using Vayu.Automation.Windows;
using Vayu.Core;
using Vayu.Logging;
using Vayu.Memory;
using Vayu.Permissions;
using Vayu.Security;

using Vayu_Desktop.Agents;
using Vayu_Desktop.Services;

namespace Vayu_Desktop;

/// <summary>
/// Application bootstrap. Builds the dependency-injection container, wires
/// every Vayu module (logging → security → memory → permissions → agent
/// runtime → automation), and shows the main window.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>Service provider built during <see cref="OnLaunched"/>. Pages resolve services from here.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services = BuildServices();
        _window = new MainWindow();
        _window.Activate();
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // --- Core / clock ---
        services.AddSingleton<IClock, SystemClock>();

        // --- Logging (Serilog + redactor + in-memory ring buffer) ---
        services.AddSingleton(new LoggingOptions());
        services.AddSingleton(sp => new InMemoryLogSink(sp.GetRequiredService<LoggingOptions>().InMemoryCapacity));
        services.AddSingleton(sp => VayuLogger.Create(
            sp.GetRequiredService<LoggingOptions>(),
            sp.GetRequiredService<InMemoryLogSink>()));

        // --- Security: secret stores + redactor consumers ---
        services.AddSingleton<EnvironmentSecretStore>();
        services.AddSingleton<EncryptedJsonSecretStore>();
        services.AddSingleton<WindowsCredentialSecretStore>();
        services.AddSingleton(sp => new SecureConfigService(
            sp.GetRequiredService<WindowsCredentialSecretStore>(),
            sp.GetRequiredService<EnvironmentSecretStore>(),
            sp.GetRequiredService<EncryptedJsonSecretStore>()));

        // --- Memory: SQLite audit log ---
        services.AddSingleton(new MemoryOptions());
        services.AddSingleton<IAuditLogService>(sp => new SqliteAuditLogService(sp.GetRequiredService<MemoryOptions>()));

        // --- Permissions: policy + WinUI prompt + engine ---
        services.AddSingleton(PermissionPolicy.Default);
        services.AddSingleton<IConfirmationPrompt, WinUiConfirmationPrompt>();
        services.AddSingleton<IPermissionService>(sp => new DefaultPermissionService(
            sp.GetRequiredService<PermissionPolicy>(),
            sp.GetRequiredService<IConfirmationPrompt>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<IAuditLogService>()));

        // --- UI navigation bridge (used by show-logs / show-settings agents) ---
        services.AddSingleton<UiNavigationService>();

        // --- Automation: known-app launcher + agent ---
        services.AddSingleton<IAppLauncher, WindowsAppLauncher>();
        services.AddSingleton(sp => new AppLauncherAgent(sp.GetRequiredService<IAppLauncher>()));
        services.AddSingleton(sp => new ShowLogsAgent(sp.GetRequiredService<UiNavigationService>()));
        services.AddSingleton(sp => new ShowSettingsAgent(sp.GetRequiredService<UiNavigationService>()));

        // --- Agent registry (populated at construction) ---
        services.AddSingleton(sp =>
        {
            var registry = new AgentRegistry();
            registry.Register(sp.GetRequiredService<AppLauncherAgent>());
            registry.Register(sp.GetRequiredService<ShowLogsAgent>());
            registry.Register(sp.GetRequiredService<ShowSettingsAgent>());
            return registry;
        });

        // --- Planner + runtime ---
        services.AddSingleton<RuleBasedCommandParser>();
        services.AddSingleton<IIntentPlanner>(sp => new IntentPlanner(sp.GetRequiredService<RuleBasedCommandParser>()));
        services.AddSingleton(new AgentRuntimeOptions());
        services.AddSingleton<IAgentRuntime>(sp => new AgentRuntime(
            sp.GetRequiredService<IIntentPlanner>(),
            sp.GetRequiredService<AgentRegistry>(),
            sp.GetRequiredService<IPermissionService>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<IAuditLogService>(),
            sp.GetRequiredService<AgentRuntimeOptions>()));

        return services.BuildServiceProvider();
    }
}
