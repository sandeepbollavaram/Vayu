using Vayu.Core.Setup;

namespace Vayu.Core.Tests;

public class FirstRunExperienceTests
{
    [Fact]
    public void FreshInstall_RoutesToFirstRunSetup()
    {
        var routing = FirstRunExperience.DecideRouting(FirstRunSetupState.Empty);

        Assert.Equal(FirstRunRouting.ShowFirstRunSetup, routing);
        Assert.True(FirstRunExperience.ShouldShowFirstRunSetup(FirstRunSetupState.Empty));
    }

    [Fact]
    public void Completed_RoutesToCommandCenter()
    {
        var state = FirstRunSetupState.Empty with
        {
            Completed = true,
            CompletedAtUtc = DateTimeOffset.UnixEpoch,
        };

        Assert.Equal(FirstRunRouting.EnterCommandCenter, FirstRunExperience.DecideRouting(state));
        Assert.False(FirstRunExperience.ShouldShowFirstRunSetup(state));
    }

    [Fact]
    public void Skipped_CountsAsCompleted_RoutesToCommandCenter()
    {
        // Skip records Completed=true so the experience does not auto-reopen.
        var skipped = FirstRunSetupState.Empty with { Completed = true };

        Assert.Equal(FirstRunRouting.EnterCommandCenter, FirstRunExperience.DecideRouting(skipped));
    }

    [Theory]
    [InlineData(FirstRunSetupMode.OfflineOnly)]
    [InlineData(FirstRunSetupMode.OnlineOnly)]
    [InlineData(FirstRunSetupMode.Hybrid)]
    public void Routing_IgnoresMode_OnlyCompletionMatters(FirstRunSetupMode mode)
    {
        var notDone = FirstRunSetupState.Empty with { Mode = mode, Completed = false };
        var done = FirstRunSetupState.Empty with { Mode = mode, Completed = true };

        Assert.Equal(FirstRunRouting.ShowFirstRunSetup, FirstRunExperience.DecideRouting(notDone));
        Assert.Equal(FirstRunRouting.EnterCommandCenter, FirstRunExperience.DecideRouting(done));
    }

    [Fact]
    public void DecideRouting_Throws_OnNullState()
    {
        Assert.Throws<ArgumentNullException>(() => FirstRunExperience.DecideRouting(null!));
    }
}
