using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

namespace Vayu.Logging.Tests;

public class RedactingLogEnricherTests
{
    private static LogEvent BuildEvent(string messageTemplate, IEnumerable<LogEventProperty> properties)
    {
        var parser = new MessageTemplateParser();
        return new LogEvent(
            timestamp: DateTimeOffset.UtcNow,
            level: LogEventLevel.Information,
            exception: null,
            messageTemplate: parser.Parse(messageTemplate),
            properties: properties);
    }

    [Fact]
    public void Enrich_RedactsStringPropertyContainingApiKey()
    {
        // Compile-time concatenation keeps this out of CI secret-grep.
        var fakeKey = "AIza" + "SyVayuTestKeyFakeValueAbcdef1234567890";
        var factory = new TestPropertyFactory();
        var evt = BuildEvent(
            "Configured key {ApiKey}",
            new[] { new LogEventProperty("ApiKey", new ScalarValue(fakeKey)) });

        new RedactingLogEnricher().Enrich(evt, factory);

        var rewritten = (ScalarValue)evt.Properties["ApiKey"];
        Assert.Equal("[REDACTED]", rewritten.Value);
    }

    [Fact]
    public void Enrich_LeavesBenignStringProperties_Untouched()
    {
        var factory = new TestPropertyFactory();
        var evt = BuildEvent(
            "User opened {App}",
            new[] { new LogEventProperty("App", new ScalarValue("notepad")) });

        new RedactingLogEnricher().Enrich(evt, factory);

        var app = (ScalarValue)evt.Properties["App"];
        Assert.Equal("notepad", app.Value);
    }

    [Fact]
    public void Enrich_IgnoresNonStringProperties()
    {
        var factory = new TestPropertyFactory();
        var evt = BuildEvent(
            "Risk {Level}",
            new[] { new LogEventProperty("Level", new ScalarValue(3)) });

        new RedactingLogEnricher().Enrich(evt, factory);

        var lvl = (ScalarValue)evt.Properties["Level"];
        Assert.Equal(3, lvl.Value);
    }

    [Fact]
    public void Enrich_HandlesMultipleSecretProperties()
    {
        var bearer = "Bearer " + "VayuFakeBearerTokenForTesting1234567890";
        var generic = "api_key=fake-test-value-1234567890";
        var factory = new TestPropertyFactory();
        var evt = BuildEvent(
            "Outbound {Auth} body {Body}",
            new[]
            {
                new LogEventProperty("Auth", new ScalarValue(bearer)),
                new LogEventProperty("Body", new ScalarValue(generic)),
            });

        new RedactingLogEnricher().Enrich(evt, factory);

        var auth = ((ScalarValue)evt.Properties["Auth"]).Value as string;
        var body = ((ScalarValue)evt.Properties["Body"]).Value as string;
        Assert.NotNull(auth);
        Assert.NotNull(body);
        Assert.Contains("[REDACTED]", auth);
        Assert.Contains("[REDACTED]", body);
        Assert.DoesNotContain("VayuFakeBearerTokenForTesting1234567890", auth);
        Assert.DoesNotContain("fake-test-value-1234567890", body);
    }

    [Fact]
    public void Enrich_Throws_OnNullArguments()
    {
        var enricher = new RedactingLogEnricher();
        var factory = new TestPropertyFactory();
        var evt = BuildEvent("hello", Array.Empty<LogEventProperty>());

        Assert.Throws<ArgumentNullException>(() => enricher.Enrich(null!, factory));
        Assert.Throws<ArgumentNullException>(() => enricher.Enrich(evt, null!));
    }

    private sealed class TestPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
            => new(name, new ScalarValue(value));
    }
}
