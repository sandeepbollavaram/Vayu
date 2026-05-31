using Vayu.AI.Local;
using Vayu.Core;

namespace Vayu.AI.Local.Tests;

public class OllamaPlanJsonParserTests
{
    private static readonly Guid Corr = Guid.NewGuid();
    private const string Source = "ollama:gemma3:1b";

    [Fact]
    public void TryParse_ValidAppOpen_ProducesL1Plan()
    {
        const string json = """{"intent":"app.open","confidence":0.86,"args":{"app":"notepad"},"reason":"open notepad"}""";

        var ok = OllamaPlanJsonParser.TryParse(json, Corr, Source, out var plan, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal("app.open", plan!.Intent);
        Assert.Equal(RiskLevel.L1, plan.Risk);
        Assert.Equal("notepad", plan.Args["app"]);
        Assert.Equal(0.86, plan.Confidence);
        Assert.Equal(Source, plan.PlanSource);
        Assert.Equal(Corr, plan.CorrelationId);
    }

    [Fact]
    public void TryParse_ShowLogs_IsL0()
    {
        var ok = OllamaPlanJsonParser.TryParse("""{"intent":"ui.show_logs","confidence":0.9}""", Corr, Source, out var plan, out _);

        Assert.True(ok);
        Assert.Equal("ui.show_logs", plan!.Intent);
        Assert.Equal(RiskLevel.L0, plan.Risk);
    }

    [Fact]
    public void TryParse_ShowSettings_IsL0()
    {
        var ok = OllamaPlanJsonParser.TryParse("""{"intent":"ui.show_settings","confidence":0.9}""", Corr, Source, out var plan, out _);

        Assert.True(ok);
        Assert.Equal("ui.show_settings", plan!.Intent);
        Assert.Equal(RiskLevel.L0, plan.Risk);
    }

    [Fact]
    public void TryParse_AppOpenWithoutApp_Fails()
    {
        var ok = OllamaPlanJsonParser.TryParse("""{"intent":"app.open","confidence":0.9}""", Corr, Source, out var plan, out var error);

        Assert.False(ok);
        Assert.Null(plan);
        Assert.Contains("requires an 'app'", error);
    }

    [Fact]
    public void TryParse_NonAllowlistedIntent_Rejected()
    {
        // The model must not be able to invent dangerous intents.
        const string json = """{"intent":"shell.run","confidence":0.99,"args":{"app":"rm -rf"}}""";

        var ok = OllamaPlanJsonParser.TryParse(json, Corr, Source, out var plan, out var error);

        Assert.False(ok);
        Assert.Null(plan);
        Assert.Contains("non-allowlisted", error);
    }

    [Theory]
    [InlineData("email.send")]
    [InlineData("file.delete")]
    [InlineData("browser.login")]
    [InlineData("system.admin")]
    [InlineData("ui.click")]
    public void TryParse_RiskyInventedIntents_AllRejected(string intent)
    {
        var json = $$"""{"intent":"{{intent}}","confidence":0.99}""";

        var ok = OllamaPlanJsonParser.TryParse(json, Corr, Source, out _, out var error);

        Assert.False(ok);
        Assert.Contains("non-allowlisted", error);
    }

    [Fact]
    public void TryParse_TypingRequest_FlaggedForM5_NotExecuted()
    {
        const string json = """{"intent":"app.open","confidence":0.9,"args":{"app":"notepad","typingRequested":true}}""";

        var ok = OllamaPlanJsonParser.TryParse(json, Corr, Source, out var plan, out _);

        Assert.True(ok);
        Assert.Equal("true", plan!.Args[OllamaPlanJsonParser.TypingRequestedArgKey]);
        // It's still an app.open — the agent (not the parser) defers typing to M5.
        Assert.Equal("app.open", plan.Intent);
    }

    [Fact]
    public void TryParse_MalformedJson_FailsSafely()
    {
        var ok = OllamaPlanJsonParser.TryParse("{ this is not json", Corr, Source, out var plan, out var error);

        Assert.False(ok);
        Assert.Null(plan);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_NoJsonObject_FailsSafely()
    {
        var ok = OllamaPlanJsonParser.TryParse("I think you want to open notepad.", Corr, Source, out _, out var error);

        Assert.False(ok);
        Assert.Contains("did not return a JSON object", error);
    }

    [Fact]
    public void TryParse_ExtractsJson_FromProseWrappedOutput()
    {
        // Models often wrap JSON in prose or code fences.
        const string wrapped = "Sure! Here is the plan:\n```json\n{\"intent\":\"app.open\",\"confidence\":0.8,\"args\":{\"app\":\"spotify\"}}\n```";

        var ok = OllamaPlanJsonParser.TryParse(wrapped, Corr, Source, out var plan, out _);

        Assert.True(ok);
        Assert.Equal("spotify", plan!.Args["app"]);
    }

    [Fact]
    public void TryParse_ConfidenceOutOfRange_Fails()
    {
        var ok = OllamaPlanJsonParser.TryParse("""{"intent":"ui.show_logs","confidence":1.7}""", Corr, Source, out _, out var error);

        Assert.False(ok);
        Assert.Contains("out of range", error);
    }

    [Fact]
    public void TryParse_AppName_CollapsesWhitespace()
    {
        var ok = OllamaPlanJsonParser.TryParse("""{"intent":"app.open","confidence":0.9,"args":{"app":"vs code"}}""", Corr, Source, out var plan, out _);

        Assert.True(ok);
        Assert.Equal("vscode", plan!.Args["app"]);
    }

    [Fact]
    public void TryParse_IntentRisk_AlwaysAssignedByVayu_NotModel()
    {
        // Even if the model claims a low risk for app.open, Vayu assigns L1.
        const string json = """{"intent":"app.open","risk":"L0","confidence":0.9,"args":{"app":"chrome"}}""";

        var ok = OllamaPlanJsonParser.TryParse(json, Corr, Source, out var plan, out _);

        Assert.True(ok);
        Assert.Equal(RiskLevel.L1, plan!.Risk);
    }
}
