using System.IO.Compression;
using System.Net;

using Vayu.Voice;

namespace Vayu.Voice.Tests;

public class VoskModelDownloadServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _destination;

    public VoskModelDownloadServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"vayu-vosk-{Guid.NewGuid():N}");
        _destination = Path.Combine(_tempDir, "vosk-wake");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    private static VoskModelInfo Model => VoskModelCatalog.Recommended;

    [Fact]
    public async Task Download_Success_ExtractsAndFlattensModel()
    {
        var zip = BuildZip(new()
        {
            ["vosk-model-small-en-us-0.15/conf/model.conf"] = "conf"u8.ToArray(),
            ["vosk-model-small-en-us-0.15/am/final.mdl"] = "model"u8.ToArray(),
        });
        var service = new VoskModelDownloadService(new HttpClient(new StubHandler(HttpStatusCode.OK, zip)));

        var result = await service.DownloadAsync(Model, _destination);

        Assert.Equal(ModelDownloadStatus.Completed, result.Status);
        Assert.Equal(_destination, result.FilePath);
        // The wrapping folder is flattened: model files sit directly under the destination.
        Assert.True(File.Exists(Path.Combine(_destination, "conf", "model.conf")));
        Assert.True(File.Exists(Path.Combine(_destination, "am", "final.mdl")));
        // No staging or partial artifacts are left behind.
        Assert.False(File.Exists(_destination + ".zip.part"));
        Assert.False(Directory.Exists(_destination + ".extract"));
    }

    [Fact]
    public async Task Download_ReplacesExistingModel()
    {
        Directory.CreateDirectory(_destination);
        await File.WriteAllTextAsync(Path.Combine(_destination, "stale.txt"), "old");

        var zip = BuildZip(new()
        {
            ["vosk-model-small-en-us-0.15/conf/model.conf"] = "conf"u8.ToArray(),
        });
        var service = new VoskModelDownloadService(new HttpClient(new StubHandler(HttpStatusCode.OK, zip)));

        var result = await service.DownloadAsync(Model, _destination);

        Assert.Equal(ModelDownloadStatus.Completed, result.Status);
        Assert.False(File.Exists(Path.Combine(_destination, "stale.txt"))); // previous model removed
        Assert.True(File.Exists(Path.Combine(_destination, "conf", "model.conf")));
    }

    [Fact]
    public async Task Download_HttpError_Fails_AndCleansUp()
    {
        var service = new VoskModelDownloadService(
            new HttpClient(new StubHandler(HttpStatusCode.NotFound, Array.Empty<byte>())));

        var result = await service.DownloadAsync(Model, _destination);

        Assert.Equal(ModelDownloadStatus.Failed, result.Status);
        Assert.Null(result.FilePath);
        Assert.False(File.Exists(_destination + ".zip.part"));
        Assert.False(Directory.Exists(_destination));
    }

    [Fact]
    public async Task Download_Cancelled_Cleanup()
    {
        using var cts = new CancellationTokenSource();
        var service = new VoskModelDownloadService(
            new HttpClient(new StubHandler(HttpStatusCode.OK, new byte[4096], cts)));

        var result = await service.DownloadAsync(Model, _destination, progress: null, cts.Token);

        Assert.Equal(ModelDownloadStatus.Cancelled, result.Status);
        Assert.False(File.Exists(_destination + ".zip.part"));
        Assert.False(Directory.Exists(_destination));
    }

    [Fact]
    public async Task Download_BlockedUrl_FailsWithoutNetwork()
    {
        var blocked = Model with { DownloadUrl = "https://evil.example.com/model.zip" };
        var handler = new StubHandler(HttpStatusCode.OK, new byte[] { 9 });
        var service = new VoskModelDownloadService(new HttpClient(handler));

        var result = await service.DownloadAsync(blocked, _destination);

        Assert.Equal(ModelDownloadStatus.Failed, result.Status);
        Assert.Equal(0, handler.Calls); // never hit the network
    }

    [Fact]
    public async Task Download_ZipSlipEntry_Rejected()
    {
        // An archive that tries to escape the target directory must fail and
        // must not write outside the destination.
        var zip = BuildZip(new()
        {
            ["../escaped.txt"] = "pwned"u8.ToArray(),
        });
        var service = new VoskModelDownloadService(new HttpClient(new StubHandler(HttpStatusCode.OK, zip)));

        var result = await service.DownloadAsync(Model, _destination);

        Assert.Equal(ModelDownloadStatus.Failed, result.Status);
        Assert.False(File.Exists(Path.Combine(_tempDir, "escaped.txt")));
        Assert.False(Directory.Exists(_destination + ".extract"));
    }

    [Fact]
    public async Task Download_NoVoskModelInside_Fails()
    {
        // Two unrelated top-level folders, neither a Vosk model — cannot resolve.
        var zip = BuildZip(new()
        {
            ["readme/notes.txt"] = "hi"u8.ToArray(),
            ["other/data.bin"] = "x"u8.ToArray(),
        });
        var service = new VoskModelDownloadService(new HttpClient(new StubHandler(HttpStatusCode.OK, zip)));

        var result = await service.DownloadAsync(Model, _destination);

        Assert.Equal(ModelDownloadStatus.Failed, result.Status);
        Assert.False(Directory.Exists(_destination));
    }

    private static byte[] BuildZip(Dictionary<string, byte[]> entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var stream = entry.Open();
                stream.Write(data, 0, data.Length);
            }
        }
        return ms.ToArray();
    }

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
