using Vayu.AI.Gemini;
using Vayu.Core;

namespace Vayu.AI.Gemini.Tests;

public class GeminiPlanJsonParserTests
{
    private static readonly Guid Corr = Guid.NewGuid();
    private const string Source = "gemini:gemini-2.0-flash";

    [Fact]
    public void Parse_ValidAppOpen_IsL1()
    {
        const string json = """{"intent":"app.open","confidence":0.9,"args":{"app":"notepad"}}""";

        var ok = GeminiPlanJsonParser.TryParse(json, Corr, Source, out var plan, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal("app.open", plan!.Intent);
        Assert.Equal(RiskLevel.L1, plan.Risk);
        Assert.Equal("notepad", plan.Args["app"]);
    }

    [Fact]
    public void Parse_ShowLogs_IsL0()
    {
        var ok = GeminiPlanJsonParser.TryParse("""{"intent":"ui.show_logs","confidence":0.9}""", Corr, Source, out var plan, out _);

        Assert.True(ok);
        Assert.Equal(RiskLevel.L0, plan!.Risk);
    }

    [Fact]
    public void Parse_AppOpenMissingApp_Fails()
    {
        var ok = GeminiPlanJsonParser.TryParse("""{"intent":"app.open","confidence":0.9}""", Corr, Source, out _, out var error);

        Assert.False(ok);
        Assert.Contains("requires an 'app'", error);
    }

    [Fact]
    public void Parse_UnknownIntent_IsSafe()
    {
        var ok = GeminiPlanJsonParser.TryParse("""{"intent":"unknown","confidence":0.9}""", Corr, Source, out var plan, out _);

        Assert.True(ok);
        Assert.Equal("unknown", plan!.Intent);
        Assert.Equal(RiskLevel.L0, plan.Risk);
    }

    [Theory]
    [InlineData("shell.run")]
    [InlineData("file.delete")]
    [InlineData("email.send")]
    [InlineData("ui.click")]
    [InlineData("ui.type")]
    [InlineData("system.admin")]
    public void Parse_DisallowedIntents_Rejected(string intent)
    {
        var json = $$"""{"intent":"{{intent}}","confidence":0.99}""";

        var ok = GeminiPlanJsonParser.TryParse(json, Corr, Source, out _, out var error);

        Assert.False(ok);
        Assert.Contains("non-allowlisted", error);
    }

    [Fact]
    public void Parse_ModelRisk_IsIgnored_VayuAssignsL1()
    {
        // Model claims L0; Vayu must still assign L1 for app.open.
        const string json = """{"intent":"app.open","risk":"L0","confidence":0.9,"args":{"app":"chrome"}}""";

        var ok = GeminiPlanJsonParser.TryParse(json, Corr, Source, out var plan, out _);

        Assert.True(ok);
        Assert.Equal(RiskLevel.L1, plan!.Risk);
    }

    [Fact]
    public void Parse_TypingRequest_FlaggedForM5()
    {
        const string json = """{"intent":"app.open","confidence":0.9,"args":{"app":"notepad","typingRequested":true}}""";

        var ok = GeminiPlanJsonParser.TryParse(json, Corr, Source, out var plan, out _);

        Assert.True(ok);
        Assert.Equal("true", plan!.Args[GeminiPlanJsonParser.TypingRequestedArgKey]);
    }

    [Fact]
    public void Parse_MalformedJson_FailsSafely()
    {
        var ok = GeminiPlanJsonParser.TryParse("{ not json", Corr, Source, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void Parse_ProseWrappedJson_IsExtracted()
    {
        const string wrapped = "Here is the plan:\n```json\n{\"intent\":\"app.open\",\"confidence\":0.8,\"args\":{\"app\":\"vs code\"}}\n```";

        var ok = GeminiPlanJsonParser.TryParse(wrapped, Corr, Source, out var plan, out _);

        Assert.True(ok);
        Assert.Equal("vscode", plan!.Args["app"]);
    }
}
