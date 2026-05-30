using Serilog.Core;
using Serilog.Events;

using Vayu.Security;

namespace Vayu.Logging;

/// <summary>
/// Serilog enricher that runs every string-valued log property through
/// <see cref="SecretRedactor"/> before any sink sees it. Combined with
/// <see cref="InMemoryLogSink"/>'s rendered-message redaction, this is the
/// last line of defence against accidentally leaking an API key into logs.
/// </summary>
/// <remarks>
/// Limitation: an enricher can only modify property values; it cannot
/// rewrite the message template itself. Callers MUST use structured
/// logging — pass secrets as properties, not as inline string
/// interpolation — for the enricher to be effective on disk sinks.
/// The <see cref="InMemoryLogSink"/> applies a second redaction pass to
/// the rendered output to catch interpolated values for the Logs page.
/// </remarks>
public sealed class RedactingLogEnricher : ILogEventEnricher
{
    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        List<KeyValuePair<string, string>>? replacements = null;

        foreach (var pair in logEvent.Properties)
        {
            if (pair.Value is not ScalarValue scalar)
            {
                continue;
            }
            if (scalar.Value is not string s)
            {
                continue;
            }

            var redacted = SecretRedactor.Redact(s);
            if (!ReferenceEquals(redacted, s) && !string.Equals(redacted, s, StringComparison.Ordinal))
            {
                replacements ??= new List<KeyValuePair<string, string>>();
                replacements.Add(new KeyValuePair<string, string>(pair.Key, redacted));
            }
        }

        if (replacements is null)
        {
            return;
        }

        foreach (var (key, redacted) in replacements)
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(key, redacted));
        }
    }
}
