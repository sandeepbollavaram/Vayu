namespace Vayu.Core;

/// <summary>
/// Outcome of evaluating an <see cref="IntentPlan"/> against the permission
/// policy. Distinct from <see cref="CommandStatus"/>, which describes the
/// outcome of the whole command (planning + permission + execution).
/// </summary>
public enum PermissionDecision
{
    /// <summary>The action is permitted to run now without asking the user.</summary>
    Allowed = 0,

    /// <summary>The action is forbidden by policy. The user is not even asked.</summary>
    Denied = 1,

    /// <summary>The action requires the user to confirm before it can run.</summary>
    RequiresConfirmation = 2,

    /// <summary>The user was asked and explicitly declined.</summary>
    Cancelled = 3,
}
