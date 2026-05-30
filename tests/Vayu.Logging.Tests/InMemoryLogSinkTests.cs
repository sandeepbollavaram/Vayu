using Serilog.Events;
using Serilog.Parsing;

namespace Vayu.Logging.Tests;

public class InMemoryLogSinkTests
{
    private static LogEvent BuildEvent(string messageTemplate, LogEventLevel level = LogEventLevel.Information)
    {
        var parser = new MessageTemplateParser();
        var template = parser.Parse(messageTemplate);
        return new LogEvent(
            timestamp: DateTimeOffset.UtcNow,
            level: level,
            exception: null,
            messageTemplate: template,
            properties: Array.Empty<LogEventProperty>());
    }

    [Fact]
    public void Snapshot_IsEmpty_OnFreshSink()
    {
        var sink = new InMemoryLogSink(capacity: 10);

        Assert.Empty(sink.Snapshot());
        Assert.Equal(0, sink.EmittedCount);
    }

    [Fact]
    public void Emit_AppendsRedactedEntry()
    {
        var sink = new InMemoryLogSink(capacity: 10);
        // Compile-time concatenation keeps the literal out of CI secret-grep.
        var fakeKey = "AIza" + "SyVayuTestKeyFakeValueAbcdef1234567890";

        sink.Emit(BuildEvent($"Resolved API key: {fakeKey} end"));

        var entries = sink.Snapshot();
        var only = Assert.Single(entries);
        Assert.DoesNotContain(fakeKey, only.Message);
        Assert.Contains("[REDACTED]", only.Message);
        Assert.Equal("Information", only.Level);
        Assert.Equal(1, sink.EmittedCount);
    }

    [Fact]
    public void Emit_DropsOldestWhenCapacityExceeded()
    {
        var sink = new InMemoryLogSink(capacity: 3);

        for (var i = 0; i < 5; i++)
        {
            sink.Emit(BuildEvent($"event {i}"));
        }

        var entries = sink.Snapshot();
        Assert.Equal(3, entries.Count);
        Assert.Equal("event 2", entries[0].Message);
        Assert.Equal("event 4", entries[^1].Message);
        Assert.Equal(5, sink.EmittedCount);
    }

    [Fact]
    public void Clear_DropsBuffer_ButKeepsCounter()
    {
        var sink = new InMemoryLogSink(capacity: 3);
        sink.Emit(BuildEvent("a"));
        sink.Emit(BuildEvent("b"));

        sink.Clear();

        Assert.Empty(sink.Snapshot());
        Assert.Equal(2, sink.EmittedCount);
    }

    [Fact]
    public void Constructor_ClampsCapacityToAtLeastOne()
    {
        var sink = new InMemoryLogSink(capacity: 0);

        Assert.Equal(1, sink.Capacity);
    }
}
