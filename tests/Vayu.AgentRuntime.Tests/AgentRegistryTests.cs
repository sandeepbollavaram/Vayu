using Vayu.Core;

namespace Vayu.AgentRuntime.Tests;

public class AgentRegistryTests
{
    [Fact]
    public void Register_AndResolveByIntent()
    {
        var registry = new AgentRegistry();
        var agent = new FakeAgent("AppLauncher", RiskLevel.L1, "app.open", "app.launch");

        registry.Register(agent);

        Assert.Same(agent, registry.FindByIntent("app.open"));
        Assert.Same(agent, registry.FindByIntent("app.launch"));
    }

    [Fact]
    public void Register_AndResolveByName()
    {
        var registry = new AgentRegistry();
        var agent = new FakeAgent("AppLauncher", RiskLevel.L1, "app.open");

        registry.Register(agent);

        Assert.Same(agent, registry.FindByName("AppLauncher"));
        Assert.Same(agent, registry.FindByName("applauncher"));
    }

    [Fact]
    public void Agents_Property_ContainsRegisteredAgents()
    {
        var registry = new AgentRegistry();
        var a = new FakeAgent("A", RiskLevel.L0, "a.x");
        var b = new FakeAgent("B", RiskLevel.L0, "b.x");

        registry.Register(a);
        registry.Register(b);

        Assert.Equal(2, registry.Agents.Count);
        Assert.Contains(a, registry.Agents);
        Assert.Contains(b, registry.Agents);
    }

    [Fact]
    public void Register_Duplicate_AgentName_Throws()
    {
        var registry = new AgentRegistry();
        registry.Register(new FakeAgent("AppLauncher", RiskLevel.L0, "a.x"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new FakeAgent("AppLauncher", RiskLevel.L0, "a.y")));

        Assert.Contains("already registered", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Register_Duplicate_AgentName_IsCaseInsensitive()
    {
        var registry = new AgentRegistry();
        registry.Register(new FakeAgent("AppLauncher", RiskLevel.L0, "a.x"));

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new FakeAgent("APPLAUNCHER", RiskLevel.L0, "a.y")));
    }

    [Fact]
    public void Register_Duplicate_IntentOwnership_Throws()
    {
        var registry = new AgentRegistry();
        registry.Register(new FakeAgent("AppLauncher", RiskLevel.L0, "app.open"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new FakeAgent("Other", RiskLevel.L0, "app.open")));

        Assert.Contains("already handled", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Register_AgentWithoutIntents_Throws()
    {
        var registry = new AgentRegistry();

        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new FakeAgent("Empty", RiskLevel.L0)));
    }

    [Fact]
    public void FindByIntent_UnknownIntent_ReturnsNull()
    {
        var registry = new AgentRegistry();

        Assert.Null(registry.FindByIntent("nope.never"));
    }

    [Fact]
    public void Register_Throws_OnNullAgent()
    {
        var registry = new AgentRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
    }

    private sealed class FakeAgent : IAgent
    {
        public FakeAgent(string name, RiskLevel maxRisk, params string[] intents)
        {
            Name = name;
            MaxRisk = maxRisk;
            Intents = intents;
        }

        public string Name { get; }
        public RiskLevel MaxRisk { get; }
        public IReadOnlyCollection<string> Intents { get; }

        public Task<CommandResult> ExecuteAsync(IntentPlan plan, CancellationToken cancellationToken = default)
            => Task.FromResult(CommandResult.Success(agentName: Name, plan: plan));
    }
}
