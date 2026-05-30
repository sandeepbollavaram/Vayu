namespace Vayu.Core;

/// <summary>
/// Abstraction over the system clock. All Vayu code that needs the current
/// time depends on this interface so tests can drive time deterministically.
/// </summary>
public interface IClock
{
    /// <summary>The current instant in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}
