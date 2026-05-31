using Vayu.AI.Gemini;
using Vayu.AI.Online;
using Vayu.Security;

namespace Vayu.AI.Gemini.Tests;

public class GeminiKeyResolverTests
{
    // Deliberately not key-shaped, so security scanners never flag it.
    private const string FakeKey = "fake-provider-token-for-tests";

    [Fact]
    public async Task Resolve_MissingKey_ReturnsNotConfigured()
    {
        var resolver = new GeminiKeyResolver(NewSecureConfig(credential: null, env: null, encrypted: null));

        var result = await resolver.ResolveAsync();

        Assert.False(result.IsConfigured);
        Assert.Equal(OnlineProviderKeySource.NotConfigured, result.Source);
        Assert.Null(result.KeyValue);
    }

    [Fact]
    public async Task Resolve_EnvKey_ReturnsConfigured_FromEnvironment()
    {
        var resolver = new GeminiKeyResolver(NewSecureConfig(credential: null, env: FakeKey, encrypted: null));

        var result = await resolver.ResolveAsync();

        Assert.True(result.IsConfigured);
        Assert.Equal(OnlineProviderKeySource.EnvironmentVariable, result.Source);
    }

    [Fact]
    public async Task Resolve_CredentialManager_PreferredOverEnv()
    {
        var resolver = new GeminiKeyResolver(NewSecureConfig(credential: FakeKey, env: FakeKey, encrypted: null));

        var result = await resolver.ResolveAsync();

        Assert.Equal(OnlineProviderKeySource.WindowsCredentialManager, result.Source);
    }

    [Fact]
    public async Task KeyStatus_NeverExposesKeyValue()
    {
        var resolver = new GeminiKeyResolver(NewSecureConfig(credential: null, env: FakeKey, encrypted: null));

        var result = await resolver.ResolveAsync();
        var status = result.ToKeyStatus("gemini");

        Assert.True(status.IsConfigured);
        // The public status object carries no key value at all.
        var rendered = $"{status}";
        Assert.DoesNotContain(FakeKey, rendered);
    }

    [Fact]
    public async Task ResolutionResult_ToString_RedactsKey()
    {
        var resolver = new GeminiKeyResolver(NewSecureConfig(credential: null, env: FakeKey, encrypted: null));

        var result = await resolver.ResolveAsync();

        Assert.DoesNotContain(FakeKey, result.ToString());
        Assert.Contains("redacted", result.ToString());
    }

    // ---- helpers ----

    private static SecureConfigService NewSecureConfig(string? credential, string? env, string? encrypted)
        => new(
            new FakeStore("WindowsCredentialManager", SecureConfigService.GeminiApiKeyCredentialName, credential),
            new FakeStore("Environment", SecureConfigService.GeminiApiKeyEnvironmentVariable, env),
            new FakeStore("EncryptedJson", SecureConfigService.GeminiApiKeyCredentialName, encrypted));

    private sealed class FakeStore : ISecretStore
    {
        private readonly string _name;
        private readonly string? _value;

        public FakeStore(string sourceName, string name, string? value)
        {
            SourceName = sourceName;
            _name = name;
            _value = value;
        }

        public string SourceName { get; }

        public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Equals(name, _name, StringComparison.Ordinal) ? _value : null);

        public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Equals(name, _name, StringComparison.Ordinal) && !string.IsNullOrEmpty(_value));

        public Task<SecretStoreResult> SetAsync(string name, string value, CancellationToken cancellationToken = default)
            => Task.FromResult(SecretStoreResult.Ok);

        public Task<SecretStoreResult> DeleteAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(SecretStoreResult.Ok);
    }
}
