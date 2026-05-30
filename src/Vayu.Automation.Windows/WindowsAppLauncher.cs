using System.Diagnostics;
using System.Runtime.Versioning;

using Vayu.Core;

namespace Vayu.Automation.Windows;

/// <summary>
/// Windows implementation of <see cref="IAppLauncher"/>. Resolves the
/// requested app through <see cref="KnownAppCatalog"/> and launches it
/// with <see cref="Process.Start(ProcessStartInfo)"/> using
/// <c>UseShellExecute = true</c>.
/// </summary>
/// <remarks>
/// Safety properties:
/// <list type="bullet">
/// <item>Never accepts an arbitrary executable path — only catalog entries.</item>
/// <item>Never elevates: the <c>Verb</c> is fixed to <c>open</c>.</item>
/// <item>Does not pass user-supplied arguments to the launched process in M1.</item>
/// <item>Returns <see cref="CommandResult.Failed"/> with a stable error code on every failure; never throws.</item>
/// </list>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsAppLauncher : IAppLauncher
{
    private const string AgentName = "AppLauncher";

    /// <inheritdoc />
    public Task<CommandResult> LaunchAsync(string appId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        cancellationToken.ThrowIfCancellationRequested();

        var info = KnownAppCatalog.TryGet(appId);
        if (info is null)
        {
            return Task.FromResult(CommandResult.Failed(
                $"'{appId}' is not in the Vayu app catalog.",
                errorCode: "UNKNOWN_APP",
                agentName: AgentName));
        }

        var target = info.LaunchKind == LaunchKind.Folder
            ? Environment.ExpandEnvironmentVariables(info.LaunchTarget)
            : info.LaunchTarget;

        var startInfo = new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
            Verb = "open",
        };

        try
        {
            using var process = Process.Start(startInfo);
            // process may be null when the OS shell handles the open without
            // returning a Process handle (typical for folder opens). That is
            // still a successful launch.
            return Task.FromResult(CommandResult.Success(
                message: $"Launched {info.DisplayName}.",
                agentName: AgentName));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Boundary: launcher must never let an underlying Win32 / IO exception escape into the runtime.
        catch (Exception ex)
        {
            return Task.FromResult(CommandResult.Failed(
                $"Could not launch {info.DisplayName}: {ex.GetType().Name}.",
                errorCode: "LAUNCH_FAILED",
                agentName: AgentName));
        }
#pragma warning restore CA1031
    }
}
