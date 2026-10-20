using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Transiever.Common.Accounts;

internal sealed class WindowsCredentialApi : INativeCredentialApi
{
    internal const int NotFound = 1168;
    private const uint GenericCredential = 1;
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    public NativeCredentialRead Read(string key)
    {
        if (!CredRead(key, GenericCredential, 0, out var pointer)) return new(Marshal.GetLastPInvokeError());
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            var bytes = new byte[credential.BlobSize];
            try
            {
                Marshal.Copy(credential.Blob, bytes, 0, bytes.Length);
                return new(0, Utf8.GetString(bytes));
            }
            catch (DecoderFallbackException) { return new(13); }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
                Marshal.Copy(bytes, 0, credential.Blob, bytes.Length);
            }
        }
        finally { CredFree(pointer); }
    }

    public int Write(string key, string value)
    {
        var bytes = Utf8.GetBytes(value);
        var target = Marshal.StringToCoTaskMemUni(key);
        var username = Marshal.StringToCoTaskMemUni("Transiever");
        var blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new Credential
            {
                Type = GenericCredential,
                TargetName = target,
                Blob = blob,
                BlobSize = checked((uint)bytes.Length),
                Persist = 2,
                UserName = username
            };
            return CredWrite(ref credential, 0) ? 0 : Marshal.GetLastPInvokeError();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            Marshal.FreeHGlobal(blob);
            Marshal.FreeCoTaskMem(username);
            Marshal.FreeCoTaskMem(target);
        }
    }

    public int Delete(string key) => CredDelete(key, GenericCredential, 0) ? 0 : Marshal.GetLastPInvokeError();

    [StructLayout(LayoutKind.Sequential)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public nint TargetName;
        public nint Comment;
        public long LastWritten;
        public uint BlobSize;
        public nint Blob;
        public uint Persist;
        public uint AttributeCount;
        public nint Attributes;
        public nint TargetAlias;
        public nint UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out nint credential);
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(nint credential);
}
