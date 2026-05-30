namespace Vayu.Core;

/// <summary>
/// The single entry point the UI uses to run a command. Implementations
/// parse the request, gate it through the permission engine, route it to
/// the right agent, and return a unified <see cref="CommandResult"/>.
/// </summary>
public interface IAgentRuntime
{
    /// <summary>Dispatches a user command end-to-end.</summary>
    Task<CommandResult> DispatchAsync(CommandRequest request, CancellationToken cancellationToken = default);
}
