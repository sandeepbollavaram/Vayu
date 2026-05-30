using System.Collections.Immutable;

namespace Vayu.Core;

/// <summary>
/// A single user-initiated request entering the runtime. Carries the raw
/// command text, where it came from, and bookkeeping fields that flow
/// through the rest of the pipeline.
/// </summary>
/// <remarks>
/// <see cref="CorrelationId"/> ties a request to every downstream log, audit,
/// and AI call. <see cref="Source"/> is a free-form channel tag such as
/// <c>"text"</c>, <c>"voice/whisper"</c>, <c>"hotkey/Ctrl+Win+Space"</c>,
/// or <c>"workflow/morning-startup"</c>.
/// </remarks>
public sealed record CommandRequest
{
    /// <summary>The raw command text as the user entered or spoke it.</summary>
    public required string Text { get; init; }

    /// <summary>The channel the request arrived on. Free-form for now.</summary>
    public required string Source { get; init; }

    /// <summary>When the request was created, in UTC.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Correlates this request with all downstream logs and audit rows.</summary>
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    /// <summary>Optional metadata bag for channel-specific extras (e.g. workflow id).</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        ImmutableDictionary<string, string>.Empty;
}
