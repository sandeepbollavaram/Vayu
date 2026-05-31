using Vayu.Core;

namespace Vayu.AgentRuntime.Tests;

public class RuleBasedCommandParserTests
{
    private static CommandRequest Req(string text) => new() { Text = text, Source = "text" };

    [Theory]
    [InlineData("open chrome",    "chrome")]
    [InlineData("open edge",      "edge")]
    [InlineData("open vscode",    "vscode")]
    [InlineData("open vs code",   "vscode")]
    [InlineData("open notepad",   "notepad")]
    [InlineData("open terminal",  "terminal")]
    [InlineData("open downloads", "downloads")]
    public void Open_KnownApp_ProducesAppOpenIntentAtL1(string command, string expectedApp)
    {
        var parser = new RuleBasedCommandParser();

        var plan = parser.Parse(Req(command));

        Assert.Equal(RuleBasedCommandParser.AppOpenIntent, plan.Intent);
        Assert.Equal(RiskLevel.L1, plan.Risk);
        Assert.Equal(expectedApp, plan.Args["app"]);
        Assert.Equal("rule-based", plan.PlanSource);
    }

    [Fact]
    public void ShowLogs_ProducesUiShowLogsAtL0()
    {
        var parser = new RuleBasedCommandParser();

        var plan = parser.Parse(Req("show logs"));

        Assert.Equal(RuleBasedCommandParser.ShowLogsIntent, plan.Intent);
        Assert.Equal(RiskLevel.L0, plan.Risk);
    }

    [Fact]
    public void ShowSettings_ProducesUiShowSettingsAtL0()
    {
        var parser = new RuleBasedCommandParser();

        var plan = parser.Parse(Req("show settings"));

        Assert.Equal(RuleBasedCommandParser.ShowSettingsIntent, plan.Intent);
        Assert.Equal(RiskLevel.L0, plan.Risk);
    }

    [Theory]
    [InlineData("OPEN NOTEPAD")]
    [InlineData("  open notepad  ")]
    [InlineData("Open Notepad")]
    public void Whitespace_AndCase_AreNormalized(string command)
    {
        var parser = new RuleBasedCommandParser();

        var plan = parser.Parse(Req(command));

        Assert.Equal(RuleBasedCommandParser.AppOpenIntent, plan.Intent);
        Assert.Equal("notepad", plan.Args["app"]);
    }

    [Theory]
    [InlineData("hello there")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("open")]
    public void Unknown_Command_ProducesUnknownIntent(string command)
    {
        var parser = new RuleBasedCommandParser();

        var plan = parser.Parse(Req(command));

        Assert.Equal(RuleBasedCommandParser.UnknownIntent, plan.Intent);
        Assert.Equal(RiskLevel.L0, plan.Risk);
    }

    [Theory]
    [InlineData("open spotify",   "spotify")]
    [InlineData("open Slack",     "slack")]
    [InlineData("open spaceship", "spaceship")]
    [InlineData("open Visual Studio Code", "visualstudiocode")]
    public void Open_AnyAppName_ProducesAppOpenAtL1(string command, string expectedApp)
    {
        // The parser no longer whitelists app names — the catalog/agent
        // decides whether the app exists. Anything after "open " becomes
        // app.open at L1.
        var parser = new RuleBasedCommandParser();

        var plan = parser.Parse(Req(command));

        Assert.Equal(RuleBasedCommandParser.AppOpenIntent, plan.Intent);
        Assert.Equal(RiskLevel.L1, plan.Risk);
        Assert.Equal(expectedApp, plan.Args["app"]);
    }

    [Fact]
    public void Parse_PropagatesCorrelationIdFromRequest()
    {
        var parser = new RuleBasedCommandParser();
        var corr = Guid.NewGuid();
        var req = new CommandRequest { Text = "open notepad", Source = "text", CorrelationId = corr };

        var plan = parser.Parse(req);

        Assert.Equal(corr, plan.CorrelationId);
    }

    [Fact]
    public void Parse_Throws_OnNullRequest()
    {
        var parser = new RuleBasedCommandParser();

        Assert.Throws<ArgumentNullException>(() => parser.Parse(null!));
    }
}
