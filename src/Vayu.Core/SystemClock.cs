namespace Vayu.Core;

/// <summary>
/// Default <see cref="IClock"/> implementation backed by
/// <see cref="DateTimeOffset.UtcNow"/>. Register as a singleton in DI.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
