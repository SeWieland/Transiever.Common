namespace Transiever.Common.Accounts.UnitTest;

public sealed class AccountManagerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "transiever-manager-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Account_deletion_preserves_profile_until_all_credential_kinds_are_removed()
    {
        var profiles = new JsonAccountProfileStore(_directory);
        var profile = profiles.Create(JsonAccountProfileStoreTests.Profile());
        var platform = new CredentialLifecycleTests.MemoryPlatform();
        var credentials = new PlatformCredentialStore(platform);
        credentials.Set(profile.Reference, CredentialKind.Password, "private-password");
        credentials.Set(profile.Reference, CredentialKind.OAuthRefreshToken, "private-token");
        var manager = new AccountManager(profiles, credentials);
        platform.Error = true;
        Assert.Equal(CredentialStatus.Unavailable, manager.Delete(profile.Reference));
        Assert.Equal(profile, profiles.Get(profile.Reference));
        platform.Error = false;
        Assert.Equal(CredentialStatus.Missing, manager.Delete(profile.Reference));
        Assert.Null(profiles.Get(profile.Reference));
        Assert.Equal(CredentialStatus.Missing, credentials.GetStatus(profile.Reference, CredentialKind.Password));
        Assert.Equal(CredentialStatus.Missing, credentials.GetStatus(profile.Reference, CredentialKind.OAuthRefreshToken));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
