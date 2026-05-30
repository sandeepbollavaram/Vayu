using Serilog.Core;
using Serilog.Events;

using Vayu.Security;

namespace Vayu.Logging;

/// <summary>
/// One row that the in-memory ring buffer keeps for the Logs page. All
/// values are already redacted; this record carries no secrets.
/// </summary>
/// <param name="TimestampUtc">When the event was emitted.</param>
/// <param name="Level">Serilog level as a string (<c>Information</c>, <c>Warning</c>, etc.).</param>
/// <param name="Message">Rendered, redacted message text.</param>
/// <param name="ExceptionType">The exception type name, if any; the message itself is not stored to avoid leaking exception text.</param>
public sealed record RedactedLogEntry(
    DateTimeOffset TimestampUtc,
    string Level,
    string Message,
    string? ExceptionType);

/// <summary>
/// Bounded ring buffer that the WinUI Logs page binds to. Every event is
/// rendered, then run through <see cref="SecretRedactor"/> before being
/// stored — so a snapshot is always safe to display.
/// </summary>
public sealed class InMemoryLogSink : ILogEventSink
{
    private readonly int _capacity;
    private readonly Queue<RedactedLogEntry> _buffer;
    private readonly Lock _gate = new();

    /// <summary>The maximum number of entries the buffer holds. Constructor argument is clamped to <c>&gt;= 1</c>.</summary>
    public int Capacity => _capacity;

    /// <summary>Total number of events emitted to this sink (including ones that aged out of the buffer).</summary>
    public long EmittedCount { get; private set; }

    public InMemoryLogSink(int capacity = 1000)
    {
        _capacity = Math.Max(1, capacity);
        _buffer = new Queue<RedactedLogEntry>(_capacity);
    }

    /// <inheritdoc />
    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var rendered = logEvent.RenderMessage();
        var redacted = SecretRedactor.Redact(rendered);
        var entry = new RedactedLogEntry(
            TimestampUtc: logEvent.Timestamp.UtcDateTime,
            Level: logEvent.Level.ToString(),
            Message: redacted,
            ExceptionType: logEvent.Exception?.GetType().FullName);

        lock (_gate)
        {
            if (_buffer.Count >= _capacity)
            {
                _buffer.Dequeue();
            }
            _buffer.Enqueue(entry);
            EmittedCount++;
        }
    }

    /// <summary>Returns a point-in-time copy of the buffer, oldest first.</summary>
    public IReadOnlyList<RedactedLogEntry> Snapshot()
    {
        lock (_gate)
        {
            return _buffer.ToArray();
        }
    }

    /// <summary>Empties the ring buffer. Does not reset <see cref="EmittedCount"/>.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _buffer.Clear();
        }
    }
}
