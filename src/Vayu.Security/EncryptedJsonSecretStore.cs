using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;

namespace Vayu.Security;

/// <summary>
/// DPAPI-encrypted local fallback store. Used only when neither the
/// Windows Credential Manager nor an environment variable carries the
/// secret. File lives at <c>%LOCALAPPDATA%\Vayu\secrets.dat</c> by default,
/// encrypted under <see cref="DataProtectionScope.CurrentUser"/>.
/// </summary>
/// <remarks>
/// The file is never plaintext on disk. The unencrypted shape is a flat
/// JSON object of <c>{ name: value }</c>; everything written goes through
/// DPAPI <see cref="ProtectedData"/> first.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class EncryptedJsonSecretStore : ISecretStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <inheritdoc />
    public string SourceName => "EncryptedJson";

    /// <summary>Default location: <c>%LOCALAPPDATA%\Vayu\secrets.dat</c>.</summary>
    public EncryptedJsonSecretStore()
        : this(DefaultPath())
    {
    }

    /// <summary>Custom location, primarily for tests.</summary>
    public EncryptedJsonSecretStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    private static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Vayu",
        "secrets.dat");

    /// <inheritdoc />
    public async Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return data.TryGetValue(name, out var v) ? v : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<SecretStoreResult> SetAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
            data[name] = value;
            await SaveAsync(data, cancellationToken).ConfigureAwait(false);
            return SecretStoreResult.Ok;
        }
        catch (CryptographicException ex)
        {
            return SecretStoreResult.Failure($"DPAPI write failed: {ex.GetType().Name}.");
        }
        catch (IOException ex)
        {
            return SecretStoreResult.Failure($"I/O failed: {ex.GetType().Name}.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<SecretStoreResult> DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (data.Remove(name))
            {
                await SaveAsync(data, cancellationToken).ConfigureAwait(false);
            }
            return SecretStoreResult.Ok;
        }
        catch (CryptographicException ex)
        {
            return SecretStoreResult.Failure($"DPAPI write failed: {ex.GetType().Name}.");
        }
        catch (IOException ex)
        {
            return SecretStoreResult.Failure($"I/O failed: {ex.GetType().Name}.");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
            return data.ContainsKey(name);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, string>();
        }

        var encrypted = await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false);
        if (encrypted.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        var plaintext = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
        var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(plaintext);
        return dict ?? new Dictionary<string, string>();
    }

    private async Task SaveAsync(Dictionary<string, string> data, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var plaintext = JsonSerializer.SerializeToUtf8Bytes(data);
        var encrypted = ProtectedData.Protect(plaintext, optionalEntropy: null, DataProtectionScope.CurrentUser);
        await File.WriteAllBytesAsync(_path, encrypted, cancellationToken).ConfigureAwait(false);
    }
}
