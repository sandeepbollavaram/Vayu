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

    /// <summary>The shell window, exposed so dialogs/pickers can obtain the HWND.</summary>
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    /// <summary>The free-floating desktop sphere overlay.</summary>
    public static SphereOverlayWindow? SphereOverlay { get; private set; }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Services = BuildServices();
        _window = new MainWindow();
        MainWindow = _window;
        _window.Activate();

        // Free-floating desktop sphere: a control surface that opens/focuses the
        // Command Center. It never captures audio or automates on its own.
        var overlay = new SphereOverlayWindow();
        overlay.OpenRequested += (_, _) =>
        {
            _window.AppWindow.Show();
            _window.Activate();
        };
        var coordinator = Services.GetService<Services.WakeWordCoordinator>();
        overlay.StartListeningRequested += async (_, _) =>
        {
            if (coordinator is not null)
            {
                await coordinator.StartAsync();
            }
        };
        overlay.StopListeningRequested += async (_, _) =>
        {
            if (coordinator is not null)
            {
                await coordinator.StopAsync();
            }
        };
        overlay.Activate();
        SphereOverlay = overlay;
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        // --- Core / clock ---
        services.AddSingleton<IClock, SystemClock>();

        // --- Active storage paths (resolved once at startup) ---
        // The bootstrap settings DB stays at the fixed default location — it is
        // what tells us the chosen root, so it cannot itself move. We read the
        // persisted setup state from it synchronously, then derive the active
        // logs/cache/models/assets paths. Fail-safe to defaults; never crashes.
        var activeStoragePaths = ResolveActiveStoragePaths();
        services.AddSingleton<IVayuStoragePathProvider>(activeStoragePaths);

        // --- Logging (Serilog + redactor + in-memory ring buffer) ---
        // Log files go under the active logs path chosen during setup.
        services.AddSingleton(new LoggingOptions { Directory = activeStoragePaths.ActivePaths.LogsPath });
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

        // --- Memory: SQLite audit log + durable user settings ---
        services.AddSingleton(new MemoryOptions());
        services.AddSingleton<IAuditLogService>(sp => new SqliteAuditLogService(sp.GetRequiredService<MemoryOptions>()));
        services.AddSingleton<IUserSettingsStore>(sp => new SqliteUserSettingsStore(sp.GetRequiredService<MemoryOptions>()));

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

        // --- M5.2: safe desktop automation (open-and-type) ---
        services.AddSingleton<IWindowDiscoveryService, WindowsWindowDiscoveryService>();
        services.AddSingleton<AutomationSafetyPolicy>();
        services.AddSingleton<IAutomationConfirmationService, WinUiAutomationConfirmationService>();
        services.AddSingleton<ITextTypingExecutor, WindowsTextTypingExecutor>();
        services.AddSingleton(sp => new DesktopAutomationAgent(
            sp.GetRequiredService<IAppLauncher>(),
            sp.GetRequiredService<IWindowDiscoveryService>(),
            sp.GetRequiredService<AutomationSafetyPolicy>(),
            sp.GetRequiredService<IAutomationConfirmationService>(),
            sp.GetRequiredService<ITextTypingExecutor>()));

        // --- Agent registry (populated at construction) ---
        services.AddSingleton(sp =>
        {
            var registry = new AgentRegistry();
            registry.Register(sp.GetRequiredService<AppLauncherAgent>());
            registry.Register(sp.GetRequiredService<ShowLogsAgent>());
            registry.Register(sp.GetRequiredService<ShowSettingsAgent>());
            registry.Register(sp.GetRequiredService<DesktopAutomationAgent>());
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
                transcribe: engine.TranscribeAsync,
                modelExists: null,
                // "Ready" only when whisper.net can actually load the model.
                readinessProbe: path =>
                {
                    var ok = engine.TryProbeRuntime(path, out var message);
                    return (ok, message);
                });
        });
        // Consented model download (HttpClient is the only network seam).
        services.AddSingleton(_ => new WhisperModelDownloadService(new HttpClient()));

        // --- Wake word ("Hey Vayu", Vosk, off by default, on-device) ---
        // Off by default; the user enables it and points at a local Vosk model.
        // The default model path is a "wake" folder under the active models dir.
        services.AddSingleton(sp =>
        {
            var models = sp.GetService<IVayuStoragePathProvider>()?.ActivePaths.ModelsPath
                         ?? System.IO.Path.Combine(
                             Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                             "Vayu", "models");
            return new WakeWordConfigState(new WakeWordOptions
            {
                EnableWakeWord = false,
                ModelPath = System.IO.Path.Combine(models, "vosk-wake"),
            });
        });
        services.AddSingleton<IWakeWordService>(sp =>
            new VoskWakeWordEngine(sp.GetRequiredService<WakeWordConfigState>()));
        services.AddSingleton(sp => new WakeWordCoordinator(
            sp.GetRequiredService<IWakeWordService>(), sp));
        // Consented Vosk wake-model download + extract (HttpClient is the only network seam).
        services.AddSingleton(_ => new VoskModelDownloadService(new HttpClient()));

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

        // --- First Run Setup: durable SQLite-backed state (survives restarts) ---
        services.AddSingleton<IFirstRunSetupService>(sp =>
        {
            var store = sp.GetRequiredService<IUserSettingsStore>();
            return new PersistentFirstRunSetupService(
                get: store.GetAsync,
                set: store.SetAsync,
                clock: sp.GetRequiredService<IClock>());
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Reads the persisted storage choice from the bootstrap settings DB (fixed
    /// default location) and resolves the active storage paths. Synchronous and
    /// fail-safe — any failure yields the default paths so startup never breaks.
    /// </summary>
    private static VayuStoragePathProvider ResolveActiveStoragePaths()
    {
        try
        {
            var bootstrap = new SqliteUserSettingsStore(new MemoryOptions());
            var json = bootstrap.GetAsync(PersistentFirstRunSetupService.StateKey)
                .GetAwaiter().GetResult();
            if (!string.IsNullOrWhiteSpace(json))
            {
                var state = System.Text.Json.JsonSerializer.Deserialize<FirstRunSetupState>(json);
                return new VayuStoragePathProvider(state?.StoragePaths);
            }
        }
#pragma warning disable CA1031 // Startup path resolution must never crash; fall back to defaults.
        catch (Exception)
        {
            // Fall through to defaults.
        }
#pragma warning restore CA1031
        return new VayuStoragePathProvider(persisted: null);
    }
}
