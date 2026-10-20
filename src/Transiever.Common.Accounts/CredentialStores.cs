namespace Transiever.Common.Accounts;

/// <summary>Creates the operating-system credential store or explicit no-store mode.</summary>
public static class CredentialStores
{
    /// <summary>Creates the current platform's store; inaccessible services fail closed as Unavailable.</summary>
    public static ICredentialStore CreateDefault()
    {
        IPlatformSecretStore platform = OperatingSystem.IsWindows() ? new NativeSecretStore(new WindowsCredentialApi(), WindowsCredentialApi.NotFound)
            : OperatingSystem.IsMacOS() ? new NativeSecretStore(new MacOsCredentialApi(), MacOsCredentialApi.NotFound)
            : OperatingSystem.IsLinux() ? new NativeSecretStore(new LinuxCredentialApi(), LinuxCredentialApi.NotFound)
            : new UnavailableSecretStore();
        return new PlatformCredentialStore(platform);
    }

    /// <summary>Creates no-store mode without a plaintext fallback.</summary>
    public static ICredentialStore CreateUnavailable() => new PlatformCredentialStore(new UnavailableSecretStore());
}

internal sealed class UnavailableSecretStore : IPlatformSecretStore
{
    public CredentialResult Read(string key) => new(CredentialStatus.Unavailable);
    public CredentialStatus Write(string key, string value) => CredentialStatus.Unavailable;
    public CredentialStatus Delete(string key) => CredentialStatus.Unavailable;
}
