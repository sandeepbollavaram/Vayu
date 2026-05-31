using System.Net;
using System.Net.Http;
using System.Text;

using Vayu.AI.Gemini;
using Vayu.AI.Online;

namespace Vayu.AI.Gemini.Tests;

public class GeminiTestKeyTests
{
    private const string FakeKey = "fake-provider-token-for-tests";

    [Fact]
    public async Task TestKey_CancelConsent_DoesNotCallHttp()
    {
        var (provider, handler) = NewProvider(keyConfigured: true);

        var result = await provider.TestKeyAsync(CloudConsentDecision.Cancel);

        Assert.False(result.Success);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task TestKey_UseLocalInstead_DoesNotCallHttp()
    {
        var (provider, handler) = NewProvider(keyConfigured: true);

        var result = await provider.TestKeyAsync(CloudConsentDecision.UseLocalInstead);

        Assert.False(result.Success);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task TestKey_NoKey_DoesNotCallHttp_EvenWithAllowOnce()
    {
        var (provider, handler) = NewProvider(keyConfigured: false);

        var result = await provider.TestKeyAsync(CloudConsentDecision.AllowOnce);

        Assert.False(result.Success);
        Assert.Equal(0, handler.CallCount);
        Assert.Contains("key", result.Message);
    }

    [Fact]
    public async Task TestKey_AllowOnce_Success_CallsHttpOnce()
    {
        var (provider, handler) = NewProvider(keyConfigured: true, _ => Ok());

        var result = await provider.TestKeyAsync(CloudConsentDecision.AllowOnce);

        Assert.True(result.Success);
        Assert.Equal(1, handler.CallCount);
        Assert.Contains("succeeded", result.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "rejected")]
    [InlineData(HttpStatusCode.Forbidden, "rejected")]
    public async Task TestKey_AuthFailures_AreSafe(HttpStatusCode code, string expected)
    {
        var (provider, _) = NewProvider(keyConfigured: true, _ => new HttpResponseMessage(code));

        var result = await provider.TestKeyAsync(CloudConsentDecision.AllowOnce);

        Assert.False(result.Success);
        Assert.Contains(expected, result.Message);
    }

    [Fact]
    public async Task TestKey_RateLimited_IsSafe()
    {
        var (provider, _) = NewProvider(keyConfigured: true, _ => new HttpResponseMessage((HttpStatusCode)429));

        var result = await provider.TestKeyAsync(CloudConsentDecision.AllowOnce);

        Assert.False(result.Success);
        Assert.Contains("rate limited", result.Message);
    }

    [Fact]
    public async Task TestKey_ServerError_IsSafe()
    {
        var (provider, _) = NewProvider(keyConfigured: true, _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await provider.TestKeyAsync(CloudConsentDecision.AllowOnce);

        Assert.False(result.Success);
        Assert.Contains("server error", result.Message);
    }

    [Fact]
    public async Task TestKey_Transport_IsSafe()
    {
        var (provider, _) = NewProvider(keyConfigured: true, _ => throw new HttpRequestException("nope"));

        var result = await provider.TestKeyAsync(CloudConsentDecision.AllowOnce);

        Assert.False(result.Success);
        Assert.Contains("could not reach", result.Message);
    }

    [Fact]
    public async Task TestKey_NeverLeaksKey_InMessage()
    {
        var (provider, _) = NewProvider(keyConfigured: true, _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await provider.TestKeyAsync(CloudConsentDecision.AllowOnce);

        Assert.DoesNotContain(FakeKey, result.Message);
        Assert.DoesNotContain(FakeKey, $"{result}");
    }

    // ---- helpers ----

    private static (GeminiProvider provider, CountingHandler handler) NewProvider(
        bool keyConfigured,
        Func<HttpRequestMessage, HttpResponseMessage>? responder = null)
    {
        var handler = new CountingHandler(responder ?? (_ => Ok()));
        var client = new HttpClient(handler);
        var provider = new GeminiProvider(client, new StubResolver(keyConfigured), new GeminiProviderOptions());
        return (provider, handler);
    }

    private static HttpResponseMessage Ok()
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"candidates":[{"content":{"parts":[{"text":"pong"}]}}]}""", Encoding.UTF8, "application/json"),
        };

    private sealed class StubResolver : IGeminiKeyResolver
    {
        private readonly bool _configured;
        public StubResolver(bool configured) => _configured = configured;

        public Task<GeminiKeyResolutionResult> ResolveAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_configured
                ? GeminiKeyResolutionResult.Configured(OnlineProviderKeySource.EnvironmentVariable, FakeKey)
                : GeminiKeyResolutionResult.NotConfigured());
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public int CallCount { get; private set; }

        public CountingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            try
            {
                return Task.FromResult(_responder(request));
            }
            catch (HttpRequestException ex)
            {
                return Task.FromException<HttpResponseMessage>(ex);
            }
        }
    }
}
