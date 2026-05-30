namespace Vayu.Security.Tests;

public class SecureConfigServiceTests
{
    [Fact]
    public async Task GetGeminiKeyStatus_ReturnsNotConfigured_WhenAllStoresMissing()
    {
        var svc = new SecureConfigService(
            new FakeStore("WindowsCredentialManager"),
            new FakeStore("Environment"),
            new FakeStore("EncryptedJson"));

        var status = await svc.GetGeminiKeyStatusAsync();

        Assert.False(status.IsConfigured);
        Assert.Null(status.Source);
    }

    [Fact]
    public async Task GetGeminiKeyStatus_PrefersCredentialManager_WhenAllHaveKey()
    {
        var svc = new SecureConfigService(
            new FakeStore("WindowsCredentialManager", credentialName: SecureConfigService.GeminiApiKeyCredentialName, value: "fake-cred"),
            new FakeStore("Environment", credentialName: SecureConfigService.GeminiApiKeyEnvironmentVariable, value: "fake-env"),
            new FakeStore("EncryptedJson", credentialName: SecureConfigService.GeminiApiKeyCredentialName, value: "fake-enc"));

        var status = await svc.GetGeminiKeyStatusAsync();

        Assert.True(status.IsConfigured);
        Assert.Equal("WindowsCredentialManager", status.Source);
    }

    [Fact]
    public async Task GetGeminiKeyStatus_FallsBackToEnvironment_WhenCredentialManagerEmpty()
    {
        var svc = new SecureConfigService(
            new FakeStore("WindowsCredentialManager"),
            new FakeStore("Environment", credentialName: SecureConfigService.GeminiApiKeyEnvironmentVariable, value: "fake-env"),
            new FakeStore("EncryptedJson", credentialName: SecureConfigService.GeminiApiKeyCredentialName, value: "fake-enc"));

        var status = await svc.GetGeminiKeyStatusAsync();

        Assert.True(status.IsConfigured);
        Assert.Equal("Environment", status.Source);
    }

    [Fact]
    public async Task GetGeminiKeyStatus_FallsBackToEncryptedJson_WhenOthersEmpty()
    {
        var svc = new SecureConfigService(
            new FakeStore("WindowsCredentialManager"),
            new FakeStore("Environment"),
            new FakeStore("EncryptedJson", credentialName: SecureConfigService.GeminiApiKeyCredentialName, value: "fake-enc"));

        var status = await svc.GetGeminiKeyStatusAsync();

        Assert.True(status.IsConfigured);
        Assert.Equal("EncryptedJson", status.Source);
    }

    [Fact]
    public async Task ReadGeminiKey_ReturnsNull_WhenAllStoresMissing()
    {
        var svc = new SecureConfigService(
            new FakeStore("WindowsCredentialManager"),
            new FakeStore("Environment"),
            new FakeStore("EncryptedJson"));

        var value = await svc.ReadGeminiKeyAsync();

        Assert.Null(value);
    }

    [Fact]
    public async Task ReadGeminiKey_FollowsSamePriority_AsStatus()
    {
        var svc = new SecureConfigService(
            new FakeStore("WindowsCredentialManager"),
            new FakeStore("Environment", credentialName: SecureConfigService.GeminiApiKeyEnvironmentVariable, value: "from-env"),
            new FakeStore("EncryptedJson", credentialName: SecureConfigService.GeminiApiKeyCredentialName, value: "from-enc"));

        var value = await svc.ReadGeminiKeyAsync();

        Assert.Equal("from-env", value);
    }

    [Fact]
    public void Constructor_Throws_OnNullStore()
    {
        var fake = new FakeStore("ok");

        Assert.Throws<ArgumentNullException>(() => new SecureConfigService(null!, fake, fake));
        Assert.Throws<ArgumentNullException>(() => new SecureConfigService(fake, null!, fake));
        Assert.Throws<ArgumentNullException>(() => new SecureConfigService(fake, fake, null!));
    }

    /// <summary>
    /// Minimal in-memory <see cref="ISecretStore"/> used to drive priority
    /// tests without touching the OS. Holds at most one named secret.
    /// </summary>
    private sealed class FakeStore(string source, string? credentialName = null, string? value = null) : ISecretStore
    {
        public string SourceName => source;

        public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(credentialName is not null && name == credentialName ? value : null);

        public Task<SecretStoreResult> SetAsync(string name, string val, CancellationToken cancellationToken = default)
            => Task.FromResult(SecretStoreResult.Ok);

        public Task<SecretStoreResult> DeleteAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(SecretStoreResult.Ok);

        public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(credentialName is not null && name == credentialName && !string.IsNullOrEmpty(value));
    }
}
