using Vayu.Core;

namespace Vayu.AgentRuntime;

/// <summary>
/// In-memory registry of <see cref="IAgent"/> instances. Lookups by
/// intent are O(1); registration is single-shot at app startup.
/// </summary>
/// <remarks>
/// Each intent must be owned by exactly one agent. The registry refuses
/// duplicate agent names (case-insensitive) and duplicate intent claims.
/// Risk-level checks (<see cref="IntentPlan.Risk"/> &lt;= <see cref="IAgent.MaxRisk"/>)
/// happen at dispatch time in <see cref="AgentRuntime"/>; they are not
/// enforced at registration because intents don't carry intrinsic risk.
/// </remarks>
public sealed class AgentRegistry
{
    private readonly Dictionary<string, IAgent> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IAgent> _byIntent = new(StringComparer.Ordinal);

    /// <summary>All registered agents in registration order.</summary>
    public IReadOnlyCollection<IAgent> Agents => _byName.Values;

    /// <summary>Registers <paramref name="agent"/> and indexes it by every intent it claims.</summary>
    public void Register(IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrWhiteSpace(agent.Name);

        if (_byName.ContainsKey(agent.Name))
        {
            throw new InvalidOperationException($"Agent '{agent.Name}' is already registered.");
        }
        if (agent.Intents.Count == 0)
        {
            throw new InvalidOperationException($"Agent '{agent.Name}' declares no intents.");
        }
        foreach (var intent in agent.Intents)
        {
            if (string.IsNullOrWhiteSpace(intent))
            {
                throw new InvalidOperationException($"Agent '{agent.Name}' declares an empty intent name.");
            }
            if (_byIntent.TryGetValue(intent, out var existing))
            {
                throw new InvalidOperationException(
                    $"Intent '{intent}' is already handled by agent '{existing.Name}'.");
            }
        }

        _byName[agent.Name] = agent;
        foreach (var intent in agent.Intents)
        {
            _byIntent[intent] = agent;
        }
    }

    /// <summary>Returns the agent that handles <paramref name="intent"/>, or <see langword="null"/> if none.</summary>
    public IAgent? FindByIntent(string intent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intent);
        return _byIntent.TryGetValue(intent, out var agent) ? agent : null;
    }

    /// <summary>Returns the agent registered under <paramref name="name"/>, or <see langword="null"/> if none.</summary>
    public IAgent? FindByName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _byName.TryGetValue(name, out var agent) ? agent : null;
    }
}
