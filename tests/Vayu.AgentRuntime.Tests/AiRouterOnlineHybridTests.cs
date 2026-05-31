using System.Collections.Immutable;

using Vayu.AgentRuntime;
using Vayu.AI.Local;
using Vayu.AI.Online;
using Vayu.Core;

namespace Vayu.AgentRuntime.Tests;

public class AiRouterOnlineHybridTests
{
    // ---- Online mode ----

    [Fact]
    public async Task Online_NoProvider_FallsBackSafely()
    {
        var router = NewRouter(PlanningMode.Online, online: null, consent: null, local: NoLocal());

        var plan = await router.PlanAsync(Request("open notepad"));

        Assert.Equal("app.open", plan.Intent);
        Assert.Equal("online-unavailable-rule-based", plan.PlanSource);
    }

    [Fact]
    public async Task Online_ConsentCancel_DoesNotCallCloud()
    {
        var online = new StubOnline(CloudPlan("app.open", "spotify"));
        var consent = new StubConsent(CloudConsentDecision.Cancel);
        var router = NewRouter(PlanningMode.Online, online, consent, NoLocal());

        var plan = await router.PlanAsync(Request("open notepad"));

        Assert.Equal(0, online.CallCount);
        Assert.Equal("cloud-consent-cancelled-rule-based", plan.PlanSource);
        Assert.Equal("notepad", plan.Args["app"]);
    }

    [Fact]
    public async Task Online_UseLocalInstead_DoesNotCallCloud_UsesLocalThenRule()
    {
        var online = new StubOnline(CloudPlan("app.open", "spotify"));
        var consent = new StubConsent(CloudConsentDecision.UseLocalInstead);
        // Local available and confident → used instead of cloud.
        var local = new StubLocal(LocalPlan("app.open", "vscode", 0.95));
        var router = NewRouter(PlanningMode.Online, online, consent, local);

        var plan = await router.PlanAsync(Request("open my editor"));

        Assert.Equal(0, online.CallCount);
        Assert.Equal("vscode", plan.Args["app"]);
    }

    [Fact]
    public async Task Online_UseLocalInstead_NoLocal_FallsBackRule()
    {
        var consent = new StubConsent(CloudConsentDecision.UseLocalInstead);
        var router = NewRouter(PlanningMode.Online, new StubOnline(CloudPlan("app.open", "x")), consent, NoLocal());

        var plan = await router.PlanAsync(Request("open notepad"));

        Assert.Equal("cloud-use-local-fallback", plan.PlanSource);
        Assert.Equal("notepad", plan.Args["app"]);
    }

    [Fact]
    public async Task Online_AllowOnce_UsesCloudPlan_WhenValid()
    {
        var online = new StubOnline(CloudPlan("app.open", "spotify"));
        var consent = new StubConsent(CloudConsentDecision.AllowOnce);
        var router = NewRouter(PlanningMode.Online, online, consent, NoLocal());

        var plan = await router.PlanAsync(Request("play music"));

        Assert.Equal(1, online.CallCount);
        Assert.Equal("app.open", plan.Intent);
        Assert.Equal("spotify", plan.Args["app"]);
        Assert.Equal("gemini:test", plan.PlanSource);
    }

    [Fact]
    public async Task Online_AllowOnce_CloudFails_FallsBackRule()
    {
        var online = new StubOnline(OnlineAiPlanningResult.Failed("model error", "gemini", "test"));
        var consent = new StubConsent(CloudConsentDecision.AllowOnce);
        var router = NewRouter(PlanningMode.Online, online, consent, NoLocal());

        var plan = await router.PlanAsync(Request("open notepad"));

        Assert.Equal(1, online.CallCount);
        Assert.Equal("gemini-fallback-rule-based", plan.PlanSource);
        Assert.Equal("notepad", plan.Args["app"]);
    }

    [Fact]
    public async Task Online_AllowOnce_CloudThrows_FallsBackRule()
    {
        var online = new StubOnline(throws: true);
        var consent = new StubConsent(CloudConsentDecision.AllowOnce);
        var router = NewRouter(PlanningMode.Online, online, consent, NoLocal());

        var plan = await router.PlanAsync(Request("open notepad"));

        Assert.Equal("gemini-fallback-rule-based", plan.PlanSource);
    }

    [Fact]
    public async Task Online_CloudTypingRequest_PreservedForM5()
    {
        // Gemini-validated app.open carrying the typing flag → router passes it through;
        // the AppLauncherAgent (not the router) defers it to M5.
        var typingPlan = new IntentPlan
        {
            Intent = "app.open",
            Risk = RiskLevel.L1,
            Args = ImmutableDictionary<string, string>.Empty.Add("app", "notepad").Add("typing_requested", "true"),
            Confidence = 0.9,
            PlanSource = "gemini:test",
        };
        var online = new StubOnline(OnlineAiPlanningResult.Succeeded(typingPlan, "gemini", "test"));
        var router = NewRouter(PlanningMode.Online, online, new StubConsent(CloudConsentDecision.AllowOnce), NoLocal());

        var plan = await router.PlanAsync(Request("open notepad and write hello"));

        Assert.Equal("app.open", plan.Intent);
        Assert.Equal("true", plan.Args["typing_requested"]);
    }

    // ---- Hybrid mode ----

    [Fact]
    public async Task Hybrid_LocalConfident_NoConsentNoCloud()
    {
        var online = new StubOnline(CloudPlan("app.open", "spotify"));
        var consent = new StubConsent(CloudConsentDecision.AllowOnce);
        var local = new StubLocal(LocalPlan("app.open", "vscode", 0.95));
        var router = NewRouter(PlanningMode.Hybrid, online, consent, local);

        var plan = await router.PlanAsync(Request("open editor"));

        Assert.Equal("vscode", plan.Args["app"]);
        Assert.Equal(0, online.CallCount);
        Assert.Equal(0, consent.CallCount);
    }

    [Fact]
    public async Task Hybrid_LocalLowConfidence_AsksConsent_ThenCloud()
    {
        var online = new StubOnline(CloudPlan("app.open", "spotify"));
        var consent = new StubConsent(CloudConsentDecision.AllowOnce);
        var local = new StubLocal(LocalPlan("app.open", "vscode", 0.20)); // below floor
        var router = NewRouter(PlanningMode.Hybrid, online, consent, local,
            options: new LocalAiPlannerOptions { MinimumConfidence = 0.70 });

        var plan = await router.PlanAsync(Request("do the thing"));

        Assert.Equal(1, consent.CallCount);
        Assert.Equal(1, online.CallCount);
        Assert.Equal("spotify", plan.Args["app"]);
    }

    [Fact]
    public async Task Hybrid_LocalFails_ConsentCancel_FallsBackRule()
    {
        var online = new StubOnline(CloudPlan("app.open", "spotify"));
        var consent = new StubConsent(CloudConsentDecision.Cancel);
        var router = NewRouter(PlanningMode.Hybrid, online, consent, NoLocal());

        var plan = await router.PlanAsync(Request("open notepad"));

        Assert.Equal(0, online.CallCount);
        Assert.Equal("hybrid-consent-declined-rule-based", plan.PlanSource);
        Assert.Equal("notepad", plan.Args["app"]);
    }

    [Fact]
    public async Task Hybrid_AsksConsentAtMostOncePerCommand()
    {
        var consent = new StubConsent(CloudConsentDecision.AllowOnce);
        var online = new StubOnline(CloudPlan("app.open", "spotify"));
        var router = NewRouter(PlanningMode.Hybrid, online, consent, NoLocal());

        await router.PlanAsync(Request("open notepad"));

        Assert.Equal(1, consent.CallCount);
    }

    [Fact]
    public async Task RuleBasedMode_NeverCallsLocalOrCloud()
    {
        var online = new StubOnline(CloudPlan("app.open", "x"));
        var consent = new StubConsent(CloudConsentDecision.AllowOnce);
        var local = new StubLocal(LocalPlan("app.open", "vscode", 0.95));
        var router = NewRouter(PlanningMode.RuleBased, online, consent, local);

        var plan = await router.PlanAsync(Request("open notepad"));

        Assert.Equal("rule-based", plan.PlanSource);
        Assert.False(local.WasCalled);
        Assert.Equal(0, online.CallCount);
        Assert.Equal(0, consent.CallCount);
    }

    // ---- helpers ----

    private static CommandRequest Request(string text) => new() { Text = text, Source = "text" };

    private static AiRouterIntentPlanner NewRouter(
        PlanningMode mode,
        IOnlineAiProvider? online,
        ICloudConsentService? consent,
        ILocalIntentPlanner local,
        LocalAiPlannerOptions? options = null)
    {
        var state = new LocalAiPlannerState { Mode = mode, ActiveOnlineProviderId = "gemini" };
        return new AiRouterIntentPlanner(
            new RuleBasedCommandParser(), local, options ?? new LocalAiPlannerOptions(), state, online, consent);
    }

    private static IntentPlan LocalPlanModel(string intent, string app, double confidence) => new()
    {
        Intent = intent,
        Risk = RiskLevel.L1,
        Args = ImmutableDictionary<string, string>.Empty.Add("app", app),
        Confidence = confidence,
        PlanSource = "ollama:test",
    };

    private static LocalAiPlanningResult LocalPlan(string intent, string app, double confidence)
        => LocalAiPlanningResult.Succeeded(LocalPlanModel(intent, app, confidence), "ollama", "test");

    private static OnlineAiPlanningResult CloudPlan(string intent, string app) => OnlineAiPlanningResult.Succeeded(
        new IntentPlan
        {
            Intent = intent,
            Risk = RiskLevel.L1,
            Args = ImmutableDictionary<string, string>.Empty.Add("app", app),
            Confidence = 0.9,
            PlanSource = "gemini:test",
        },
        "gemini",
        "test");

    private static StubLocal NoLocal() => new(LocalAiPlanningResult.Failed("no local", "ollama", "test"));

    private sealed class StubLocal : ILocalIntentPlanner
    {
        private readonly LocalAiPlanningResult _result;
        public StubLocal(LocalAiPlanningResult result) => _result = result;
        public bool WasCalled { get; private set; }

        public Task<LocalAiPlanningResult> PlanAsync(CommandRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(_result);
        }
    }

    private sealed class StubOnline : IOnlineAiProvider
    {
        private readonly OnlineAiPlanningResult? _result;
        private readonly bool _throws;

        public StubOnline(OnlineAiPlanningResult result) { _result = result; }
        public StubOnline(bool throws) { _throws = throws; }

        public int CallCount { get; private set; }
        public string ProviderId => "gemini";

        public Task<OnlineProviderKeyStatus> GetKeyStatusAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(OnlineProviderKeyStatus.Configured("gemini", OnlineProviderKeySource.EnvironmentVariable));

        public Task<OnlineAiPlanningResult> PlanAsync(CommandRequest request, CloudConsentDecision consent, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (_throws)
            {
                throw new InvalidOperationException("simulated cloud crash");
            }
            return Task.FromResult(_result!);
        }
    }

    private sealed class StubConsent : ICloudConsentService
    {
        private readonly CloudConsentDecision _decision;
        public StubConsent(CloudConsentDecision decision) => _decision = decision;
        public int CallCount { get; private set; }

        public Task<CloudConsentDecision> RequestConsentAsync(CloudConsentRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_decision);
        }
    }
}
