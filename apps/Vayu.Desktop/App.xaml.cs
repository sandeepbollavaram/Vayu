using System.Net.Http;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;

using Vayu.AgentRuntime;
using Vayu.AI.Gemini;
using Vayu.AI.Local;
using Vayu.AI.Online;
using Vayu.Automation.Windows;
using Vayu.Core;
using Vayu.Core.Setup;
using Vayu.Logging;
using Vayu.Memory;
using Vayu.Permissions;
using Vayu.Security;
using Vayu.Voice;

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

        // M3.3: Gemini key setup (save → Credential Manager; status value-free; remove writable stores only).
        services.AddSingleton(sp => new GeminiKeySetupService(
            sp.GetRequiredService<WindowsCredentialSecretStore>(),
            sp.GetRequiredService<EnvironmentSecretStore>(),
            sp.GetRequiredService<EncryptedJsonSecretStore>()));

        // M3.4: cloud consent dialog + Gemini connector for the consented Test-key round-trip.
        services.AddSingleton<DesktopCloudConsentService>();
        services.AddSingleton<ICloudConsentService>(sp => sp.GetRequiredService<DesktopCloudConsentService>());
        services.AddSingleton(new GeminiProviderOptions());
        services.AddSingleton(sp => new GeminiProvider(
            new HttpClient(),
            sp.GetRequiredService<SecureConfigService>(),
            sp.GetRequiredService<GeminiProviderOptions>()));

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
        services.AddSingleton<WindowsAppPathsResolver>();
        services.AddSingleton(sp => new AppLauncherAgent(
            sp.GetRequiredService<IAppLauncher>(),
            sp.GetRequiredService<InstalledAppCatalog>(),
            sp.GetRequiredService<WindowsAppPathsResolver>()));
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

        // --- Vayu.AI.Local (M2): detection-only runtime + provider adapter ---
        // Local AI defaults to disabled (LocalAiOptions.EnableLocalAi = false);
        // probes are read-only and bounded by OllamaProviderOptions.ProbeTimeoutSeconds.
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

        // M2.6: explicit-consent model pull. The service refuses unknown tags;
        // the wizard adds a consent dialog and per-row cancellation.
        services.AddSingleton<IOllamaModelPullService>(sp => new OllamaModelPullService(
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<OllamaProviderOptions>()));

        // --- M2.7: local AI planning (opt-in, off by default) ---
        services.AddSingleton(new LocalAiPlannerOptions());
        // Live, user-flippable toggle the Settings page writes and the router reads.
        services.AddSingleton(sp => new LocalAiPlannerState(
            sp.GetRequiredService<LocalAiPlannerOptions>().EnableOfflinePlanning));
        services.AddSingleton<ILocalIntentPlanner>(sp => new OllamaIntentPlanner(
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<OllamaProviderOptions>(),
            sp.GetRequiredService<LocalAiPlannerOptions>()));

        // --- Planner + runtime ---
        // The AI Router replaces the bare IntentPlanner: it tries the local AI
        // planner when the user has enabled offline planning, and always falls
        // back to the rule-based parser. Every plan it returns still flows
        // through AgentRuntime -> IPermissionService -> agent -> audit log.
        services.AddSingleton<RuleBasedCommandParser>();
        services.AddSingleton<IIntentPlanner>(sp => new AiRouterIntentPlanner(
            sp.GetRequiredService<RuleBasedCommandParser>(),
            sp.GetRequiredService<ILocalIntentPlanner>(),
            sp.GetRequiredService<LocalAiPlannerOptions>(),
            sp.GetRequiredService<LocalAiPlannerState>(),
            // M3.5: Online/Hybrid routing — Gemini behind the cloud consent dialog.
            sp.GetRequiredService<GeminiProvider>(),
            sp.GetRequiredService<ICloudConsentService>()));
        services.AddSingleton(new AgentRuntimeOptions());
        services.AddSingleton<IAgentRuntime>(sp => new AgentRuntime(
            sp.GetRequiredService<IIntentPlanner>(),
            sp.GetRequiredService<AgentRegistry>(),
            sp.GetRequiredService<IPermissionService>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<IAuditLogService>(),
            sp.GetRequiredService<AgentRuntimeOptions>()));

        // --- M4.2: voice push-to-talk UI foundation (stub — no mic capture) ---
        services.AddSingleton<IVoiceInputService, StubVoiceInputService>();
        services.AddSingleton<IVoiceActivitySink>(_ => new InMemoryVoiceActivitySink());

        // --- Local STT: real push-to-talk mic capture + delegating provider ---
        // Capture is Windows-only and lives in the desktop adapter; the library
        // stays native-free. The transcription delegate is the seam a real local
        // engine (e.g. Whisper) plugs into — null here, so the provider honestly
        // reports the runtime as unavailable rather than faking a transcript.
        services.AddSingleton(new LocalSpeechToTextOptions());
        services.AddSingleton(sp => new VoiceSttState(sp.GetRequiredService<LocalSpeechToTextOptions>()));
        // The real whisper.net engine (the only place the native runtime is used).
        services.AddSingleton<WhisperNetSpeechToTextEngine>();
        // Transient so each resolve reads the live model-path/enable state the
        // Settings → Voice Setup surface may have just updated. The whisper.net
        // engine is the injected transcription delegate — real, local, no fake.
        services.AddTransient<IAudioCaptureService>(sp =>
            new WindowsAudioCaptureService(sp.GetRequiredService<VoiceSttState>().Options));
        services.AddTransient<ISpeechToTextProvider>(sp =>
        {
            var engine = sp.GetRequiredService<WhisperNetSpeechToTextEngine>();
            return new DelegatingLocalSttProvider(
                sp.GetRequiredService<VoiceSttState>().Options,
                transcribe: engine.TranscribeAsync);
        });

        // --- M4.4: system TTS (off by default; opt-in via the Settings toggle/VoiceTtsState) ---
        services.AddSingleton(new TextToSpeechOptions());
        services.AddSingleton(new VoiceTtsState());
        services.AddSingleton<WinUiSpeechAdapter>();
        services.AddSingleton<ITextToSpeechService>(sp =>
        {
            var adapter = sp.GetRequiredService<WinUiSpeechAdapter>();
            var state = sp.GetRequiredService<VoiceTtsState>();
            return new SystemTextToSpeechService(
                sp.GetRequiredService<TextToSpeechOptions>(),
                speakAsync: adapter.SpeakAsync,
                stopAsync: adapter.StopAsync,
                isEnabledOverride: () => state.Enabled);
        });

        // --- M4.5: voice → AgentRuntime pipeline (off by default; same runtime/permission/audit as typed) ---
        // The service's own EnableVoiceCommands gate is on; the Home toggle decides whether to route to it.
        services.AddSingleton(new VoiceCommandOptions { EnableVoiceCommands = true });
        services.AddSingleton(sp => new VoiceCommandService(
            sp.GetRequiredService<IVoiceInputService>(),
            sp.GetRequiredService<IAgentRuntime>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<VoiceCommandOptions>(),
            sp.GetService<IVoiceActivitySink>(),
            sp.GetService<ITextToSpeechService>(),
            sp.GetService<VoiceTtsState>()));

        // --- First Run Setup Wizard (M2.5): in-memory state for now; SQLite persistence lands in M2.8 ---
        services.AddSingleton<IFirstRunSetupService>(sp => new InMemoryFirstRunSetupService(
            sp.GetRequiredService<IClock>()));

        return services.BuildServiceProvider();
    }
}
