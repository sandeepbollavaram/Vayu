using System.Net;
using System.Net.Http;
using System.Text;

using Vayu.AI.Local;

namespace Vayu.AI.Local.Tests;

public class OllamaRuntimeServiceTests
{
    // Ollama's documented /api/tags shape. Three models — two curated, one unknown.
    private const string SampleTagsJson = """
    {
      "models": [
        { "name": "gemma3:4b",   "size": 3826793677 },
        { "name": "gemma3:1b",   "size":  815000000 },
        { "name": "llama3.2:3b", "size": 2100000000 },
        { "name": "mistral:7b",  "size": 4500000000 }
      ]
    }
    """;

    [Fact]
    public async Task IsReachableAsync_True_OnHttp200()
    {
        var service = NewService(_ => JsonOk("""{ "models": [] }"""));

        Assert.True(await service.IsReachableAsync());
    }

    [Fact]
    public async Task IsReachableAsync_False_OnHttp500()
    {
        var service = NewService(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        Assert.False(await service.IsReachableAsync());
    }

    [Fact]
    public async Task IsReachableAsync_False_OnTransportException()
    {
        var service = NewService(_ => throw new HttpRequestException("connection refused"));

        Assert.False(await service.IsReachableAsync());
    }

    [Fact]
    public async Task ListLocalModelsAsync_ParsesAllInstalledModels()
    {
        var service = NewService(_ => JsonOk(SampleTagsJson));

        var models = await service.ListLocalModelsAsync();

        Assert.Equal(4, models.Count);
        Assert.Contains(models, m => m.Tag == "gemma3:4b");
        Assert.Contains(models, m => m.Tag == "gemma3:1b");
        Assert.Contains(models, m => m.Tag == "llama3.2:3b");
        Assert.Contains(models, m => m.Tag == "mistral:7b");
    }

    [Theory]
    [InlineData("gemma3:4b",   "gemma3")]
    [InlineData("gemma3:1b",   "gemma3")]
    [InlineData("llama3.2:3b", "llama3.2")]
    public async Task ListLocalModelsAsync_SplitsFamilyName(string tag, string expectedFamily)
    {
        var service = NewService(_ => JsonOk($$"""{ "models": [ { "name": "{{tag}}", "size": 1 } ] }"""));

        var models = await service.ListLocalModelsAsync();

        var only = Assert.Single(models);
        Assert.Equal(expectedFamily, only.Name);
        Assert.Equal(tag, only.Tag);
        Assert.True(only.IsPresent);
    }

    [Fact]
    public async Task ListLocalModelsAsync_EmptyArray_ReturnsEmpty()
    {
        var service = NewService(_ => JsonOk("""{ "models": [] }"""));

        Assert.Empty(await service.ListLocalModelsAsync());
    }

    [Fact]
    public async Task ListLocalModelsAsync_Empty_On500()
    {
        var service = NewService(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        Assert.Empty(await service.ListLocalModelsAsync());
    }

    [Fact]
    public async Task ListLocalModelsAsync_Empty_OnMalformedJson()
    {
        var service = NewService(_ => JsonOk("not actually json"));

        Assert.Empty(await service.ListLocalModelsAsync());
    }

    [Fact]
    public async Task ListLocalModelsAsync_Empty_OnTransportException()
    {
        var service = NewService(_ => throw new HttpRequestException("connection refused"));

        Assert.Empty(await service.ListLocalModelsAsync());
    }

    [Fact]
    public async Task ListLocalModelsAsync_SkipsEntries_WithMissingName()
    {
        var service = NewService(_ => JsonOk("""
        {
          "models": [
            { "name": "gemma3:4b", "size": 1 },
            { "name": "",           "size": 2 },
            { "name": null,         "size": 3 }
          ]
        }
        """));

        var models = await service.ListLocalModelsAsync();

        var only = Assert.Single(models);
        Assert.Equal("gemma3:4b", only.Tag);
    }

    [Fact]
    public async Task IsInstalledAsync_False_WhenExecutableNotOnPath()
    {
        // Use a unique executable name nobody will have on PATH.
        var service = NewService(
            _ => JsonOk("""{ "models": [] }"""),
            options: new OllamaProviderOptions
            {
                ExecutableName = "vayu-fake-binary-" + Guid.NewGuid().ToString("N"),
            });

        Assert.False(await service.IsInstalledAsync());
    }

    [Fact]
    public async Task IsInstalledAsync_True_WhenExecutableFoundOnPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "vayu-runtime-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var execName = "vayu-test-bin-" + Guid.NewGuid().ToString("N");
        var fileName = OperatingSystem.IsWindows() ? execName + ".exe" : execName;
        File.WriteAllText(Path.Combine(tempDir, fileName), string.Empty);

        var originalPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        Environment.SetEnvironmentVariable("PATH", tempDir + Path.PathSeparator + originalPath);
        try
        {
            var service = NewService(
                _ => JsonOk("""{ "models": [] }"""),
                options: new OllamaProviderOptions { ExecutableName = execName });

            Assert.True(await service.IsInstalledAsync());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
            try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public async Task GetRuntimeStatusAsync_Healthy_ReportsRecommendedPresent()
    {
        var service = NewService(
            _ => JsonOk(SampleTagsJson),
            wingetProbe: _ => Task.FromResult(true));

        var status = await service.GetRuntimeStatusAsync();

        Assert.True(status.IsEndpointReachable);
        Assert.True(status.IsWingetPackageDetected);
        Assert.True(status.RecommendedModelPresent);
        Assert.Equal(4, status.InstalledModels.Count);
        Assert.Null(status.Message);
    }

    [Fact]
    public async Task GetRuntimeStatusAsync_ExecutableButServerDown_ReportsHelpfulMessage()
    {
        var service = NewService(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            wingetProbe: _ => Task.FromResult(true));

        var status = await service.GetRuntimeStatusAsync();

        Assert.False(status.IsEndpointReachable);
        Assert.True(status.IsWingetPackageDetected);
        Assert.False(status.RecommendedModelPresent);
        Assert.NotNull(status.Message);
        Assert.Contains("not running", status.Message);
    }

    [Fact]
    public async Task GetRuntimeStatusAsync_NothingDetected_ReportsNotDetected()
    {
        // Use a guaranteed-absent executable name so PATH cannot accidentally satisfy the probe
        // on a developer machine that already has Ollama installed.
        var service = NewService(
            _ => throw new HttpRequestException("nope"),
            options: new OllamaProviderOptions
            {
                ExecutableName = "vayu-absent-binary-" + Guid.NewGuid().ToString("N"),
            },
            wingetProbe: _ => Task.FromResult(false));

        var status = await service.GetRuntimeStatusAsync();

        Assert.False(status.IsExecutableAvailable);
        Assert.False(status.IsEndpointReachable);
        Assert.False(status.IsWingetPackageDetected);
        Assert.NotNull(status.Message);
        Assert.Contains("not detected", status.Message);
    }

    [Fact]
    public void Constructor_Throws_OnNullHttpClient()
    {
        Assert.Throws<ArgumentNullException>(() => new OllamaRuntimeService(null!));
    }

    // ---- helpers ----

    private static OllamaRuntimeService NewService(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        OllamaProviderOptions? options = null,
        Func<CancellationToken, Task<bool>>? wingetProbe = null)
    {
        var client = new HttpClient(new StubHandler(responder));
        return new OllamaRuntimeService(client, options, wingetProbe ?? (_ => Task.FromResult(false)));
    }

    private static HttpResponseMessage JsonOk(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
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
