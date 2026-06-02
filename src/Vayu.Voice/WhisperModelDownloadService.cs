namespace Vayu.Voice;

/// <summary>Progress for an in-flight model download.</summary>
/// <param name="BytesReceived">Bytes written so far.</param>
/// <param name="TotalBytes">Total expected bytes, or null when the server didn't report a length.</param>
public readonly record struct ModelDownloadProgress(long BytesReceived, long? TotalBytes)
{
    /// <summary>0..1 fraction when the total is known; null otherwise.</summary>
    public double? Fraction => TotalBytes is > 0 ? Math.Clamp((double)BytesReceived / TotalBytes.Value, 0, 1) : null;
}

/// <summary>How a model download ended.</summary>
public enum ModelDownloadStatus
{
    /// <summary>Downloaded and saved to disk.</summary>
    Completed = 0,
    /// <summary>The user cancelled; any partial file was deleted.</summary>
    Cancelled = 1,
    /// <summary>Failed (network, disk, blocked URL); any partial file was deleted.</summary>
    Failed = 2,
}

/// <summary>Outcome of a model download. Carries the saved path on success.</summary>
/// <param name="Status">How the download ended.</param>
/// <param name="FilePath">The saved model path on success; null otherwise.</param>
/// <param name="Message">Redaction-safe explanation for the UI.</param>
public sealed record ModelDownloadResult(ModelDownloadStatus Status, string? FilePath, string Message);

/// <summary>
/// Downloads a catalog Whisper model to a local directory with explicit user
/// consent, streaming progress and cleaning up partial files on cancel/failure.
/// The library takes an injected <see cref="HttpClient"/> so it is fully
/// testable with a fake handler — no real network in CI.
/// </summary>
/// <remarks>
/// Safety: only a <see cref="WhisperModelCatalog.IsAllowedDownloadUrl"/> source
/// is fetched; the model is large binary data (never a secret); a partial file
/// is always deleted if the download does not complete; nothing is logged about
/// the file contents.
/// </remarks>
public sealed class WhisperModelDownloadService
{
    private readonly HttpClient _http;

    public WhisperModelDownloadService(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    /// <summary>
    /// Downloads <paramref name="model"/> into <paramref name="destinationDirectory"/>
    /// (created if needed), reporting progress. Returns the saved path on success.
    /// </summary>
    public async Task<ModelDownloadResult> DownloadAsync(
        WhisperModelInfo model,
        string destinationDirectory,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        if (!WhisperModelCatalog.IsAllowedDownloadUrl(model.DownloadUrl))
        {
            return new ModelDownloadResult(ModelDownloadStatus.Failed, null,
                "Download source is not on Vayu's allowlist.");
        }

        string finalPath;
        string tempPath;
        try
        {
            Directory.CreateDirectory(destinationDirectory);
            finalPath = Path.Combine(destinationDirectory, model.FileName);
            tempPath = finalPath + ".part";
        }
#pragma warning disable CA1031 // Setup failure → safe message.
        catch (Exception)
        {
            return new ModelDownloadResult(ModelDownloadStatus.Failed, null,
                "Could not prepare the download folder.");
        }
#pragma warning restore CA1031

        try
        {
            using var response = await _http
                .GetAsync(model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                DeleteQuietly(tempPath);
                return new ModelDownloadResult(ModelDownloadStatus.Failed, null,
                    $"Download failed ({(int)response.StatusCode}).");
            }

            var total = response.Content.Headers.ContentLength;
            await using (var src = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var dst = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                long received = 0;
                int read;
                while ((read = await src.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;
                    progress?.Report(new ModelDownloadProgress(received, total));
                }
            }

            // Atomically move the completed temp file into place.
            if (File.Exists(finalPath))
            {
                File.Delete(finalPath);
            }
            File.Move(tempPath, finalPath);

            return new ModelDownloadResult(ModelDownloadStatus.Completed, finalPath,
                $"Downloaded {model.DisplayName}.");
        }
        catch (OperationCanceledException)
        {
            DeleteQuietly(tempPath);
            return new ModelDownloadResult(ModelDownloadStatus.Cancelled, null,
                "Download cancelled.");
        }
#pragma warning disable CA1031 // Network/disk boundary: any failure → safe message + cleanup.
        catch (Exception)
        {
            DeleteQuietly(tempPath);
            return new ModelDownloadResult(ModelDownloadStatus.Failed, null,
                "Download failed. Check your connection and try again.");
        }
#pragma warning restore CA1031
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup of the partial file.
        }
    }
}
