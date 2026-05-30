using Vayu.Core;

namespace Vayu.Permissions;

/// <summary>
/// Configurable policy that decides which risk levels auto-allow,
/// which require user confirmation, and which are disabled outright.
/// </summary>
/// <remarks>
/// Defaults match <c>docs/PERMISSIONS.md</c>: anything at or above
/// <see cref="RiskLevel.L3"/> requires confirmation;
/// <see cref="RiskLevel.L6"/> (admin/system) is disabled.
/// </remarks>
public sealed record PermissionPolicy
{
    /// <summary>Inclusive risk floor at which the user must explicitly confirm.</summary>
    public RiskLevel AlwaysConfirmAtOrAbove { get; init; } = RiskLevel.L3;

    /// <summary>Inclusive risk floor that is denied without ever asking the user.</summary>
    public RiskLevel DisabledAtOrAbove { get; init; } = RiskLevel.L6;

    /// <summary>The default policy used by the runtime when no override is provided.</summary>
    public static PermissionPolicy Default { get; } = new();
}
