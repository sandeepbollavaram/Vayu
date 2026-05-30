using Vayu.Core;
using Vayu.Memory;
using Vayu.Permissions;

namespace Vayu.AgentRuntime.Tests;

public class AgentRuntimeTests
{
    [Fact]
    public async Task Dispatch_OpenNotepad_RoutesToAgent_AndReturnsSuccess()
    {
        var harness = new Harness().WithAppLauncher();

        var result = await harness.Runtime.DispatchAsync(Req("open notepad"));

        Assert.Equal(CommandStatus.Success, result.Status);
        Assert.Equal("AppLauncher", result.AgentName);
        Assert.Equal(1, harness.AppLauncher!.CallCount);
        Assert.Equal("notepad", harness.AppLauncher.LastPlan!.Args["app"]);
    }

    [Fact]
    public async Task Dispatch_UnknownCommand_ReturnsNeedsClarification()
    {
        var harness = new Harness().WithAppLauncher();

        var result = await harness.Runtime.DispatchAsync(Req("dance for me"));

        Assert.Equal(CommandStatus.NeedsClarification, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.ClarificationPrompt));
        Assert.Equal(0, harness.AppLauncher!.CallCount);
    }

    [Fact]
    public async Task Dispatch_NoAgentForIntent_ReturnsFailed()
    {
        // Registry has no app.open agent.
        var harness = new Harness();

        var result = await harness.Runtime.DispatchAsync(Req("open notepad"));

        Assert.Equal(CommandStatus.Failed, result.Status);
        Assert.Equal("NO_AGENT_FOR_INTENT", result.ErrorCode);
    }

    [Fact]
    public async Task Dispatch_PermissionDenied_ReturnsCancelled_AndDoesNotExecute()
    {
        var harness = Harness.WithRiskyPlanner(
            promptOutcome: PermissionDecision.Denied,
            agentMaxRisk: RiskLevel.L5);

        var result = await harness.Runtime.DispatchAsync(Req("trigger risky"));

        Assert.Equal(CommandStatus.Cancelled, result.Status);
        Assert.Equal(0, harness.RiskyAgent!.CallCount);
    }

    [Fact]
    public async Task Dispatch_PermissionCancelled_ReturnsCancelled()
    {
        var harness = Harness.WithRiskyPlanner(
            promptOutcome: PermissionDecision.Cancelled,
            agentMaxRisk: RiskLevel.L5);

        var result = await harness.Runtime.DispatchAsync(Req("trigger risky"));

        Assert.Equal(CommandStatus.Cancelled, result.Status);
        Assert.Equal(0, harness.RiskyAgent!.CallCount);
    }

    [Fact]
    public async Task Dispatch_AgentThrows_ReturnsFailed_WithRedactedMessage()
    {
        var harness = new Harness().WithAppLauncher();
        // Compile-time concatenation keeps this out of CI secret-grep.
        var fakeKey = "AIza" + "SyVayuTestKeyFakeValueAbcdef1234567890";
        harness.AppLauncher!.ThrowOnExecute = new InvalidOperationException(
            $"failed with key {fakeKey} leaking");

        var result = await harness.Runtime.DispatchAsync(Req("open notepad"));

        Assert.Equal(CommandStatus.Failed, result.Status);
        Assert.Equal("InvalidOperationException", result.ErrorCode);
        Assert.NotNull(result.Message);
        Assert.DoesNotContain(fakeKey, result.Message);
        Assert.Contains("[REDACTED]", result.Message);
    }

    [Fact]
    public async Task Dispatch_PlanRiskAboveAgentMax_ReturnsFailed()
    {
        var harness = new Harness().WithAppLauncher(maxRisk: RiskLevel.L0); // launcher caps at L0

        var result = await harness.Runtime.DispatchAsync(Req("open notepad")); // app.open is L1

        Assert.Equal(CommandStatus.Failed, result.Status);
        Assert.Equal("AGENT_RISK_EXCEEDED", result.ErrorCode);
    }

    [Fact]
    public async Task Dispatch_SuccessfulRun_WritesExecutionAuditRow()
    {
        var harness = new Harness().WithAppLauncher();

        await harness.Runtime.DispatchAsync(Req("open notepad"));

        // DefaultPermissionService writes one row at permission step;
        // AgentRuntime writes one row at execution step.
        Assert.Equal(2, harness.Audit.Entries.Count);
        var execution = harness.Audit.Entries[^1];
        Assert.Equal("AppLauncher", execution.AgentName);
        Assert.Equal(CommandStatus.Success, execution.Status);
        Assert.Equal(PermissionDecision.Allowed, execution.PermissionDecision);
    }

    [Fact]
    public async Task Dispatch_CommandText_IsRedacted_BeforeAudit()
    {
        var harness = new Harness().WithAppLauncher();
        var fakeKey = "AIza" + "SyVayuTestKeyFakeValueAbcdef1234567890";

        await harness.Runtime.DispatchAsync(Req($"open notepad {fakeKey}"));

        Assert.All(harness.Audit.Entries, e =>
        {
            Assert.NotNull(e.CommandText);
            Assert.DoesNotContain(fakeKey, e.CommandText);
        });
    }

    [Fact]
    public async Task Dispatch_CorrelationId_FlowsToBothAuditRows()
    {
        var harness = new Harness().WithAppLauncher();
        var corr = Guid.NewGuid();
        var req = new CommandRequest { Text = "open notepad", Source = "text", CorrelationId = corr };

        await harness.Runtime.DispatchAsync(req);

        Assert.Equal(2, harness.Audit.Entries.Count);
        Assert.All(harness.Audit.Entries, e =>
        {
            // The parser uses the request's CorrelationId for the plan.
            Assert.Equal(corr, e.CorrelationId);
        });
    }

    [Fact]
    public async Task Dispatch_CancellationToken_PropagatesFromAgent()
    {
        var harness = new Harness().WithAppLauncher();
        harness.AppLauncher!.RespectCancellation = true;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => harness.Runtime.DispatchAsync(Req("open notepad"), cts.Token));
    }

    [Fact]
    public void Constructor_Throws_OnNullArgs()
    {
        var parser = new RuleBasedCommandParser();
        var registry = new AgentRegistry();
        var planner = new IntentPlanner(parser);
        var permissions = new NoopPermissionService();
        var clock = new FakeClock();

        Assert.Throws<ArgumentNullException>(() =>
            new AgentRuntime(null!, registry, permissions, clock));
        Assert.Throws<ArgumentNullException>(() =>
            new AgentRuntime(planner, null!, permissions, clock));
        Assert.Throws<ArgumentNullException>(() =>
            new AgentRuntime(planner, registry, null!, clock));
        Assert.Throws<ArgumentNullException>(() =>
            new AgentRuntime(planner, registry, permissions, null!));
    }

    [Fact]
    public async Task Dispatch_Throws_OnNullRequest()
    {
        var harness = new Harness().WithAppLauncher();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => harness.Runtime.DispatchAsync(null!));
    }

    private static CommandRequest Req(string text) =>
        new() { Text = text, Source = "text" };

    private sealed class Harness
    {
        public AgentRegistry Registry { get; } = new();
        public FakeAuditLog Audit { get; } = new();
        public FakeClock Clock { get; } = new();
        public AgentRuntime Runtime { get; }
        public FakeLauncher? AppLauncher { get; private set; }
        public FakeLauncher? RiskyAgent { get; private set; }

        public Harness(
            PermissionDecision promptOutcome = PermissionDecision.Allowed,
            IIntentPlanner? planner = null)
        {
            var resolvedPlanner = planner ?? new IntentPlanner(new RuleBasedCommandParser());
            var permissions = new DefaultPermissionService(
                PermissionPolicy.Default,
                new FakeConfirmationService(promptOutcome),
                Clock,
                Audit);
            Runtime = new AgentRuntime(resolvedPlanner, Registry, permissions, Clock, Audit);
        }

        public Harness WithAppLauncher(RiskLevel maxRisk = RiskLevel.L1)
        {
            AppLauncher = new FakeLauncher("AppLauncher", maxRisk, RuleBasedCommandParser.AppOpenIntent);
            Registry.Register(AppLauncher);
            return this;
        }

        /// <summary>
        /// Builds a harness whose planner always emits a high-risk plan
        /// (<c>danger.do</c>, <see cref="RiskLevel.L4"/>). Used by tests
        /// that need to exercise the permission-engine denial path.
        /// </summary>
        public static Harness WithRiskyPlanner(PermissionDecision promptOutcome, RiskLevel agentMaxRisk)
        {
            var harness = new Harness(promptOutcome, new ConstantPlanner("danger.do", RiskLevel.L4));
            harness.RiskyAgent = new FakeLauncher("Risky", agentMaxRisk, "danger.do");
            harness.Registry.Register(harness.RiskyAgent);
            return harness;
        }
    }

    private sealed class ConstantPlanner : IIntentPlanner
    {
        private readonly string _intent;
        private readonly RiskLevel _risk;

        public ConstantPlanner(string intent, RiskLevel risk)
        {
            _intent = intent;
            _risk = risk;
        }

        public Task<IntentPlan> PlanAsync(CommandRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            return Task.FromResult(new IntentPlan
            {
                Intent = _intent,
                Risk = _risk,
                CorrelationId = request.CorrelationId,
                PlanSource = "test-constant",
            });
        }
    }

    private sealed class FakeLauncher : IAgent
    {
        public FakeLauncher(string name, RiskLevel maxRisk, params string[] intents)
        {
            Name = name;
            MaxRisk = maxRisk;
            Intents = intents;
        }

        public string Name { get; }
        public RiskLevel MaxRisk { get; }
        public IReadOnlyCollection<string> Intents { get; }
        public int CallCount { get; private set; }
        public IntentPlan? LastPlan { get; private set; }
        public Exception? ThrowOnExecute { get; set; }
        public bool RespectCancellation { get; set; }

        public Task<CommandResult> ExecuteAsync(IntentPlan plan, CancellationToken cancellationToken = default)
        {
            if (RespectCancellation)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            CallCount++;
            LastPlan = plan;
            if (ThrowOnExecute is not null)
            {
                throw ThrowOnExecute;
            }
            return Task.FromResult(CommandResult.Success(agentName: Name, plan: plan));
        }
    }

    private sealed class FakeAuditLog : IAuditLogService
    {
        public List<ActionLogEntry> Entries { get; } = new();

        public Task<ActionLogEntry> AppendAsync(ActionLogEntry entry, CancellationToken cancellationToken = default)
        {
            var stored = entry with { Id = Entries.Count + 1 };
            Entries.Add(stored);
            return Task.FromResult(stored);
        }

        public Task<IReadOnlyList<ActionLogEntry>> ListRecentAsync(int limit, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ActionLogEntry> view = Entries
                .OrderByDescending(e => e.Id)
                .Take(limit)
                .ToArray();
            return Task.FromResult(view);
        }

        public Task<ActionLogEntry?> FindByCorrelationAsync(Guid correlationId, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries.FirstOrDefault(e => e.CorrelationId == correlationId));
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed class NoopPermissionService : IPermissionService
    {
        public Task<PermissionResult> EvaluateAsync(PermissionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new PermissionResult
            {
                Decision = PermissionDecision.Allowed,
                Reason = "noop",
                Plan = request.Plan,
            });
    }
}
