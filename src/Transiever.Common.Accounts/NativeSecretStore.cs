using System.Runtime.InteropServices;

namespace Transiever.Common.Accounts;

internal interface INativeCredentialApi
{
    NativeCredentialRead Read(string key);
    int Write(string key, string value);
    int Delete(string key);
}

internal readonly struct NativeCredentialRead(int error, string? value = null)
{
    public int Error { get; } = error;
    public string? Value { get; } = value;
}

internal sealed class NativeSecretStore(INativeCredentialApi api, int missingError) : IPlatformSecretStore
{
    public CredentialResult Read(string key)
    {
        try
        {
            var result = api.Read(key);
            return result.Error == 0 && result.Value is not null ? new(CredentialStatus.Available, result.Value)
                : new(result.Error == missingError ? CredentialStatus.Missing : CredentialStatus.Unavailable);
        }
        catch (Exception error) when (IsUnavailable(error)) { return new(CredentialStatus.Unavailable); }
    }

    public CredentialStatus Write(string key, string value)
    {
        try { return api.Write(key, value) == 0 ? CredentialStatus.Available : CredentialStatus.Unavailable; }
        catch (Exception error) when (IsUnavailable(error)) { return CredentialStatus.Unavailable; }
    }

    public CredentialStatus Delete(string key)
    {
        try
        {
            var error = api.Delete(key);
            return error == 0 || error == missingError ? CredentialStatus.Missing : CredentialStatus.Unavailable;
        }
        catch (Exception error) when (IsUnavailable(error)) { return CredentialStatus.Unavailable; }
    }

    internal static bool CompletedWithoutPrompt(string? path) => path == "/";

    private static bool IsUnavailable(Exception error) => error is DllNotFoundException or EntryPointNotFoundException
        or BadImageFormatException or ExternalException or UnauthorizedAccessException;
}
