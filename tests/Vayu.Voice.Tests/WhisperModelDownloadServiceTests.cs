using System.Net;

using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class WhisperModelDownloadServiceTests : IDisposable
{
    private readonly string _tempDir;

    public WhisperModelDownloadServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"vayu-dl-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    private static WhisperModelInfo Model => WhisperModelCatalog.Recommended;

    [Fact]
    public async Task Download_Success_SavesFile_AndLeavesNoPartial()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var service = new WhisperModelDownloadService(
            new HttpClient(new StubHandler(HttpStatusCode.OK, payload)));

        var result = await service.DownloadAsync(Model, _tempDir);

        Assert.Equal(ModelDownloadStatus.Completed, result.Status);
        Assert.NotNull(result.FilePath);
        Assert.True(File.Exists(result.FilePath!));
        Assert.Equal(payload, await File.ReadAllBytesAsync(result.FilePath!));
        Assert.False(File.Exists(result.FilePath + ".part")); // partial cleaned/moved
    }

    [Fact]
    public async Task Download_HttpError_Fails_AndCleansPartial()
    {
        var service = new WhisperModelDownloadService(
            new HttpClient(new StubHandler(HttpStatusCode.NotFound, Array.Empty<byte>())));

        var result = await service.DownloadAsync(Model, _tempDir);

        Assert.Equal(ModelDownloadStatus.Failed, result.Status);
        Assert.Null(result.FilePath);
        Assert.Empty(PartFiles());
    }

    [Fact]
    public async Task Download_Cancelled_Cleanup()
    {
        using var cts = new CancellationTokenSource();
        var service = new WhisperModelDownloadService(
            new HttpClient(new StubHandler(HttpStatusCode.OK, new byte[4096], cts)));

        var result = await service.DownloadAsync(Model, _tempDir, progress: null, cts.Token);

        Assert.Equal(ModelDownloadStatus.Cancelled, result.Status);
        Assert.Empty(PartFiles());
        Assert.False(File.Exists(Path.Combine(_tempDir, Model.FileName)));
    }

    [Fact]
    public async Task Download_BlockedUrl_FailsWithoutNetwork()
    {
        var blocked = Model with { DownloadUrl = "https://evil.example.com/x.bin" };
        var handler = new StubHandler(HttpStatusCode.OK, new byte[] { 9 });
        var service = new WhisperModelDownloadService(new HttpClient(handler));

        var result = await service.DownloadAsync(blocked, _tempDir);

        Assert.Equal(ModelDownloadStatus.Failed, result.Status);
        Assert.Equal(0, handler.Calls); // never hit the network
    }

    private IEnumerable<string> PartFiles()
        => Directory.Exists(_tempDir)
            ? Directory.EnumerateFiles(_tempDir, "*.part")
            : Array.Empty<string>();

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly byte[] _body;
        private readonly CancellationTokenSource? _cancelDuringRead;

        public StubHandler(HttpStatusCode status, byte[] body, CancellationTokenSource? cancelDuringRead = null)
        {
            _status = status;
            _body = body;
            _cancelDuringRead = cancelDuringRead;
        }

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            HttpContent content = _cancelDuringRead is null
                ? new ByteArrayContent(_body)
                : new StreamContent(new CancellingStream(_body, _cancelDuringRead));
            return Task.FromResult(new HttpResponseMessage(_status) { Content = content });
        }
    }

    // A stream that triggers cancellation on the first read so the download loop observes it.
    private sealed class CancellingStream(byte[] data, CancellationTokenSource cts) : MemoryStream(data)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return base.ReadAsync(buffer, cancellationToken);
        }
    }
}
