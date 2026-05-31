using System.Net;
using System.Net.Http;
using System.Text;

using Vayu.AI.Local;

namespace Vayu.AI.Local.Tests;

public class OllamaModelPullServiceTests
{
    [Fact]
    public async Task PullModelAsync_RejectsEmptyTag()
    {
        var (svc, _) = NewServiceWithRequests();
        var progress = new ListProgress();

        var result = await svc.PullModelAsync(new OllamaModelPullRequest(""), progress);

        Assert.False(result.IsComplete);
        Assert.Contains("Refused", result.ErrorMessage);
        var only = Assert.Single(progress.Updates);
        Assert.Equal("rejected", only.Status);
    }

    [Fact]
    public async Task PullModelAsync_RejectsUnknownTag()
    {
        var (svc, captured) = NewServiceWithRequests();

        var result = await svc.PullModelAsync(new OllamaModelPullRequest("mistral-nemo:12b"));

        Assert.False(result.IsComplete);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(captured); // Service must not hit Ollama for unknown tags.
    }

    [Fact]
    public async Task PullModelAsync_SendsPostToApiPull_WithKnownTag()
    {
        var capturedBodies = new List<string>();
        var (svc, captured) = NewServiceWithRequests(
            JsonOk("""{"status":"success"}""", streaming: false),
            capturedBodies);

        var result = await svc.PullModelAsync(new OllamaModelPullRequest("gemma3:1b", StreamProgress: false));

        Assert.True(result.IsComplete);
        var only = Assert.Single(captured);
        Assert.Equal(HttpMethod.Post, only.Method);
        Assert.EndsWith("/api/pull", only.RequestUri!.AbsolutePath);
        Assert.Contains("\"model\":\"gemma3:1b\"", Assert.Single(capturedBodies));
    }

    [Fact]
    public async Task PullModelAsync_ParsesStreamingProgress()
    {
        const string body = """
        {"status":"pulling manifest"}
        {"status":"downloading","completed":250,"total":1000,"digest":"sha256:a"}
        {"status":"downloading","completed":1000,"total":1000,"digest":"sha256:a"}
        {"status":"verifying sha256 digest"}
        {"status":"success"}
        """;
        var (svc, _) = NewServiceWithRequests(JsonOk(body, streaming: true));
        var progress = new ListProgress();

        var result = await svc.PullModelAsync(new OllamaModelPullRequest("gemma3:1b"), progress);

        Assert.True(result.IsComplete);
        Assert.Equal("success", result.Status);
        Assert.Equal(1000, result.CompletedBytes);
        Assert.Equal(1000, result.TotalBytes);
        // Initial "starting", four real status updates, plus a final IsComplete tick.
        Assert.Contains(progress.Updates, p => p.Status == "downloading" && p.CompletedBytes == 250);
        Assert.Contains(progress.Updates, p => p.Percent is { } v && v >= 99.0);
        Assert.Equal(progress.Updates[^1], result);
    }

    [Fact]
    public async Task PullModelAsync_HandlesHttp500_Safely()
    {
        var (svc, _) = NewServiceWithRequests(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var progress = new ListProgress();

        var result = await svc.PullModelAsync(new OllamaModelPullRequest("gemma3:1b"), progress);

        Assert.False(result.IsComplete);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("HTTP 500", result.ErrorMessage);
        Assert.Equal(result, progress.Updates[^1]);
    }

    [Fact]
    public async Task PullModelAsync_SkipsMalformedProgressLines()
    {
        // The third line is malformed — the stream must keep going.
        const string body = """
        {"status":"pulling manifest"}
        {"status":"downloading","completed":500,"total":1000}
        this is not json
        {"status":"success"}
        """;
        var (svc, _) = NewServiceWithRequests(JsonOk(body, streaming: true));

        var result = await svc.PullModelAsync(new OllamaModelPullRequest("gemma3:1b"));

        Assert.True(result.IsComplete);
        Assert.Equal("success", result.Status);
    }

    [Fact]
    public async Task PullModelAsync_SurfacesOllamaError_FromStream()
    {
        const string body = """
        {"status":"pulling manifest"}
        {"error":"manifest fetch failed"}
        """;
        var (svc, _) = NewServiceWithRequests(JsonOk(body, streaming: true));

        var result = await svc.PullModelAsync(new OllamaModelPullRequest("gemma3:1b"));

        Assert.False(result.IsComplete);
        Assert.Equal("manifest fetch failed", result.ErrorMessage);
    }

    [Fact]
    public async Task PullModelAsync_Cancellation_StopsStreamAndReportsCancelled()
    {
        using var cts = new CancellationTokenSource();
        // Handler completes the response normally; we cancel before the stream starts.
        var (svc, _) = NewServiceWithRequests(JsonOk("""{"status":"pulling manifest"}""", streaming: true));
        cts.Cancel();
        var progress = new ListProgress();

        var result = await svc.PullModelAsync(new OllamaModelPullRequest("gemma3:1b"), progress, cts.Token);

        Assert.True(result.IsCancelled);
        Assert.False(result.IsComplete);
        Assert.Contains(progress.Updates, p => p.IsCancelled);
    }

    [Fact]
    public async Task PullModelAsync_NonStreaming_MalformedBody_ReportsError()
    {
        var (svc, _) = NewServiceWithRequests(JsonOk("not actually json", streaming: false));

        var result = await svc.PullModelAsync(new OllamaModelPullRequest("gemma3:1b", StreamProgress: false));

        Assert.False(result.IsComplete);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Malformed", result.ErrorMessage);
    }

    [Fact]
    public void Constructor_Throws_OnNullHttpClient()
    {
        Assert.Throws<ArgumentNullException>(() => new OllamaModelPullService(null!));
    }

    // ---- helpers ----

    private static (OllamaModelPullService svc, List<HttpRequestMessage> captured) NewServiceWithRequests(
        HttpResponseMessage? response = null,
        List<string>? capturedBodies = null)
    {
        var captured = new List<HttpRequestMessage>();
        var handler = new RecordingHandler(
            response ?? JsonOk("""{"status":"success"}""", streaming: false),
            captured,
            capturedBodies);
        var client = new HttpClient(handler);
        var svc = new OllamaModelPullService(client);
        return (svc, captured);
    }

    private static HttpResponseMessage JsonOk(string body, bool streaming)
    {
        var content = streaming
            ? new StringContent(body, Encoding.UTF8, "application/x-ndjson")
            : new StringContent(body, Encoding.UTF8, "application/json");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private sealed class ListProgress : IProgress<OllamaModelPullProgress>
    {
        public List<OllamaModelPullProgress> Updates { get; } = new();
        public void Report(OllamaModelPullProgress value) => Updates.Add(value);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        private readonly List<HttpRequestMessage> _captured;
        private readonly List<string>? _capturedBodies;

        public RecordingHandler(
            HttpResponseMessage response,
            List<HttpRequestMessage> captured,
            List<string>? capturedBodies)
        {
            _response = response;
            _captured = captured;
            _capturedBodies = capturedBodies;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _captured.Add(request);
            if (_capturedBodies is not null && request.Content is not null)
            {
                _capturedBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            }
            return _response;
        }
    }
}
