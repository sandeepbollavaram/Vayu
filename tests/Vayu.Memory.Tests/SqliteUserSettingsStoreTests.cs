using Microsoft.Data.Sqlite;

namespace Vayu.Memory.Tests;

public class SqliteUserSettingsStoreTests : IDisposable
{
    private readonly string _dbPath;
    private readonly MemoryOptions _options;

    public SqliteUserSettingsStoreTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"vayu-settings-{Guid.NewGuid():N}.db");
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

    private SqliteUserSettingsStore NewStore() => new(_options);

    [Fact]
    public async Task Get_MissingKey_ReturnsNull()
    {
        var store = NewStore();
        Assert.Null(await store.GetAsync("nope"));
    }

    [Fact]
    public async Task Set_Then_Get_RoundTrips()
    {
        var store = NewStore();
        await store.SetAsync("k", "v1");
        Assert.Equal("v1", await store.GetAsync("k"));
    }

    [Fact]
    public async Task Set_Upserts_ExistingKey()
    {
        var store = NewStore();
        await store.SetAsync("k", "v1");
        await store.SetAsync("k", "v2");
        Assert.Equal("v2", await store.GetAsync("k"));
    }

    [Fact]
    public async Task Persists_AcrossNewStoreInstances()
    {
        await NewStore().SetAsync("firstrun.setup.state", "{\"done\":true}");

        // A fresh store over the same db file must see the value (durable).
        Assert.Equal("{\"done\":true}", await NewStore().GetAsync("firstrun.setup.state"));
    }

    [Fact]
    public async Task Remove_DeletesKey()
    {
        var store = NewStore();
        await store.SetAsync("k", "v");
        await store.RemoveAsync("k");
        Assert.Null(await store.GetAsync("k"));
    }

    [Fact]
    public async Task Get_Throws_OnBlankKey()
    {
        var store = NewStore();
        await Assert.ThrowsAnyAsync<ArgumentException>(() => store.GetAsync("  "));
    }
}
