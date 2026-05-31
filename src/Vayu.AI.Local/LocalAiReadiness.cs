namespace Vayu.AI.Local;

/// <summary>
/// Pure helpers (no UI, no I/O) deciding whether offline AI planning is
/// allowed to be switched on, and which installed curated model the planner
/// should use. M2.8 uses these to gate the Settings toggle and to pick the
/// active model without ever auto-pulling.
/// </summary>
public static class LocalAiReadiness
{
    /// <summary>
    /// Chooses the model the offline planner should run, given the set of
    /// installed tags. Preference: the curated recommended model first, then
    /// the remaining curated models in catalog order
    /// (<c>gemma3:1b</c>, then <c>llama3.2:3b</c>). Returns <see langword="null"/>
    /// when no curated model is installed — the caller must then keep the
    /// planner disabled rather than pull anything.
    /// </summary>
    /// <param name="installedTags">Tags reported by Ollama's <c>/api/tags</c>.</param>
    public static string? SelectActiveModel(IEnumerable<string>? installedTags)
    {
        if (installedTags is null)
        {
            return null;
        }

        var installed = new HashSet<string>(
            installedTags.Where(t => !string.IsNullOrWhiteSpace(t)),
            StringComparer.OrdinalIgnoreCase);
        if (installed.Count == 0)
        {
            return null;
        }

        // Recommended wins when present.
        var recommended = LocalModelCatalog.Recommended.ModelTag;
        if (installed.Contains(recommended))
        {
            return recommended;
        }

        // Otherwise the first curated model (in catalog order) that is installed.
        foreach (var descriptor in LocalModelCatalog.ListAll())
        {
            if (installed.Contains(descriptor.ModelTag))
            {
                return descriptor.ModelTag;
            }
        }

        return null;
    }

    /// <summary>
    /// True when the offline planner toggle may be turned on: Ollama must be
    /// reachable AND at least one curated model installed.
    /// </summary>
    public static bool CanEnablePlanner(bool endpointReachable, string? activeModel)
        => endpointReachable && !string.IsNullOrEmpty(activeModel);

    /// <summary>
    /// A short, user-facing reason describing the current readiness state.
    /// Used as the toggle's hint text.
    /// </summary>
    public static string DescribeReadiness(bool endpointReachable, string? activeModel)
    {
        if (!endpointReachable)
        {
            return "Install and start Ollama first, then Refresh.";
        }
        if (string.IsNullOrEmpty(activeModel))
        {
            return "Download Gemma 3 4B (or another curated model) first — use Setup → Model catalog.";
        }
        return $"Offline planner is ready. Active model: {activeModel}.";
    }
}
