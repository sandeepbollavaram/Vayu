namespace Vayu.Automation.Windows;

/// <summary>
/// How a <see cref="KnownAppInfo"/> entry is launched.
/// </summary>
public enum LaunchKind
{
    /// <summary>An executable name resolved through PATH or App Paths (e.g. <c>notepad</c>, <c>chrome</c>).</summary>
    Executable = 0,

    /// <summary>A shell command string handed to the OS shell as-is. Reserved for future use.</summary>
    ShellCommand = 1,

    /// <summary>A folder path. Environment variables in the path are expanded at launch time.</summary>
    Folder = 2,

    /// <summary>A URI handled by a registered protocol (e.g. <c>microsoft-edge:</c>). Reserved for future use.</summary>
    Uri = 3,
}

/// <summary>
/// One entry in <see cref="KnownAppCatalog"/>. Vayu only launches things
/// described by an entry of this shape — never an arbitrary string from
/// the user.
/// </summary>
/// <param name="AppId">Stable, lowercase identifier used in <c>IntentPlan.Args["app"]</c> and the catalog lookup.</param>
/// <param name="DisplayName">Human-readable label shown in the UI and audit log.</param>
/// <param name="LaunchTarget">Executable name, folder path, or URI to launch. Folder targets may contain <c>%ENV%</c> placeholders.</param>
/// <param name="LaunchKind">How <see cref="LaunchTarget"/> should be invoked.</param>
public sealed record KnownAppInfo(
    string AppId,
    string DisplayName,
    string LaunchTarget,
    LaunchKind LaunchKind);
