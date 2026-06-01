using System.Diagnostics;
using System.Runtime.Versioning;

using Vayu.Core;

namespace Vayu.Automation.Windows;

/// <summary>
/// Windows implementation of <see cref="IAppLauncher"/>. Resolves the
/// requested app through <see cref="KnownAppCatalog"/> (or a vetted
/// <see cref="InstalledAppEntry"/> for shortcut launches) and launches it
/// with <see cref="Process.Start(ProcessStartInfo)"/> using
/// <c>UseShellExecute = true</c>.
/// </summary>
/// <remarks>
/// Safety properties:
/// <list type="bullet">
/// <item>Never accepts an arbitrary executable path through <see cref="LaunchAsync"/>.</item>
/// <item>Shortcut launches require a catalog-issued <see cref="InstalledAppEntry"/>.</item>
/// <item>Never elevates: the <c>Verb</c> is fixed to <c>open</c>.</item>
/// <item>Does not pass user-supplied arguments to the launched process.</item>
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

        // URI launches must pass the scheme allowlist. The catalog is trusted,
        // but this is the safety boundary that guarantees Vayu never shell-opens
        // anything but a vetted, payload-free registered scheme.
        if (info.LaunchKind == LaunchKind.Uri && !KnownUriCatalog.IsAllowedLaunchUri(info.LaunchTarget))
        {
            return Task.FromResult(CommandResult.Failed(
                $"Launch URI for {info.DisplayName} is not on Vayu's allowlist.",
                errorCode: "UNSAFE_URI",
                agentName: AgentName));
        }

        var target = info.LaunchKind == LaunchKind.Folder
            ? Environment.ExpandEnvironmentVariables(info.LaunchTarget)
            : info.LaunchTarget;

        return LaunchInternal(
            target,
            info.DisplayName,
            successMessage: $"Launched {info.DisplayName}.");
    }

    /// <inheritdoc />
    public Task<CommandResult> LaunchShortcutAsync(InstalledAppEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(entry.ShortcutPath) || !File.Exists(entry.ShortcutPath))
        {
            return Task.FromResult(CommandResult.Failed(
                $"Shortcut '{entry.DisplayName}' is no longer present at the recorded path.",
                errorCode: "SHORTCUT_NOT_FOUND",
                agentName: AgentName));
        }

        return LaunchInternal(
            entry.ShortcutPath,
            entry.DisplayName,
            successMessage: $"Launched {entry.DisplayName}.");
    }

    /// <inheritdoc />
    public Task<CommandResult> LaunchExecutablePathAsync(string executablePath, string displayName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        cancellationToken.ThrowIfCancellationRequested();

        // Defence in depth: even though the resolver validated the path, re-check
        // here so this entry point can never launch a command line or non-exe.
        if (!WindowsAppPathsResolver.IsSafeExecutablePath(executablePath))
        {
            return Task.FromResult(CommandResult.Failed(
                $"Resolved path for {displayName} is not a safe executable.",
                errorCode: "UNSAFE_EXECUTABLE",
                agentName: AgentName));
        }

        return LaunchInternal(
            executablePath,
            displayName,
            successMessage: $"Launched {displayName}.");
    }

    private static Task<CommandResult> LaunchInternal(string target, string displayName, string successMessage)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true,
            Verb = "open",
        };

        try
        {
            using var process = Process.Start(startInfo);
            // process may be null when the OS shell handles the open
            // without returning a Process handle (typical for folder
            // opens and shortcut redirects). That is still success.
            return Task.FromResult(CommandResult.Success(
                message: successMessage,
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
                $"Could not launch {displayName}: {ex.GetType().Name}.",
                errorCode: "LAUNCH_FAILED",
                agentName: AgentName));
        }
#pragma warning restore CA1031
    }
}
