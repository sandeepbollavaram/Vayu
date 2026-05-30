using Microsoft.Data.Sqlite;

using Vayu.Core;

namespace Vayu.Memory.Tests;

public class SqliteAuditLogServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly MemoryOptions _options;

    public SqliteAuditLogServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vayu-tests-{Guid.NewGuid():N}.db");
        _options = new MemoryOptions { DatabasePath = _dbPath };
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
    }

    private SqliteAuditLogService NewService() => new(_options);

    private static ActionLogEntry NewEntry(
        Guid correlationId,
        DateTimeOffset timestamp,
        string? agentName = "AppLauncher",
        RiskLevel risk = RiskLevel.L1,
        PermissionDecision decision = PermissionDecision.Allowed,
        CommandStatus status = CommandStatus.Success,
        string? redactedDetails = null,
        string? errorMessage = null)
        => new()
        {
            TimestampUtc = timestamp,
            CorrelationId = correlationId,
            AgentName = agentName,
            CommandText = "open notepad",
            RiskLevel = risk,
            PermissionDecision = decision,
            Status = status,
            RedactedDetails = redactedDetails,
            ErrorMessage = errorMessage,
        };

    [Fact]
    public async Task EmptyDatabase_ReturnsEmptyList()
    {
        var svc = NewService();

        var entries = await svc.ListRecentAsync(50);

        Assert.Empty(entries);
    }

    [Fact]
    public async Task AppendAsync_AssignsId_AndReturnsEntry()
    {
        var svc = NewService();
        var entry = NewEntry(Guid.NewGuid(), DateTimeOffset.UtcNow);

        var stored = await svc.AppendAsync(entry);

        Assert.True(stored.Id > 0);
        Assert.Equal(entry.CorrelationId, stored.CorrelationId);
        Assert.Equal(entry.AgentName, stored.AgentName);
    }

    [Fact]
    public async Task AppendAsync_ThenList_ReturnsRow()
    {
        var svc = NewService();
        var entry = NewEntry(Guid.NewGuid(), DateTimeOffset.UtcNow,
            redactedDetails: """{"app":"notepad"}""");
        await svc.AppendAsync(entry);

        var entries = await svc.ListRecentAsync(10);

        var only = Assert.Single(entries);
        Assert.Equal(entry.CorrelationId, only.CorrelationId);
        Assert.Equal("""{"app":"notepad"}""", only.RedactedDetails);
    }

    [Fact]
    public async Task ListRecentAsync_OrdersNewestFirst()
    {
        var svc = NewService();
        var now = DateTimeOffset.UtcNow;
        await svc.AppendAsync(NewEntry(Guid.NewGuid(), now.AddMinutes(-30), agentName: "older"));
        await svc.AppendAsync(NewEntry(Guid.NewGuid(), now.AddMinutes(-10), agentName: "middle"));
        await svc.AppendAsync(NewEntry(Guid.NewGuid(), now,                 agentName: "newest"));

        var entries = await svc.ListRecentAsync(10);

        Assert.Equal(3, entries.Count);
        Assert.Equal("newest", entries[0].AgentName);
        Assert.Equal("middle", entries[1].AgentName);
        Assert.Equal("older",  entries[2].AgentName);
    }

    [Fact]
    public async Task ListRecentAsync_RespectsLimit()
    {
        var svc = NewService();
        for (var i = 0; i < 5; i++)
        {
            await svc.AppendAsync(NewEntry(Guid.NewGuid(), DateTimeOffset.UtcNow.AddSeconds(i)));
        }

        var entries = await svc.ListRecentAsync(2);

        Assert.Equal(2, entries.Count);
    }

    [Fact]
    public async Task FindByCorrelationAsync_ReturnsMatchingRow()
    {
        var svc = NewService();
        var corr = Guid.NewGuid();
        await svc.AppendAsync(NewEntry(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), agentName: "noise"));
        await svc.AppendAsync(NewEntry(corr, DateTimeOffset.UtcNow, agentName: "target"));

        var found = await svc.FindByCorrelationAsync(corr);

        Assert.NotNull(found);
        Assert.Equal("target", found!.AgentName);
        Assert.Equal(corr, found.CorrelationId);
    }

    [Fact]
    public async Task FindByCorrelationAsync_ReturnsNull_WhenMissing()
    {
        var svc = NewService();

        var found = await svc.FindByCorrelationAsync(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public async Task RedactedDetails_SurviveRoundTrip()
    {
        var svc = NewService();
        var corr = Guid.NewGuid();
        const string details = """{"prompt":"open [REDACTED] now","args":{"app":"notepad"}}""";
        await svc.AppendAsync(NewEntry(corr, DateTimeOffset.UtcNow, redactedDetails: details));

        var found = await svc.FindByCorrelationAsync(corr);

        Assert.NotNull(found);
        Assert.Equal(details, found!.RedactedDetails);
    }

    [Fact]
    public async Task RiskAndPermission_RoundTripExactly()
    {
        var svc = NewService();
        var corr = Guid.NewGuid();
        var entry = NewEntry(corr, DateTimeOffset.UtcNow,
            risk: RiskLevel.L4,
            decision: PermissionDecision.RequiresConfirmation,
            status: CommandStatus.PermissionRequired,
            errorMessage: "user has not confirmed yet");

        await svc.AppendAsync(entry);
        var found = await svc.FindByCorrelationAsync(corr);

        Assert.NotNull(found);
        Assert.Equal(RiskLevel.L4, found!.RiskLevel);
        Assert.Equal(PermissionDecision.RequiresConfirmation, found.PermissionDecision);
        Assert.Equal(CommandStatus.PermissionRequired, found.Status);
        Assert.Equal("user has not confirmed yet", found.ErrorMessage);
    }

    [Fact]
    public async Task SchemaBootstrap_CreatesDatabase_WhenFileMissing()
    {
        // Confirm the constructor + first call created the file.
        var svc = NewService();
        await svc.AppendAsync(NewEntry(Guid.NewGuid(), DateTimeOffset.UtcNow));

        Assert.True(File.Exists(_dbPath));
    }

    [Fact]
    public async Task ListRecentAsync_ReturnsEmpty_WhenLimitIsZero()
    {
        var svc = NewService();
        await svc.AppendAsync(NewEntry(Guid.NewGuid(), DateTimeOffset.UtcNow));

        var entries = await svc.ListRecentAsync(0);

        Assert.Empty(entries);
    }
}
