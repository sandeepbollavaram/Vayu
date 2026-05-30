using Vayu.Core;

namespace Vayu.Memory.Tests;

public class ActionLogEntryTests
{
    private static ActionLogEntry NewEntry(Guid? correlationId = null) => new()
    {
        TimestampUtc = DateTimeOffset.UtcNow,
        CorrelationId = correlationId ?? Guid.NewGuid(),
        RiskLevel = RiskLevel.L1,
        PermissionDecision = PermissionDecision.Allowed,
        Status = CommandStatus.Success,
    };

    [Fact]
    public void Construction_WithRequiredFields_LeavesOptionalsNull()
    {
        var entry = NewEntry();

        Assert.Equal(0, entry.Id);
        Assert.Null(entry.AgentName);
        Assert.Null(entry.CommandText);
        Assert.Null(entry.RedactedDetails);
        Assert.Null(entry.ErrorMessage);
    }

    [Fact]
    public void With_AssignsId_WithoutMutatingOriginal()
    {
        var original = NewEntry();
        var withId = original with { Id = 42 };

        Assert.Equal(0, original.Id);
        Assert.Equal(42, withId.Id);
        Assert.Equal(original.CorrelationId, withId.CorrelationId);
    }

    [Fact]
    public void CorrelationId_IsPreserved_AcrossWith()
    {
        var corr = Guid.NewGuid();
        var entry = NewEntry(corr);
        var copy = entry with { AgentName = "AppLauncher" };

        Assert.Equal(corr, copy.CorrelationId);
        Assert.Equal("AppLauncher", copy.AgentName);
    }

    [Fact]
    public void RecordEquality_IgnoresReferenceIdentity_OnSameValues()
    {
        var ts = DateTimeOffset.UtcNow;
        var corr = Guid.NewGuid();
        var a = new ActionLogEntry
        {
            TimestampUtc = ts,
            CorrelationId = corr,
            RiskLevel = RiskLevel.L3,
            PermissionDecision = PermissionDecision.RequiresConfirmation,
            Status = CommandStatus.PermissionRequired,
            AgentName = "Gmail",
        };
        var b = a with { };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
