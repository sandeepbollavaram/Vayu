using System.Runtime.Versioning;

using Vayu.Automation.Windows;
using Vayu.Core;

namespace Vayu.Automation.Windows.Tests;

/// <summary>
/// Tests that do NOT launch real processes. The launcher must:
/// validate input, fail safely on unknown app ids, and refuse cancelled
/// requests. Actual app launching is covered by the M1 demo script,
/// not CI.
/// </summary>
[SupportedOSPlatform("windows")]
public class WindowsAppLauncherTests
{
    [Fact]
    public async Task LaunchAsync_UnknownApp_ReturnsFailed_WithoutInvokingProcess()
    {
        var launcher = new WindowsAppLauncher();

        var result = await launcher.LaunchAsync("definitely-not-a-real-app-id");

        Assert.Equal(CommandStatus.Failed, result.Status);
        Assert.Equal("UNKNOWN_APP", result.ErrorCode);
        Assert.Equal("AppLauncher", result.AgentName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LaunchAsync_NullEmptyOrWhitespace_Throws(string? input)
    {
        var launcher = new WindowsAppLauncher();

        // null -> ArgumentNullException; empty/whitespace -> ArgumentException.
        // Both are ArgumentException-derived.
        await Assert.ThrowsAnyAsync<ArgumentException>(() => launcher.LaunchAsync(input!));
    }

    [Fact]
    public async Task LaunchAsync_CancelledToken_Throws()
    {
        var launcher = new WindowsAppLauncher();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => launcher.LaunchAsync("notepad", cts.Token));
    }

    [Fact]
    public void Construct_DoesNotThrow()
    {
        var launcher = new WindowsAppLauncher();

        Assert.NotNull(launcher);
    }

    [Fact]
    public void Implements_IAppLauncher()
    {
        IAppLauncher launcher = new WindowsAppLauncher();

        Assert.NotNull(launcher);
    }
}
