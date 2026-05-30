namespace Vayu.Logging;

/// <summary>
/// Configuration for Vayu's logging pipeline. Mirrors the
/// <c>logging</c> section of <c>appsettings.sample.json</c>.
/// </summary>
/// <remarks>
/// <see cref="Directory"/> may contain Windows environment variable
/// placeholders like <c>%LOCALAPPDATA%</c>; <see cref="VayuLogger"/>
/// expands them at configuration time. <see cref="Verbose"/> is
/// off by default — turning it on does NOT disable redaction; it
/// only lowers the minimum level to Debug.
/// </remarks>
public sealed record LoggingOptions
{
    /// <summary>Minimum log level: <c>Verbose</c>, <c>Debug</c>, <c>Information</c>, <c>Warning</c>, <c>Error</c>, <c>Fatal</c>.</summary>
    public string Level { get; init; } = "Information";

    /// <summary>Directory for rolling daily log files. Environment variables are expanded.</summary>
    public string Directory { get; init; } = @"%LOCALAPPDATA%\Vayu\logs";

    /// <summary>How many days of rolled files to keep on disk.</summary>
    public int RetainDays { get; init; } = 7;

    /// <summary>
    /// When true, the minimum level drops to <c>Debug</c> regardless of <see cref="Level"/>.
    /// Does not disable secret redaction.
    /// </summary>
    public bool Verbose { get; init; }

    /// <summary>How many recent events the in-memory ring buffer keeps for the Logs page.</summary>
    public int InMemoryCapacity { get; init; } = 1000;

    /// <summary>File name template for the rolling sink. Daily roll appends a date stamp.</summary>
    public string FileNamePrefix { get; init; } = "vayu-.log";
}
