using System.Reflection;

namespace Transiever.Common.Accounts.UnitTest;

public sealed class AccountReferenceTests
{
    [Fact]
    public void Parses_and_formats_a_stable_account_reference()
    {
        var assembly = Assembly.Load("Transiever.Common.Accounts");
        var type = assembly.GetType("Transiever.Common.Accounts.AccountReference");
        Assert.NotNull(type);
        var parse = type!.GetMethod("Parse", [typeof(string)]);
        Assert.NotNull(parse);
        var value = parse!.Invoke(null, ["account:6d53f44e-6c16-429c-a1e8-d8ef332f2232"]);

        Assert.Equal("account:6d53f44e-6c16-429c-a1e8-d8ef332f2232", value?.ToString());
    }
}
