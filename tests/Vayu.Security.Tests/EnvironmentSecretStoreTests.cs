namespace Vayu.Security.Tests;

public class EnvironmentSecretStoreTests
{
    private static string UniqueVar(string suffix) => $"VAYU_TEST_{suffix}_{Guid.NewGuid():N}";

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenVariableMissing()
    {
        var store = new EnvironmentSecretStore();
        var value = await store.GetAsync(UniqueVar("MISSING"));

        Assert.Null(value);
    }

    [Fact]
    public async Task GetAsync_ReturnsValue_WhenVariableSet()
    {
        var name = UniqueVar("PRESENT");
        Environment.SetEnvironmentVariable(name, "fake-test-value-only");
        try
        {
            var store = new EnvironmentSecretStore();
            var value = await store.GetAsync(name);

            Assert.Equal("fake-test-value-only", value);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public async Task ExistsAsync_TracksPresence()
    {
        var name = UniqueVar("EXISTS");
        var store = new EnvironmentSecretStore();

        Assert.False(await store.ExistsAsync(name));

        Environment.SetEnvironmentVariable(name, "x-test-only");
        try
        {
            Assert.True(await store.ExistsAsync(name));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public async Task SetAsync_Fails_BecauseStoreIsReadOnly()
    {
        var store = new EnvironmentSecretStore();
        var result = await store.SetAsync(UniqueVar("WRITE"), "fake-test-value");

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task DeleteAsync_Fails_BecauseStoreIsReadOnly()
    {
        var store = new EnvironmentSecretStore();
        var result = await store.DeleteAsync(UniqueVar("DELETE"));

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void SourceName_IsEnvironment()
    {
        Assert.Equal("Environment", new EnvironmentSecretStore().SourceName);
    }
}
