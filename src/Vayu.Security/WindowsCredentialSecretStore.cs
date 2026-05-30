using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Vayu.Security;

/// <summary>
/// Stores secrets in the Windows Credential Manager via <c>advapi32.dll</c>
/// (DPAPI-protected, current-user scope). Vayu's preferred location for a
/// Gemini API key.
/// </summary>
/// <remarks>
/// Each secret is stored as a Generic credential under the exact
/// <c>name</c> you pass — <c>SecureConfigService</c> chooses the
/// <c>Vayu:GeminiApiKey</c> convention. Never logs the credential blob.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialSecretStore : ISecretStore
{
    /// <inheritdoc />
    public string SourceName => "WindowsCredentialManager";

    /// <inheritdoc />
    public Task<string?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!CredRead(name, CredentialType.Generic, 0, out var ptr))
        {
            var err = Marshal.GetLastWin32Error();
            return err == ErrorNotFound
                ? Task.FromResult<string?>(null)
                : Task.FromException<string?>(new Win32Exception(err));
        }

        try
        {
            var cred = Marshal.PtrToStructure<NativeCredential>(ptr);
            if (cred.CredentialBlobSize == 0 || cred.CredentialBlob == IntPtr.Zero)
            {
                return Task.FromResult<string?>(string.Empty);
            }

            var charCount = (int)(cred.CredentialBlobSize / sizeof(char));
            return Task.FromResult<string?>(Marshal.PtrToStringUni(cred.CredentialBlob, charCount));
        }
        finally
        {
            CredFree(ptr);
        }
    }

    /// <inheritdoc />
    public Task<SecretStoreResult> SetAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        var blob = Encoding.Unicode.GetBytes(value);
        var blobPtr = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobPtr, blob.Length);
            var cred = new NativeCredential
            {
                Type = (uint)CredentialType.Generic,
                TargetName = name,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobPtr,
                Persist = (uint)CredentialPersistence.LocalMachine,
                UserName = Environment.UserName,
            };

            if (!CredWrite(ref cred, 0))
            {
                var err = Marshal.GetLastWin32Error();
                return Task.FromResult(SecretStoreResult.Failure($"CredWrite failed (Win32 {err})."));
            }

            return Task.FromResult(SecretStoreResult.Ok);
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    /// <inheritdoc />
    public Task<SecretStoreResult> DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!CredDelete(name, CredentialType.Generic, 0))
        {
            var err = Marshal.GetLastWin32Error();
            return Task.FromResult(err == ErrorNotFound
                ? SecretStoreResult.Ok
                : SecretStoreResult.Failure($"CredDelete failed (Win32 {err})."));
        }

        return Task.FromResult(SecretStoreResult.Ok);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!CredRead(name, CredentialType.Generic, 0, out var ptr))
        {
            return Task.FromResult(false);
        }

        CredFree(ptr);
        return Task.FromResult(true);
    }

    private enum CredentialType : uint
    {
        Generic = 1,
    }

    private enum CredentialPersistence : uint
    {
        LocalMachine = 2,
    }

    private const int ErrorNotFound = 1168;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
    }

#pragma warning disable SYSLIB1054 // Source-gen LibraryImport does not yet support the marshalled string fields on NativeCredential.
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, CredentialType type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, CredentialType type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr cred);
#pragma warning restore SYSLIB1054
}
