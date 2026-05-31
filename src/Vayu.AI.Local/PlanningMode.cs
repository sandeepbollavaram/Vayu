namespace Vayu.AI.Local;

/// <summary>
/// How the AI Router plans a command. The default is the safe deterministic
/// path; the AI modes are opt-in.
/// </summary>
public enum PlanningMode
{
    /// <summary>Deterministic rule-based parser only. No model is contacted. The safe default.</summary>
    RuleBased = 0,

    /// <summary>Local Ollama planner first, rule-based fallback. No cloud.</summary>
    Offline = 1,

    /// <summary>Cloud provider (e.g. Gemini) plans, behind per-call consent; rule-based fallback. No local model.</summary>
    Online = 2,

    /// <summary>Local first; on low confidence or failure, ask consent and try the cloud provider; rule-based fallback.</summary>
    Hybrid = 3,
}
