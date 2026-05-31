using System.Net;
using System.Net.Http;
using System.Text;

using Vayu.AI.Local;
using Vayu.Core;

namespace Vayu.AI.Local.Tests;

public class OllamaIntentPlannerTests
{
    [Fact]
    public async Task PlanAsync_ValidModelJson_Succeeds()
    {
        // Ollama /api/generate wraps the model text in a "response" field.
        var planner = NewPlanner(GenerateResponse("""{"intent":"app.open","confidence":0.9,"args":{"app":"notepad"}}"""));

        var result = await planner.PlanAsync(Request("open notepad"));

        Assert.True(result.Success);
        Assert.Equal("app.open", result.IntentPlan!.Intent);
        Assert.Equal("notepad", result.IntentPlan.Args["app"]);
        Assert.Equal("ollama", result.ProviderName);
        Assert.StartsWith("ollama:", result.IntentPlan.PlanSource);
    }

    [Fact]
    public async Task PlanAsync_LowConfidence_Fails()
    {
        var planner = NewPlanner(
            GenerateResponse("""{"intent":"app.open","confidence":0.2,"args":{"app":"notepad"}}"""),
            planner: new LocalAiPlannerOptions { MinimumConfidence = 0.7 });

        var result = await planner.PlanAsync(Request("open notepad"));

        Assert.False(result.Success);
        Assert.Contains("below floor", result.ErrorMessage);
    }

    [Fact]
    public async Task PlanAsync_UnknownIntent_Succeeds_AsUnknown()
    {
        var planner = NewPlanner(GenerateResponse("""{"intent":"unknown","confidence":0.9}"""));

        var result = await planner.PlanAsync(Request("do a barrel roll"));

        Assert.True(result.Success);
        Assert.Equal("unknown", result.IntentPlan!.Intent);
        Assert.Equal(RiskLevel.L0, result.IntentPlan.Risk);
    }

    [Fact]
    public async Task PlanAsync_RiskyInventedIntent_Fails()
    {
        var planner = NewPlanner(GenerateResponse("""{"intent":"shell.run","confidence":0.99,"args":{"app":"rm"}}"""));

        var result = await planner.PlanAsync(Request("delete everything"));

        Assert.False(result.Success);
        Assert.Contains("non-allowlisted", result.ErrorMessage);
    }

    [Fact]
    public async Task PlanAsync_MalformedModelJson_FailsSafely()
    {
        var planner = NewPlanner(GenerateResponse("totally not json"));

        var result = await planner.PlanAsync(Request("open notepad"));

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task PlanAsync_Http500_ReturnsFailed()
    {
        var planner = NewPlanner(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await planner.PlanAsync(Request("open notepad"));

        Assert.False(result.Success);
        Assert.Contains("HTTP 500", result.ErrorMessage);
    }

    [Fact]
    public async Task PlanAsync_TransportException_ReturnsFailed_NotThrow()
    {
        var planner = NewPlannerThatThrows();

        var result = await planner.PlanAsync(Request("open notepad"));

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task PlanAsync_EmptyCommand_Fails()
    {
        var planner = NewPlanner(GenerateResponse("""{"intent":"unknown"}"""));

        var result = await planner.PlanAsync(Request("   "));

        Assert.False(result.Success);
        Assert.Contains("Empty", result.ErrorMessage);
    }

    [Fact]
    public async Task PlanAsync_CallerCancellation_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var planner = NewPlanner(GenerateResponse("""{"intent":"unknown","confidence":0.9}"""));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => planner.PlanAsync(Request("open notepad"), cts.Token));
    }

    [Fact]
    public void Constructor_Throws_OnNullHttpClient()
    {
        Assert.Throws<ArgumentNullException>(() => new OllamaIntentPlanner(null!));
    }

    // ---- helpers ----

    private static CommandRequest Request(string text) => new() { Text = text, Source = "text" };

    private static OllamaIntentPlanner NewPlanner(HttpResponseMessage response, LocalAiPlannerOptions? planner = null)
    {
        var client = new HttpClient(new StubHandler(_ => response));
        return new OllamaIntentPlanner(client, new OllamaProviderOptions(), planner ?? new LocalAiPlannerOptions());
    }

    private static OllamaIntentPlanner NewPlannerThatThrows()
    {
        var client = new HttpClient(new StubHandler(_ => throw new HttpRequestException("connection refused")));
        return new OllamaIntentPlanner(client);
    }

    private static HttpResponseMessage GenerateResponse(string modelText)
    {
        // Encode the model text as the value of Ollama's "response" field.
        var payload = System.Text.Json.JsonSerializer.Serialize(new { response = modelText, done = true });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
