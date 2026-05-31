using Vayu.Core;

namespace Vayu.Voice;

/// <summary>
/// Orchestrates a full voice command: capture → transcript → <see cref="CommandRequest"/>
/// → the existing planner/router/permission/audit pipeline. M4.1 defines the
/// contract; the wiring into <c>AgentRuntime</c> lands in M4.5.
/// </summary>
/// <remarks>
/// The invariant that makes voice safe: a spoken command is just another
/// <see cref="CommandRequest"/> (with <c>Source = "voice/..."</c>). It is
/// planned and gated by exactly the same engine as typed commands — voice
/// never bypasses <c>IPermissionService</c> or the audit log, and never
/// executes a tool directly.
/// </remarks>
public interface IVoiceCommandService
{
    /// <summary>
    /// Runs one push-to-talk command end to end and returns the pipeline's
    /// <see cref="CommandResult"/>. Returns <see langword="null"/> if no usable
    /// transcript was produced (nothing is dispatched in that case).
    /// </summary>
    Task<CommandResult?> StartPushToTalkCommandAsync(CancellationToken cancellationToken = default);

    /// <summary>Cancels any in-progress voice command (stops capture, dispatches nothing).</summary>
    Task CancelAsync(CancellationToken cancellationToken = default);
}
