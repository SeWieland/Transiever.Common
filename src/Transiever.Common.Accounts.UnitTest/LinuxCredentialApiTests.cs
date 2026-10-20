namespace Transiever.Common.Accounts.UnitTest;

public sealed class LinuxCredentialApiTests
{
    [Fact]
    public void Replacement_and_outcome_updates_follow_existing_entry_after_default_collection_changes()
    {
        var session = new FakeSession();
        var api = new LinuxCredentialApi(() => session);
        var store = new PlatformCredentialStore(new NativeSecretStore(api, LinuxCredentialApi.NotFound));
        var reference = new AccountReference(Guid.NewGuid());
        Assert.Equal(CredentialStatus.Available, store.Set(reference, CredentialKind.Password, "old-private-password"));
        session.DefaultCollection = "B";

        Assert.Equal(CredentialStatus.Available, store.Set(reference, CredentialKind.Password, "replacement-private-password"));
        Assert.Equal("replacement-private-password", store.Get(reference, CredentialKind.Password).Secret);
        Assert.Equal(CredentialStatus.Rejected, store.MarkOutcome(reference, CredentialKind.Password, CredentialStatus.Rejected));
        Assert.Equal(CredentialStatus.Rejected, store.GetStatus(reference, CredentialKind.Password));
        Assert.Equal("A", Assert.Single(session.Entries.Values).Collection);
        Assert.Equal(CredentialStatus.Missing, store.Remove(reference, CredentialKind.Password));
        Assert.Empty(session.Entries);
        Assert.Equal(CredentialStatus.Missing, store.GetStatus(reference, CredentialKind.Password));
    }

    [Fact]
    public void Duplicate_matches_fail_closed_without_reading_replacing_or_deleting_either_entry()
    {
        var session = new FakeSession();
        session.Create("scoped-key", "first-private-payload");
        session.DefaultCollection = "B";
        session.Create("scoped-key", "second-private-payload");
        var api = new LinuxCredentialApi(() => session);

        Assert.Equal(2, api.Read("scoped-key").Error);
        Assert.Equal(2, api.Write("scoped-key", "replacement-private-payload"));
        Assert.Equal(2, api.Delete("scoped-key"));
        Assert.Equal(["first-private-payload", "second-private-payload"], session.Entries.Values.Select(entry => entry.Value));
    }

    [Fact]
    public void Locked_match_is_unavailable_and_does_not_create_a_second_entry_in_the_new_default_collection()
    {
        var session = new FakeSession();
        session.Create("scoped-key", "original-private-payload");
        session.Locked = true;
        session.DefaultCollection = "B";
        var api = new LinuxCredentialApi(() => session);

        Assert.Equal(2, api.Read("scoped-key").Error);
        Assert.Equal(2, api.Write("scoped-key", "replacement-private-payload"));
        Assert.Equal(2, api.Delete("scoped-key"));
        Assert.Equal("original-private-payload", Assert.Single(session.Entries.Values).Value);
    }

    private sealed class FakeSession : ILinuxCredentialSession
    {
        public string DefaultCollection { get; set; } = "A";
        public bool Locked { get; set; }
        public Dictionary<nint, Entry> Entries { get; } = [];

        public LinuxCredentialMatches Search(string key, bool loadSecrets)
        {
            var matches = Entries.Where(pair => pair.Value.Key == key).Select(pair => pair.Key).ToArray();
            return new LinuxCredentialMatches(Locked && matches.Length > 0 ? 2 : matches.Length == 0 ? 1 : 0, matches);
        }

        public NativeCredentialRead Read(nint item) => new(0, Entries[item].Value);

        public int Create(string key, string value)
        {
            var existing = Entries.FirstOrDefault(pair => pair.Value.Collection == DefaultCollection && pair.Value.Key == key);
            if (existing.Value is not null) Entries[existing.Key] = existing.Value with { Value = value };
            else Entries[Entries.Count + 1] = new(DefaultCollection, key, value);
            return 0;
        }

        public int Delete(nint item)
        {
            Entries.Remove(item);
            return 0;
        }

        public int Update(nint item, string value)
        {
            Entries[item] = Entries[item] with { Value = value };
            return 0;
        }

        public void Dispose() { }
        internal sealed record Entry(string Collection, string Key, string Value);
    }
}
