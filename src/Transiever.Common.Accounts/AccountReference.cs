namespace Transiever.Common.Accounts;

/// <summary>Identifies one account profile without containing credential material.</summary>
public readonly record struct AccountReference(Guid Value)
{
    /// <summary>Parses the canonical <c>account:&lt;guid&gt;</c> representation.</summary>
    public static AccountReference Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        const string prefix = "account:";
        if (!value.StartsWith(prefix, StringComparison.Ordinal)
            || !Guid.TryParseExact(value[prefix.Length..], "D", out var identifier))
        {
            throw new ArgumentException("An account reference must use the account:<guid> format.", nameof(value));
        }

        return new AccountReference(identifier);
    }

    /// <inheritdoc />
    public override string ToString() => $"account:{Value:D}";
}
