using System.Collections.Immutable;

using Vayu.AgentRuntime;
using Vayu.AI.Local;
using Vayu.Core;

namespace Vayu.AgentRuntime.Tests;

public class AiRouterIntentPlannerTests
{
    [Fact]
    public async Task Router_OfflineDisabled_UsesRuleBasedParser()
    {
        var local = new StubLocalPlanner(/* should never be called */ throwIfCalled: true);
        var router = new AiRouterIntentPlanner(
            new RuleBasedCommandParser(),
            local,
            new LocalAiPlannerOptions { EnableOfflinePlanning = false });

        var plan = await router.PlanAsync(Request("open notepad"));

        Assert.Equal("app.open", plan.Intent);
        Assert.Equal("rule-based", plan.PlanSource);
        Assert.False(local.WasCalled);
    }

    [Fact]
    public async Task Router_OfflineEnabled_UsesLocalAi_WhenItSucceeds()
    {
        var aiPlan = new IntentPlan
        {
            Intent = "app.open",
            Risk = RiskLevel.L1,
            Args = ImmutableDictionary<string, string>.Empty.Add("app", "spotify"),
            Confidence = 0.95,
            PlanSource = "ollama:gemma3:1b",
        };
        var local = new StubLocalPlanner(LocalAiPlanningResult.Succeeded(aiPlan, "ollama", "gemma3:1b"));
        var router = new AiRouterIntentPlanner(
            new RuleBasedCommandParser(),
            local,
            new LocalAiPlannerOptions { EnableOfflinePlanning = true });

        var plan = await router.PlanAsync(Request("launch the music app"));

        Assert.Equal("app.open", plan.Intent);
        Assert.Equal("spotify", plan.Args["app"]);
        Assert.Equal("ollama:gemma3:1b", plan.PlanSource);
        Assert.True(local.WasCalled);
    }

    [Fact]
    public async Task Router_OfflineEnabled_FallsBack_WhenLocalAiFails()
    {
        var local = new StubLocalPlanner(LocalAiPlanningResult.Failed("model offline", "ollama", "gemma3:1b"));
        var router = new AiRouterIntentPlanner(
            new RuleBasedCommandParser(),
            local,
            new LocalAiPlannerOptions { EnableOfflinePlanning = true });

        var plan = await router.PlanAsync(Request("open notepad"));

        // Falls back to rule-based, and the source records that it was a fallback.
        Assert.Equal("app.open", plan.Intent);
        Assert.Equal("notepad", plan.Args["app"]);
        Assert.Equal("ollama-fallback-rule-based", plan.PlanSource);
    }

    [Fact]
    public async Task Router_OfflineEnabled_FallsBack_WhenLocalAiThrows()
    {
        var local = new StubLocalPlanner(throwGeneric: true);
        var router = new AiRouterIntentPlanner(
            new RuleBasedCommandParser(),
            local,
            new LocalAiPlannerOptions { EnableOfflinePlanning = true });

        var plan = await router.PlanAsync(Request("show logs"));

        Assert.Equal("ui.show_logs", plan.Intent);
        Assert.Equal("ollama-fallback-rule-based", plan.PlanSource);
    }

    [Fact]
    public async Task Router_PropagatesCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var router = new AiRouterIntentPlanner(
            new RuleBasedCommandParser(),
            new StubLocalPlanner(),
            new LocalAiPlannerOptions { EnableOfflinePlanning = true });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => router.PlanAsync(Request("open notepad"), cts.Token));
    }

    [Fact]
    public void Constructor_Throws_OnNullArgs()
    {
        Assert.Throws<ArgumentNullException>(() => new AiRouterIntentPlanner(null!, new StubLocalPlanner()));
        Assert.Throws<ArgumentNullException>(() => new AiRouterIntentPlanner(new RuleBasedCommandParser(), null!));
    }

    private static CommandRequest Request(string text) => new() { Text = text, Source = "text" };

    private sealed class StubLocalPlanner : ILocalIntentPlanner
    {
        private readonly LocalAiPlanningResult? _result;
        private readonly bool _throwIfCalled;
        private readonly bool _throwGeneric;

        public StubLocalPlanner(LocalAiPlanningResult? result = null, bool throwIfCalled = false, bool throwGeneric = false)
        {
            _result = result;
            _throwIfCalled = throwIfCalled;
            _throwGeneric = throwGeneric;
        }

        public bool WasCalled { get; private set; }

        public Task<LocalAiPlanningResult> PlanAsync(CommandRequest request, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            cancellationToken.ThrowIfCancellationRequested();
            if (_throwIfCalled)
            {
                throw new InvalidOperationException("Local planner should not have been called.");
            }
            if (_throwGeneric)
            {
                throw new InvalidOperationException("simulated model crash");
            }
            return Task.FromResult(_result ?? LocalAiPlanningResult.Failed("no result", "ollama", "gemma3:1b"));
        }
    }
}
