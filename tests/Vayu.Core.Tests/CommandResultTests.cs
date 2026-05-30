namespace Vayu.Core.Tests;

public class CommandResultTests
{
    private static IntentPlan Plan() =>
        new() { Intent = "app.launch", Risk = RiskLevel.L1 };

    [Fact]
    public void Success_Factory_SetsStatusAndCarriesContext()
    {
        var p = Plan();
        var r = CommandResult.Success(message: "ok", agentName: "AppLauncher", plan: p);

        Assert.Equal(CommandStatus.Success, r.Status);
        Assert.Equal("ok", r.Message);
        Assert.Equal("AppLauncher", r.AgentName);
        Assert.Equal(p, r.Plan);
        Assert.Null(r.ErrorCode);
        Assert.Null(r.ClarificationPrompt);
    }

    [Fact]
    public void Failed_Factory_CarriesMessageAndErrorCode()
    {
        var r = CommandResult.Failed("Ollama unreachable", errorCode: "OLLAMA_UNREACHABLE");

        Assert.Equal(CommandStatus.Failed, r.Status);
        Assert.Equal("Ollama unreachable", r.Message);
        Assert.Equal("OLLAMA_UNREACHABLE", r.ErrorCode);
    }

    [Fact]
    public void PermissionRequired_Factory_RequiresPlan()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CommandResult.PermissionRequired(plan: null!));
    }

    [Fact]
    public void PermissionRequired_Factory_DefaultsMessage()
    {
        var p = Plan();
        var r = CommandResult.PermissionRequired(p);

        Assert.Equal(CommandStatus.PermissionRequired, r.Status);
        Assert.Equal(p, r.Plan);
        Assert.Equal("Confirmation required.", r.Message);
    }

    [Fact]
    public void Cancelled_Factory_DefaultsMessage()
    {
        var r = CommandResult.Cancelled();

        Assert.Equal(CommandStatus.Cancelled, r.Status);
        Assert.Equal("Cancelled.", r.Message);
    }

    [Fact]
    public void NeedsClarification_Factory_RequiresPrompt()
    {
        Assert.Throws<ArgumentException>(() =>
            CommandResult.NeedsClarification(clarificationPrompt: " "));
    }

    [Fact]
    public void NeedsClarification_Factory_CarriesPrompt()
    {
        var p = Plan();
        var r = CommandResult.NeedsClarification("Which Chrome profile?", p);

        Assert.Equal(CommandStatus.NeedsClarification, r.Status);
        Assert.Equal("Which Chrome profile?", r.ClarificationPrompt);
        Assert.Equal(p, r.Plan);
    }
}
