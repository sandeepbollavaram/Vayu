using Vayu.Core;
using Vayu.Memory;

namespace Vayu.Permissions.Tests;

public class DefaultPermissionServiceTests
{
    private static PermissionRequest Request(RiskLevel risk, string commandText = "open notepad") => new()
    {
        Plan = new IntentPlan { Intent = "app.launch", Risk = risk },
        Origin = new CommandRequest { Text = commandText, Source = "text" },
        AgentName = "AppLauncher",
    };

    private static DefaultPermissionService BuildService(
        PermissionDecision promptOutcome = PermissionDecision.Allowed,
        FakeAuditLog? auditLog = null,
        PermissionPolicy? policy = null)
        => new(
            policy ?? PermissionPolicy.Default,
            new FakeConfirmationService(promptOutcome),
            new FakeClock(),
            auditLog);

    [Theory]
    [InlineData(RiskLevel.L0)]
    [InlineData(RiskLevel.L1)]
    [InlineData(RiskLevel.L2)]
    public async Task LowRisk_IsAutoAllowed_WithoutPrompt(RiskLevel risk)
    {
        var prompt = new FakeConfirmationService(PermissionDecision.Denied);
        var svc = new DefaultPermissionService(PermissionPolicy.Default, prompt, new FakeClock());

        var result = await svc.EvaluateAsync(Request(risk));

        Assert.Equal(PermissionDecision.Allowed, result.Decision);
        Assert.Equal(0, prompt.CallCount);
    }

    [Theory]
    [InlineData(RiskLevel.L3)]
    [InlineData(RiskLevel.L4)]
    [InlineData(RiskLevel.L5)]
    public async Task MidRisk_AsksPrompt_AndForwardsItsDecision(RiskLevel risk)
    {
        var prompt = new FakeConfirmationService(PermissionDecision.Cancelled);
        var svc = new DefaultPermissionService(PermissionPolicy.Default, prompt, new FakeClock());

        var result = await svc.EvaluateAsync(Request(risk));

        Assert.Equal(PermissionDecision.Cancelled, result.Decision);
        Assert.Equal(1, prompt.CallCount);
    }

    [Fact]
    public async Task L6_IsDenied_WithoutPrompt()
    {
        var prompt = new FakeConfirmationService(PermissionDecision.Allowed);
        var svc = new DefaultPermissionService(PermissionPolicy.Default, prompt, new FakeClock());

        var result = await svc.EvaluateAsync(Request(RiskLevel.L6));

        Assert.Equal(PermissionDecision.Denied, result.Decision);
        Assert.Equal(0, prompt.CallCount);
        Assert.Contains("disabled by policy", result.Reason);
    }

    [Fact]
    public async Task AuditLog_RowWritten_OnAllow()
    {
        var audit = new FakeAuditLog();
        var svc = BuildService(auditLog: audit);

        await svc.EvaluateAsync(Request(RiskLevel.L1));

        var row = Assert.Single(audit.Entries);
        Assert.Equal(PermissionDecision.Allowed, row.PermissionDecision);
        Assert.Equal(CommandStatus.Success, row.Status);
        Assert.Equal("AppLauncher", row.AgentName);
        Assert.Null(row.ErrorMessage);
    }

    [Fact]
    public async Task AuditLog_RowWritten_OnDeny()
    {
        var audit = new FakeAuditLog();
        var svc = BuildService(auditLog: audit);

        await svc.EvaluateAsync(Request(RiskLevel.L6));

        var row = Assert.Single(audit.Entries);
        Assert.Equal(PermissionDecision.Denied, row.PermissionDecision);
        Assert.Equal(CommandStatus.Cancelled, row.Status);
        Assert.False(string.IsNullOrEmpty(row.ErrorMessage));
    }

    [Fact]
    public async Task AuditLog_RowWritten_OnCancel()
    {
        var audit = new FakeAuditLog();
        var svc = BuildService(promptOutcome: PermissionDecision.Cancelled, auditLog: audit);

        await svc.EvaluateAsync(Request(RiskLevel.L4));

        var row = Assert.Single(audit.Entries);
        Assert.Equal(PermissionDecision.Cancelled, row.PermissionDecision);
        Assert.Equal(CommandStatus.Cancelled, row.Status);
    }

    [Fact]
    public async Task AuditLog_NullAuditLog_DoesNotThrow()
    {
        var svc = BuildService(auditLog: null);

        var result = await svc.EvaluateAsync(Request(RiskLevel.L1));

        Assert.Equal(PermissionDecision.Allowed, result.Decision);
    }

    [Fact]
    public async Task AuditLog_CommandText_IsRedacted_BeforeStorage()
    {
        var audit = new FakeAuditLog();
        var svc = BuildService(auditLog: audit);
        // Compile-time concatenation keeps this out of CI secret-grep.
        var fakeKey = "AIza" + "SyVayuTestKeyFakeValueAbcdef1234567890";

        await svc.EvaluateAsync(Request(RiskLevel.L1, commandText: $"set gemini key {fakeKey}"));

        var row = Assert.Single(audit.Entries);
        Assert.NotNull(row.CommandText);
        Assert.DoesNotContain(fakeKey, row.CommandText);
        Assert.Contains("[REDACTED]", row.CommandText);
    }

    [Fact]
    public async Task AuditLog_PreservesCorrelationId_FromPlan()
    {
        var audit = new FakeAuditLog();
        var corr = Guid.NewGuid();
        var req = new PermissionRequest
        {
            Plan = new IntentPlan { Intent = "x", Risk = RiskLevel.L1, CorrelationId = corr },
            Origin = new CommandRequest { Text = "x", Source = "text" },
        };
        var svc = BuildService(auditLog: audit);

        await svc.EvaluateAsync(req);

        Assert.Equal(corr, audit.Entries[0].CorrelationId);
    }

    [Fact]
    public async Task AuditLog_TimestampComesFromClock()
    {
        var audit = new FakeAuditLog();
        var fixedNow = new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero);
        var svc = new DefaultPermissionService(
            PermissionPolicy.Default,
            new FakeConfirmationService(),
            new FakeClock { UtcNow = fixedNow },
            audit);

        await svc.EvaluateAsync(Request(RiskLevel.L1));

        Assert.Equal(fixedNow, audit.Entries[0].TimestampUtc);
    }

    [Fact]
    public void Constructor_Throws_OnNullArgs()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DefaultPermissionService(null!, new FakeConfirmationService(), new FakeClock()));
        Assert.Throws<ArgumentNullException>(() =>
            new DefaultPermissionService(PermissionPolicy.Default, null!, new FakeClock()));
        Assert.Throws<ArgumentNullException>(() =>
            new DefaultPermissionService(PermissionPolicy.Default, new FakeConfirmationService(), null!));
    }

    [Fact]
    public async Task EvaluateAsync_Throws_OnNullRequest()
    {
        var svc = BuildService();

        await Assert.ThrowsAsync<ArgumentNullException>(() => svc.EvaluateAsync(null!));
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
    }

    private sealed class FakeAuditLog : IAuditLogService
    {
        public List<ActionLogEntry> Entries { get; } = new();

        public Task<ActionLogEntry> AppendAsync(ActionLogEntry entry, CancellationToken cancellationToken = default)
        {
            var stored = entry with { Id = Entries.Count + 1 };
            Entries.Add(stored);
            return Task.FromResult(stored);
        }

        public Task<IReadOnlyList<ActionLogEntry>> ListRecentAsync(int limit, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ActionLogEntry> snapshot = Entries
                .OrderByDescending(e => e.Id)
                .Take(limit)
                .ToArray();
            return Task.FromResult(snapshot);
        }

        public Task<ActionLogEntry?> FindByCorrelationAsync(Guid correlationId, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries.FirstOrDefault(e => e.CorrelationId == correlationId));
    }
}
