namespace Transiever.Common.Accounts;

/// <summary>Coordinates local profile and remembered-credential removal.</summary>
public sealed class AccountManager(IAccountProfileStore profiles, ICredentialStore credentials)
{
    /// <summary>Deletes known credentials before the profile, preserving it if any store operation fails.</summary>
    /// <returns>Missing on complete deletion, or Unavailable while the profile remains.</returns>
    public CredentialStatus Delete(AccountReference reference)
    {
        foreach (var kind in Enum.GetValues<CredentialKind>())
            if (credentials.Remove(reference, kind) != CredentialStatus.Missing) return CredentialStatus.Unavailable;
        profiles.Delete(reference);
        return CredentialStatus.Missing;
    }
}
