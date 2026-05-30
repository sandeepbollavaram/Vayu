using Serilog;
using Serilog.Events;

namespace Vayu.Logging;

/// <summary>
/// Builds Vayu's <see cref="Serilog.ILogger"/> from a
/// <see cref="LoggingOptions"/> snapshot. Wires the redaction enricher,
/// the in-memory ring buffer (so the Logs page can subscribe), and a
/// rolling daily file sink under the configured log directory.
/// </summary>
public static class VayuLogger
{
    /// <summary>
    /// Creates a configured <see cref="Serilog.ILogger"/>. The caller
    /// owns disposal of the returned logger and of <paramref name="inMemorySink"/>.
    /// </summary>
    /// <param name="options">Logging configuration; environment variables in <see cref="LoggingOptions.Directory"/> are expanded.</param>
    /// <param name="inMemorySink">The ring buffer the Logs page binds to. Pre-constructed so the same instance is shared between the logger and the UI.</param>
    public static ILogger Create(LoggingOptions options, InMemoryLogSink inMemorySink)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(inMemorySink);

        var directory = Environment.ExpandEnvironmentVariables(options.Directory);
        System.IO.Directory.CreateDirectory(directory);
        var filePath = System.IO.Path.Combine(directory, options.FileNamePrefix);
        var level = ResolveLevel(options);

        return new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .Enrich.With(new RedactingLogEnricher())
            .WriteTo.Sink(inMemorySink)
            .WriteTo.File(
                path: filePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: Math.Max(1, options.RetainDays),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}",
                shared: false)
            .CreateLogger();
    }

    private static LogEventLevel ResolveLevel(LoggingOptions options)
    {
        if (options.Verbose)
        {
            return LogEventLevel.Debug;
        }
        return Enum.TryParse<LogEventLevel>(options.Level, ignoreCase: true, out var level)
            ? level
            : LogEventLevel.Information;
    }
}
