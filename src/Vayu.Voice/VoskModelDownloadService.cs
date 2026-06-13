using System.IO.Compression;

namespace Vayu.Voice;

/// <summary>
/// Downloads a catalog Vosk model archive with explicit user consent, then
/// extracts it into the local wake/STT model directory. A Vosk model is a
/// directory of files shipped as a ZIP, so this both downloads (streaming
/// progress) and unpacks — flattening the archive's single top-level folder so
/// the model files sit directly under the destination directory that
/// <c>VoskWakeWordEngine</c> loads.
/// </summary>
/// <remarks>
/// Safety: only a <see cref="VoskModelCatalog.IsAllowedDownloadUrl"/> source is
/// fetched; extraction is guarded against path traversal (zip-slip); the model
/// is large binary data (never a secret); partial archives and staging folders
/// are always cleaned up on cancel/failure; nothing is logged about the file
/// contents. The injected <see cref="HttpClient"/> makes it fully testable with
/// a fake handler — no real network in CI.
/// </remarks>
public sealed class VoskModelDownloadService
{
    private readonly HttpClient _http;

    public VoskModelDownloadService(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    /// <summary>
    /// Downloads <paramref name="model"/> and extracts it into
    /// <paramref name="destinationModelDirectory"/> (replacing any existing
    /// contents). On success the result's path is the model directory ready for
    /// the wake-word engine to load.
    /// </summary>
    public async Task<ModelDownloadResult> DownloadAsync(
        VoskModelInfo model,
        string destinationModelDirectory,
        IProgress<ModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationModelDirectory);

        if (!VoskModelCatalog.IsAllowedDownloadUrl(model.DownloadUrl))
        {
            return new ModelDownloadResult(ModelDownloadStatus.Failed, null,
                "Download source is not on Vayu's allowlist.");
        }

        var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destinationModelDirectory));
        var archivePath = destination + ".zip.part";
        var stagingDir = destination + ".extract";

        try
        {
            var parent = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }
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
            // 1. Stream the archive to a temp .part file with progress.
            using (var response = await _http
                .GetAsync(model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    CleanupQuietly(archivePath, stagingDir);
                    return new ModelDownloadResult(ModelDownloadStatus.Failed, null,
                        $"Download failed ({(int)response.StatusCode}).");
                }

                var total = response.Content.Headers.ContentLength;
                await using var src = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var dst = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None);
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

            cancellationToken.ThrowIfCancellationRequested();

            // 2. Extract to a clean staging directory (zip-slip guarded).
            if (Directory.Exists(stagingDir))
            {
                Directory.Delete(stagingDir, recursive: true);
            }
            Directory.CreateDirectory(stagingDir);
            ExtractZipSafely(archivePath, stagingDir);

            // 3. Locate the actual model root inside the archive and flatten it.
            var modelRoot = ResolveModelRoot(stagingDir);
            if (modelRoot is null)
            {
                CleanupQuietly(archivePath, stagingDir);
                return new ModelDownloadResult(ModelDownloadStatus.Failed, null,
                    "The downloaded archive did not contain a recognisable Vosk model.");
            }

            // 4. Move the model into place, replacing any previous model.
            if (Directory.Exists(destination))
            {
                Directory.Delete(destination, recursive: true);
            }
            Directory.Move(modelRoot, destination);

            CleanupQuietly(archivePath, stagingDir);
            return new ModelDownloadResult(ModelDownloadStatus.Completed, destination,
                $"Installed {model.DisplayName}.");
        }
        catch (OperationCanceledException)
        {
            CleanupQuietly(archivePath, stagingDir);
            return new ModelDownloadResult(ModelDownloadStatus.Cancelled, null,
                "Download cancelled.");
        }
#pragma warning disable CA1031 // Network/disk/extract boundary: any failure → safe message + cleanup.
        catch (Exception)
        {
            CleanupQuietly(archivePath, stagingDir);
            return new ModelDownloadResult(ModelDownloadStatus.Failed, null,
                "Download or extraction failed. Check your connection and try again.");
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Extracts <paramref name="archivePath"/> into <paramref name="targetDir"/>,
    /// rejecting any entry whose resolved path would escape the target directory
    /// (zip-slip protection).
    /// </summary>
    private static void ExtractZipSafely(string archivePath, string targetDir)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDir))
            + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            var destPath = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
            if (!destPath.StartsWith(root, StringComparison.Ordinal))
            {
                throw new IOException("Archive entry escapes the target directory.");
            }

            // Directory entries have an empty name; just ensure the folder exists.
            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destPath);
                continue;
            }

            var entryDir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(entryDir))
            {
                Directory.CreateDirectory(entryDir);
            }
            entry.ExtractToFile(destPath, overwrite: true);
        }
    }

    /// <summary>
    /// Finds the directory that actually holds the Vosk model files. The archive
    /// usually wraps everything in a single top-level folder; a Vosk model
    /// directory is identified by its <c>conf</c> subfolder.
    /// </summary>
    private static string? ResolveModelRoot(string stagingDir)
    {
        if (IsVoskModelDir(stagingDir))
        {
            return stagingDir;
        }
        foreach (var dir in Directory.EnumerateDirectories(stagingDir))
        {
            if (IsVoskModelDir(dir))
            {
                return dir;
            }
        }
        // Fall back to a single wrapping folder even if the marker differs.
        var subdirs = Directory.GetDirectories(stagingDir);
        return subdirs.Length == 1 ? subdirs[0] : null;
    }

    private static bool IsVoskModelDir(string dir)
        => Directory.Exists(Path.Combine(dir, "conf"));

    private static void CleanupQuietly(string archivePath, string stagingDir)
    {
        try
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
        }
        catch
        {
            // Best-effort cleanup of the partial archive.
        }
        try
        {
            if (Directory.Exists(stagingDir))
            {
                Directory.Delete(stagingDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup of the staging folder.
        }
    }
}
