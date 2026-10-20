namespace Transiever.Common.Accounts;

/// <summary>Persists only non-secret account settings.</summary>
public interface IAccountProfileStore
{
    /// <summary>Creates a profile with a new stable reference and store-assigned timestamps.</summary>
    AccountProfile Create(AccountProfile profile);
    /// <summary>Gets a profile or null when absent.</summary>
    AccountProfile? Get(AccountReference reference);
    /// <summary>Lists saved profiles.</summary>
    IReadOnlyList<AccountProfile> List();
    /// <summary>Updates settings while preserving the reference and creation time.</summary>
    AccountProfile Update(AccountProfile profile);
    /// <summary>Deletes a profile. Use <see cref="AccountManager.Delete"/> to remove credentials first.</summary>
    void Delete(AccountReference reference);
}
