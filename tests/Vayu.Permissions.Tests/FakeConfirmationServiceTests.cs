using Vayu.Core;

namespace Vayu.Permissions.Tests;

public class FakeConfirmationServiceTests
{
    private static PermissionRequest BuildRequest() => new()
    {
        Plan = new IntentPlan { Intent = "app.launch", Risk = RiskLevel.L4 },
        Origin = new CommandRequest { Text = "send email", Source = "text" },
    };

    [Fact]
    public async Task AskAsync_ReturnsPresetDecision_AndTracksCall()
    {
        var fake = new FakeConfirmationService(PermissionDecision.Allowed);
        var req = BuildRequest();

        var decision = await fake.AskAsync(req);

        Assert.Equal(PermissionDecision.Allowed, decision);
        Assert.Equal(1, fake.CallCount);
        Assert.Same(req, fake.LastRequest);
    }

    [Theory]
    [InlineData(PermissionDecision.Allowed)]
    [InlineData(PermissionDecision.Denied)]
    [InlineData(PermissionDecision.Cancelled)]
    public async Task AskAsync_ReturnsConfiguredOutcome(PermissionDecision configured)
    {
        var fake = new FakeConfirmationService(configured);

        var decision = await fake.AskAsync(BuildRequest());

        Assert.Equal(configured, decision);
    }

    [Fact]
    public void Constructor_Rejects_RequiresConfirmation()
    {
        Assert.Throws<ArgumentException>(() => new FakeConfirmationService(PermissionDecision.RequiresConfirmation));
    }

    [Fact]
    public async Task AskAsync_Throws_OnNullRequest()
    {
        var fake = new FakeConfirmationService();

        await Assert.ThrowsAsync<ArgumentNullException>(() => fake.AskAsync(null!));
    }

    [Fact]
    public async Task CallCount_AccumulatesAcrossCalls()
    {
        var fake = new FakeConfirmationService();

        await fake.AskAsync(BuildRequest());
        await fake.AskAsync(BuildRequest());
        await fake.AskAsync(BuildRequest());

        Assert.Equal(3, fake.CallCount);
    }
}
