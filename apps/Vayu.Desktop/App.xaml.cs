using System.Net.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

using Vayu.AgentRuntime;
using Vayu.AI.Local;
using Vayu.Automation.Windows;
using Vayu.Core;
using Vayu.Core.Setup;
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

        // --- Automation: known-app launcher + installed-app discovery + agent ---
        services.AddSingleton<IAppLauncher, WindowsAppLauncher>();
        services.AddSingleton<InstalledAppCatalog>();
        services.AddSingleton(sp => new AppLauncherAgent(
            sp.GetRequiredService<IAppLauncher>(),
            sp.GetRequiredService<InstalledAppCatalog>()));
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

        // --- Vayu.AI.Local (M2): detection-only runtime + provider adapter ---
        // Local AI defaults to disabled (LocalAiOptions.EnableLocalAi = false);
        // probes are read-only and bounded by OllamaProviderOptions.ProbeTimeoutSeconds.
        // Install and model pull are NOT registered here — they belong to M2.6 with explicit consent.
        services.AddSingleton(new LocalAiOptions());
        services.AddSingleton(new OllamaProviderOptions());
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<OllamaProviderOptions>();
            return new HttpClient { BaseAddress = options.Endpoint };
        });
        services.AddSingleton<IOllamaRuntimeService>(sp => new OllamaRuntimeService(
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<OllamaProviderOptions>()));
        services.AddSingleton<ILocalAiProvider>(sp => new OllamaLocalAiProvider(
            sp.GetRequiredService<IOllamaRuntimeService>()));

        // --- First Run Setup Wizard (M2.5): in-memory state for now; SQLite persistence lands in M2.8 ---
        services.AddSingleton<IFirstRunSetupService>(sp => new InMemoryFirstRunSetupService(
            sp.GetRequiredService<IClock>()));

        return services.BuildServiceProvider();
    }
}
