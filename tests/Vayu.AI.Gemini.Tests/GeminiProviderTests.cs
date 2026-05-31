using System.Net;
using System.Net.Http;
using System.Text;

using Vayu.AI.Gemini;
using Vayu.AI.Online;
using Vayu.Core;

namespace Vayu.AI.Gemini.Tests;

public class GeminiProviderTests
{
    private const string FakeKey = "fake-provider-token-for-tests";

    [Fact]
    public async Task GetKeyStatus_NotConfigured_WhenNoKey()
    {
        var (provider, _) = NewProvider(keyConfigured: false);

        var status = await provider.GetKeyStatusAsync();

        Assert.False(status.IsConfigured);
        Assert.Equal("gemini", status.ProviderId);
    }

    [Fact]
    public async Task Plan_CancelConsent_DoesNotCallHttp()
    {
        var (provider, handler) = NewProvider(keyConfigured: true, ModelJson("""{"intent":"unknown","confidence":0.9}"""));

        var result = await provider.PlanAsync(Request("open notepad"), CloudConsentDecision.Cancel);

        Assert.False(result.Success);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Plan_UseLocalInstead_DoesNotCallHttp()
    {
        var (provider, handler) = NewProvider(keyConfigured: true, ModelJson("""{"intent":"unknown","confidence":0.9}"""));

        var result = await provider.PlanAsync(Request("open notepad"), CloudConsentDecision.UseLocalInstead);

        Assert.False(result.Success);
        Assert.Equal(0, handler.CallCount);
        Assert.Contains("local model", result.ErrorMessage);
    }

    [Fact]
    public async Task Plan_NoKey_DoesNotCallHttp_EvenWithAllowOnce()
    {
        var (provider, handler) = NewProvider(keyConfigured: false, ModelJson("""{"intent":"unknown","confidence":0.9}"""));

        var result = await provider.PlanAsync(Request("open notepad"), CloudConsentDecision.AllowOnce);

        Assert.False(result.Success);
        Assert.Equal(0, handler.CallCount);
        Assert.Contains("key", result.ErrorMessage);
    }

    [Fact]
    public async Task Plan_AllowOnce_ValidJson_ReturnsAppOpen()
    {
        var (provider, handler) = NewProvider(keyConfigured: true,
            ModelJson("""{"intent":"app.open","confidence":0.9,"args":{"app":"notepad"}}"""));

        var result = await provider.PlanAsync(Request("open notepad"), CloudConsentDecision.AllowOnce);

        Assert.True(result.Success);
        Assert.Equal("app.open", result.IntentPlan!.Intent);
        Assert.Equal("notepad", result.IntentPlan.Args["app"]);
        Assert.Equal("gemini", result.ProviderId);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Plan_AllowOnce_ShowLogs_IsL0()
    {
        var (provider, _) = NewProvider(keyConfigured: true,
            ModelJson("""{"intent":"ui.show_logs","confidence":0.9}"""));

        var result = await provider.PlanAsync(Request("show logs"), CloudConsentDecision.AllowOnce);

        Assert.True(result.Success);
        Assert.Equal(RiskLevel.L0, result.IntentPlan!.Risk);
    }

    [Fact]
    public async Task Plan_DisallowedIntent_FailsSafely()
    {
        var (provider, _) = NewProvider(keyConfigured: true,
            ModelJson("""{"intent":"shell.run","confidence":0.99,"args":{"app":"rm"}}"""));

        var result = await provider.PlanAsync(Request("delete everything"), CloudConsentDecision.AllowOnce);

        Assert.False(result.Success);
        Assert.Contains("non-allowlisted", result.ErrorMessage);
    }

    [Fact]
    public async Task Plan_TypingRequest_FlaggedForM5()
    {
        var (provider, _) = NewProvider(keyConfigured: true,
            ModelJson("""{"intent":"app.open","confidence":0.9,"args":{"app":"notepad","typingRequested":true}}"""));

        var result = await provider.PlanAsync(Request("open notepad and write hi"), CloudConsentDecision.AllowOnce);

        Assert.True(result.Success);
        Assert.Equal("true", result.IntentPlan!.Args[GeminiPlanJsonParser.TypingRequestedArgKey]);
    }

    [Fact]
    public async Task Plan_MalformedJson_FailsSafely()
    {
        var (provider, _) = NewProvider(keyConfigured: true, ModelJson("not json at all"));

        var result = await provider.PlanAsync(Request("open notepad"), CloudConsentDecision.AllowOnce);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Plan_Http500_FailsSafely()
    {
        var (provider, _) = NewProvider(keyConfigured: true, _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await provider.PlanAsync(Request("open notepad"), CloudConsentDecision.AllowOnce);

        Assert.False(result.Success);
        Assert.Contains("HTTP 500", result.ErrorMessage);
    }

    [Fact]
    public async Task Plan_LowConfidence_FailsSafely()
    {
        var (provider, _) = NewProvider(keyConfigured: true,
            ModelJson("""{"intent":"app.open","confidence":0.1,"args":{"app":"notepad"}}"""),
            options: new GeminiProviderOptions { MinimumConfidence = 0.6 });

        var result = await provider.PlanAsync(Request("open notepad"), CloudConsentDecision.AllowOnce);

        Assert.False(result.Success);
        Assert.Contains("below floor", result.ErrorMessage);
    }

    [Fact]
    public async Task Plan_NeverLeaksKey_InErrorMessage()
    {
        var (provider, _) = NewProvider(keyConfigured: true, _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await provider.PlanAsync(Request("open notepad"), CloudConsentDecision.AllowOnce);

        Assert.False(result.Success);
        Assert.DoesNotContain(FakeKey, result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public void Constructor_Throws_OnNullHttpClient()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GeminiProvider(null!, new StubResolver(false), new GeminiProviderOptions()));
    }

    // ---- helpers ----

    private static CommandRequest Request(string text) => new() { Text = text, Source = "text" };

    private static (GeminiProvider provider, CountingHandler handler) NewProvider(
        bool keyConfigured,
        Func<HttpRequestMessage, HttpResponseMessage>? responder = null,
        GeminiProviderOptions? options = null)
    {
        var handler = new CountingHandler(responder ?? (_ => ModelOk("""{"intent":"unknown","confidence":0.9}""")));
        var client = new HttpClient(handler);
        var resolver = new StubResolver(keyConfigured);
        var provider = new GeminiProvider(client, resolver, options ?? new GeminiProviderOptions());
        return (provider, handler);
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> ModelJson(string modelText)
        => _ => ModelOk(modelText);

    private static HttpResponseMessage ModelOk(string modelText)
    {
        // Wrap modelText as Gemini's candidates[0].content.parts[0].text.
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new { content = new { parts = new[] { new { text = modelText } } } },
            },
        });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
    }

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
            return Task.FromResult(_responder(request));
        }
    }
}
