namespace Transiever.Common.Cli.UnitTest;

public sealed class SecretOptionsTests
{
    [Theory]
    [InlineData("--password=synthetic-sentinel", true)]
    [InlineData("--PASSWORD=synthetic-sentinel", true)]
    [InlineData("--password=", true)]
    [InlineData("--password-stdin=synthetic-sentinel", true)]
    [InlineData("--password", false)]
    [InlineData("--password-stdin", false)]
    [InlineData("--password-extra=synthetic-sentinel", false)]
    [InlineData("prefix--password=synthetic-sentinel", false)]
    [InlineData("--token=synthetic-sentinel", false)]
    [InlineData("", false)]
    public void Assignment_matching_is_exact_case_insensitive_and_classification_only(string argument, bool expected)
    {
        Assert.Equal(expected, SecretOptions.IsAssignment(argument, "--password", "--password-stdin"));
    }

    [Fact]
    public void No_forbidden_options_means_no_match()
    {
        Assert.False(SecretOptions.IsAssignment("--password=synthetic-sentinel"));
    }
}
