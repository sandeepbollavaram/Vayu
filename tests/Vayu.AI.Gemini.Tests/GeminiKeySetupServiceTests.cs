using Vayu.AI.Gemini;
using Vayu.AI.Online;
using Vayu.Security;

namespace Vayu.AI.Gemini.Tests;

public class GeminiKeySetupServiceTests
{
    // Deliberately not key-shaped — never trips secret scanners.
    private const string FakeKey = "fake-provider-token-for-tests";

    [Fact]
    public async Task Missing_ReturnsNotConfigured()
    {
        var svc = NewService(out _, out _, out _);

        var status = await svc.GetKeyStatusAsync();

        Assert.False(status.IsConfigured);
        Assert.Equal(OnlineProviderKeySource.NotConfigured, status.Source);
        Assert.False(await svc.IsKeyConfiguredAsync());
    }

    [Fact]
    public async Task Save_WritesToCredentialStore()
    {
        var svc = NewService(out var cred, out _, out _);

        await svc.SaveKeyAsync(FakeKey);

        Assert.True(await cred.ExistsAsync(SecureConfigService.GeminiApiKeyCredentialName));
        var status = await svc.GetKeyStatusAsync();
        Assert.True(status.IsConfigured);
        Assert.Equal(OnlineProviderKeySource.WindowsCredentialManager, status.Source);
    }

    [Fact]
    public async Task Save_BlankKey_Rejected()
    {
        var svc = NewService(out _, out _, out _);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => svc.SaveKeyAsync("   "));
    }

    [Fact]
    public async Task Save_FallsBackToEncrypted_WhenCredentialStoreFails()
    {
        var cred = new FakeStore("WindowsCredentialManager", writable: false);
        var env = new FakeStore("Environment", writable: false);
        var enc = new FakeStore("EncryptedJson", writable: true);
        var svc = new GeminiKeySetupService(cred, env, enc);

        await svc.SaveKeyAsync(FakeKey);

        Assert.False(await cred.ExistsAsync(SecureConfigService.GeminiApiKeyCredentialName));
        Assert.True(await enc.ExistsAsync(SecureConfigService.GeminiApiKeyCredentialName));
        Assert.Equal(OnlineProviderKeySource.DpapiEncryptedConfig, (await svc.GetKeyStatusAsync()).Source);
    }

    [Fact]
    public async Task Remove_DeletesFromWritableStores()
    {
        var svc = NewService(out var cred, out _, out var enc);
        await svc.SaveKeyAsync(FakeKey);

        await svc.RemoveKeyAsync();

        Assert.False(await cred.ExistsAsync(SecureConfigService.GeminiApiKeyCredentialName));
        Assert.False(await enc.ExistsAsync(SecureConfigService.GeminiApiKeyCredentialName));
        Assert.False(await svc.IsKeyConfiguredAsync());
    }

    [Fact]
    public async Task EnvKey_ReportsConfigured_ButCannotBeRemoved()
    {
        var cred = new FakeStore("WindowsCredentialManager", writable: true);
        var env = new FakeStore("Environment", writable: false);
        await env.ForceSeedAsync(SecureConfigService.GeminiApiKeyEnvironmentVariable, FakeKey);
        var enc = new FakeStore("EncryptedJson", writable: true);
        var svc = new GeminiKeySetupService(cred, env, enc);

        var status = await svc.GetKeyStatusAsync();
        Assert.True(status.IsConfigured);
        Assert.Equal(OnlineProviderKeySource.EnvironmentVariable, status.Source);
        Assert.False(await svc.CanRemoveKeyAsync());

        // Remove is a no-op against the env store; status stays configured-via-env.
        await svc.RemoveKeyAsync();
        Assert.Equal(OnlineProviderKeySource.EnvironmentVariable, (await svc.GetKeyStatusAsync()).Source);
    }

    [Fact]
    public async Task Status_NeverExposesKeyValue()
    {
        var svc = NewService(out _, out _, out _);
        await svc.SaveKeyAsync(FakeKey);

        var status = await svc.GetKeyStatusAsync();

        Assert.DoesNotContain(FakeKey, $"{status}");
        Assert.DoesNotContain(FakeKey, status.Message ?? string.Empty);
    }

    [Fact]
    public async Task Validate_IsNotSupported_UntilM34()
    {
        var svc = NewService(out _, out _, out _);

        await Assert.ThrowsAsync<NotSupportedException>(() => svc.ValidateKeyAsync());
    }

    [Fact]
    public void Constructor_Throws_OnNullStore()
    {
        var ok = new FakeStore("x", true);
        Assert.Throws<ArgumentNullException>(() => new GeminiKeySetupService(null!, ok, ok));
        Assert.Throws<ArgumentNullException>(() => new GeminiKeySetupService(ok, null!, ok));
        Assert.Throws<ArgumentNullException>(() => new GeminiKeySetupService(ok, ok, null!));
    }

    // ---- helpers ----

    private static GeminiKeySetupService NewService(out FakeStore cred, out FakeStore env, out FakeStore enc)
    {
        cred = new FakeStore("WindowsCredentialManager", writable: true);
        env = new FakeStore("Environment", writable: false);
        enc = new FakeStore("EncryptedJson", writable: true);
        return new GeminiKeySetupService(cred, env, enc);
    }

    private sealed class FakeStore : ISecretStore
    {
        private readonly bool _writable;
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public FakeStore(string sourceName, bool writable)
        {
            SourceName = sourceName;
            _writable = writable;
        }

        public string SourceName { get; }

        public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_values.TryGetValue(name, out var v) ? v : null);

        public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(_values.ContainsKey(name));

        public Task<SecretStoreResult> SetAsync(string name, string value, CancellationToken cancellationToken = default)
        {
            if (!_writable)
            {
                return Task.FromResult(SecretStoreResult.Failure($"{SourceName} is read-only."));
            }
            _values[name] = value;
            return Task.FromResult(SecretStoreResult.Ok);
        }

        public Task<SecretStoreResult> DeleteAsync(string name, CancellationToken cancellationToken = default)
        {
            if (_writable)
            {
                _values.Remove(name);
            }
            return Task.FromResult(SecretStoreResult.Ok);
        }

        /// <summary>Test helper to seed a read-only store (e.g. simulate an env var).</summary>
        public Task ForceSeedAsync(string name, string value)
        {
            _values[name] = value;
            return Task.CompletedTask;
        }
    }
}
