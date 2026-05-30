namespace Vayu.Security;

/// <summary>
/// Outcome of an <see cref="ISecretStore"/> mutation. Carries no secret
/// values — only a success flag and an optional, already-redacted message
/// suitable for logging.
/// </summary>
public sealed record SecretStoreResult(bool Success, string? ErrorMessage = null)
{
    /// <summary>Singleton success result.</summary>
    public static SecretStoreResult Ok { get; } = new(true);

    /// <summary>Builds a failed result with a human-readable, redaction-safe message.</summary>
    public static SecretStoreResult Failure(string errorMessage) => new(false, errorMessage);
}
