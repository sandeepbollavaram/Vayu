using System.Collections.Concurrent;

namespace Vayu.Voice;

/// <summary>
/// Keeps the most recent <see cref="VoiceEvent"/>s in a bounded in-memory ring
/// so the UI (Vayu Sphere / activity feed) can render voice state. Stores
/// events only — never raw audio.
/// </summary>
public sealed class InMemoryVoiceActivitySink : IVoiceActivitySink
{
    private readonly int _capacity;
    private readonly ConcurrentQueue<VoiceEvent> _events = new();

    public InMemoryVoiceActivitySink(int capacity = 50)
    {
        _capacity = capacity < 1 ? 1 : capacity;
    }

    /// <summary>The most recently published event, or <see langword="null"/> if none.</summary>
    public VoiceEvent? Latest { get; private set; }

    /// <inheritdoc />
    public Task PublishAsync(VoiceEvent voiceEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voiceEvent);
        Latest = voiceEvent;
        _events.Enqueue(voiceEvent);
        while (_events.Count > _capacity && _events.TryDequeue(out _))
        {
            // Trim to capacity.
        }
        return Task.CompletedTask;
    }

    /// <summary>Snapshot of buffered events, oldest first.</summary>
    public IReadOnlyList<VoiceEvent> Snapshot() => _events.ToArray();
}
