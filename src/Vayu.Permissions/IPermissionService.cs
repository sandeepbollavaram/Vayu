namespace Vayu.Permissions;

/// <summary>
/// The permission engine. Sits between the AI planner and any agent
/// that would actually run side effects — the AI never executes a tool
/// directly, the runtime always passes the plan through this service
/// first.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Evaluates <paramref name="request"/> against policy and (when
    /// required) the user, and returns a <see cref="PermissionResult"/>.
    /// Implementations also write a row to the audit log for every
    /// non-trivial decision.
    /// </summary>
    Task<PermissionResult> EvaluateAsync(PermissionRequest request, CancellationToken cancellationToken = default);
}
