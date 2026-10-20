namespace Transiever.Common.Accounts.UnitTest;

public sealed class NativeSecretStoreTests
{
    [Theory]
    [InlineData(1168)]
    [InlineData(-25300)]
    [InlineData(1)]
    public void Platform_error_mapping_preserves_missing_available_and_inaccessible_states(int missingCode)
    {
        var native = new FakeNative { Code = missingCode };
        var store = new NativeSecretStore(native, missingCode);
        Assert.Equal(CredentialStatus.Missing, store.Read("scoped-key").Status);
        Assert.Equal(CredentialStatus.Missing, store.Delete("scoped-key"));
        native.Code = 0;
        Assert.Equal("private-native-secret", store.Read("scoped-key").Secret);
        Assert.Equal(CredentialStatus.Available, store.Write("scoped-key", "new-private-secret"));
        Assert.Equal(CredentialStatus.Missing, store.Delete("scoped-key"));
        native.Code = 77;
        Assert.Equal(CredentialStatus.Unavailable, store.Read("scoped-key").Status);
        Assert.Null(store.Read("scoped-key").Secret);
        Assert.Equal(CredentialStatus.Unavailable, store.Write("scoped-key", "private-secret"));
        Assert.Equal(CredentialStatus.Unavailable, store.Delete("scoped-key"));
    }

    [Fact]
    public void Missing_native_libraries_fail_closed_without_exposing_exception_messages()
    {
        var store = new NativeSecretStore(new FakeNative { Throw = true }, 1);
        Assert.Equal(CredentialStatus.Unavailable, store.Read("scoped-key").Status);
        Assert.Equal(CredentialStatus.Unavailable, store.Write("scoped-key", "private-secret"));
        Assert.Equal(CredentialStatus.Unavailable, store.Delete("scoped-key"));
    }

    [Fact]
    public void Secret_service_prompt_requests_are_not_treated_as_completed_operations()
    {
        Assert.True(NativeSecretStore.CompletedWithoutPrompt("/"));
        Assert.False(NativeSecretStore.CompletedWithoutPrompt("/org/freedesktop/secrets/prompt/1"));
        Assert.False(NativeSecretStore.CompletedWithoutPrompt(null));
    }

    private sealed class FakeNative : INativeCredentialApi
    {
        public int Code { get; set; }
        public bool Throw { get; set; }
        public NativeCredentialRead Read(string key) => Throw ? throw new DllNotFoundException("private-secret") : new(Code, "private-native-secret");
        public int Write(string key, string value) => Throw ? throw new DllNotFoundException(value) : Code;
        public int Delete(string key) => Throw ? throw new DllNotFoundException("private-secret") : Code;
    }
}
