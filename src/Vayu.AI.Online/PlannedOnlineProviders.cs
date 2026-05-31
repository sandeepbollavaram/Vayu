using System.Collections.Immutable;

namespace Vayu.AI.Online;

/// <summary>
/// Factory for the M3.7 connector shells. Builds a <see cref="PlannedOnlineProvider"/>
/// for every catalog provider that does not yet have a real connector (i.e.
/// everything except Gemini). The shells make the architecture ready to flip on
/// real connectors one at a time — none of them call out.
/// </summary>
public static class PlannedOnlineProviders
{
    /// <summary>The id of the only provider with a working connector today.</summary>
    public const string AvailableProviderId = "gemini";

    /// <summary>
    /// Provider ids that have a connector shell present in M3.7. These are the
    /// catalog providers most likely to get a real connector first; the rest of
    /// the catalog is still "Planned" without a dedicated shell.
    /// </summary>
    public static IReadOnlyList<string> ShellProviderIds { get; } =
    [
        "openai",
        "claude",
        "deepseek",
        "kimi",
        "openrouter",
        "custom-openai-compatible",
    ];

    /// <summary>Builds a shell for every non-Gemini catalog provider.</summary>
    public static IReadOnlyList<PlannedOnlineProvider> BuildAll()
    {
        var shells = ImmutableArray.CreateBuilder<PlannedOnlineProvider>();
        foreach (var descriptor in OnlineProviderCatalog.ListAll())
        {
            if (!string.Equals(descriptor.ProviderId, AvailableProviderId, StringComparison.OrdinalIgnoreCase))
            {
                shells.Add(new PlannedOnlineProvider(descriptor));
            }
        }
        return shells.ToImmutable();
    }

    /// <summary>True when <paramref name="providerId"/> has a dedicated connector shell in M3.7.</summary>
    public static bool HasShell(string providerId)
        => !string.IsNullOrWhiteSpace(providerId)
           && ShellProviderIds.Contains(providerId.Trim(), StringComparer.OrdinalIgnoreCase);
}
