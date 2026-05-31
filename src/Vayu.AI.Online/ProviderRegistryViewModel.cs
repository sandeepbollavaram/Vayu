using System.Collections.Immutable;

namespace Vayu.AI.Online;

/// <summary>
/// Builds the Settings "Online Provider Registry" cards from
/// <see cref="OnlineProviderCatalog"/>. Pure (no UI, no I/O, no secrets) so it
/// is unit-testable. Exactly one provider — Gemini — is "available now"; the
/// rest are clearly marked "Planned" so the UI never implies an unsupported
/// provider works.
/// </summary>
public static class ProviderRegistryViewModel
{
    /// <summary>The id of the only provider with a working connector today.</summary>
    public const string AvailableProviderId = "gemini";

    /// <summary>
    /// Projects every catalog entry into a <see cref="ProviderCardViewModel"/>.
    /// </summary>
    /// <param name="geminiKeyConfigured">Whether a Gemini key is configured (drives Gemini's status/action text).</param>
    public static IReadOnlyList<ProviderCardViewModel> BuildCards(bool geminiKeyConfigured)
    {
        var cards = ImmutableArray.CreateBuilder<ProviderCardViewModel>();
        foreach (var descriptor in OnlineProviderCatalog.ListAll())
        {
            cards.Add(BuildCard(descriptor, geminiKeyConfigured));
        }
        return cards.ToImmutable();
    }

    private static ProviderCardViewModel BuildCard(OnlineProviderDescriptor d, bool geminiKeyConfigured)
    {
        var isAvailable = string.Equals(d.ProviderId, AvailableProviderId, StringComparison.OrdinalIgnoreCase);
        var capabilities = BuildCapabilityLabels(d);

        if (isAvailable)
        {
            var configured = geminiKeyConfigured;
            return new ProviderCardViewModel(
                ProviderId: d.ProviderId,
                DisplayName: d.DisplayName,
                KindLabel: KindLabel(d.Kind),
                CapabilityLabels: capabilities,
                StatusLabel: configured ? "Available now · Configured" : "Available now · Not configured",
                IsAvailableNow: true,
                IsConfigured: configured,
                ActionLabel: configured ? "Configured — manage below" : "Set up below",
                IsActionEnabled: false); // setup happens in the dedicated Gemini key card
        }

        // M3.7: providers with a connector shell are still "Planned" (not working),
        // but we surface that the shell is present so the multi-provider story is honest.
        var hasShell = PlannedOnlineProviders.HasShell(d.ProviderId);
        return new ProviderCardViewModel(
            ProviderId: d.ProviderId,
            DisplayName: d.DisplayName,
            KindLabel: KindLabel(d.Kind),
            CapabilityLabels: capabilities,
            StatusLabel: hasShell ? "Planned · connector shell present" : "Planned",
            IsAvailableNow: false,
            IsConfigured: false,
            ActionLabel: hasShell ? "Connector shell present — implementation later" : "Planned",
            IsActionEnabled: false);
    }

    private static IReadOnlyList<string> BuildCapabilityLabels(OnlineProviderDescriptor d)
    {
        var labels = ImmutableArray.CreateBuilder<string>();
        if (d.SupportsChat)
        {
            labels.Add("Chat");
        }
        if (d.SupportsJsonMode)
        {
            labels.Add("JSON mode");
        }
        if (d.SupportsStreaming)
        {
            labels.Add("Streaming");
        }
        return labels.ToImmutable();
    }

    /// <summary>Human-readable label for a provider kind.</summary>
    public static string KindLabel(OnlineProviderKind kind) => kind switch
    {
        OnlineProviderKind.DirectApi => "Direct API",
        OnlineProviderKind.Router => "Router",
        OnlineProviderKind.CloudPlatform => "Cloud Platform",
        OnlineProviderKind.CustomOpenAiCompatible => "Custom OpenAI-compatible",
        _ => "Unknown",
    };
}
