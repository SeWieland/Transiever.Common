namespace Transiever.Common.Cli.UnitTest;

public sealed class SecretInputTests
{
    [Fact]
    public void Hidden_input_preserves_characters_edits_and_stops_at_enter()
    {
        Queue<ConsoleKeyInfo> keys = new([
            Key('\b', ConsoleKey.Backspace), Key('a'), Key('b'), Key('\b', ConsoleKey.Backspace),
            Key('\t', ConsoleKey.Tab), Key('\0', ConsoleKey.LeftArrow), Key(' '), Key('c'),
            Key('\r', ConsoleKey.Enter), Key('z')]);

        Assert.Equal("a c", SecretInput.ReadHidden(keys.Dequeue));
        Assert.Single(keys);
    }

    [Fact]
    public void Hidden_input_returns_empty_without_a_password_policy()
    {
        Assert.Equal("", SecretInput.ReadHidden(() => Key('\r', ConsoleKey.Enter)));
    }

    [Fact]
    public void Hidden_backspace_removes_one_utf16_code_unit()
    {
        Queue<ConsoleKeyInfo> keys = new([
            Key('\ud83d'), Key('\ude00'), Key('\b', ConsoleKey.Backspace), Key('\r', ConsoleKey.Enter)]);
        Assert.Equal("\ud83d", SecretInput.ReadHidden(keys.Dequeue));
    }

    [Fact]
    public void Hidden_input_propagates_read_failure()
    {
        var failure = new IOException("synthetic read failure");
        Assert.Same(failure, Assert.Throws<IOException>(() => SecretInput.ReadHidden(() => throw failure)));
    }

    [Theory]
    [InlineData("terminated")]
    [InlineData("unterminated")]
    [InlineData("spaces")]
    public void Line_input_preserves_one_line_and_reader_ownership(string scenario)
    {
        string content = scenario switch
        {
            "terminated" => "  synthetic-secret  \r\nsecond",
            "unterminated" => "  synthetic-secret  ",
            _ => "   \nsecond"
        };
        using var reader = new StringReader(content);

        Assert.Equal(scenario == "spaces" ? "   " : "  synthetic-secret  ",
            SecretInput.ReadNonEmptyLine(reader.ReadLine, "A secret is required."));
        Assert.Equal(scenario == "unterminated" ? null : "second", reader.ReadLine());
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("eof")]
    public void Line_input_rejects_empty_or_eof_once_with_fixed_diagnostic(string scenario)
    {
        int reads = 0;
        var error = Assert.Throws<InvalidOperationException>(() => SecretInput.ReadNonEmptyLine(() =>
        {
            reads++;
            return scenario == "empty" ? "" : null;
        }, "A secret is required."));
        Assert.Equal("A secret is required.", error.Message);
        Assert.Equal(1, reads);
    }

    [Fact]
    public void Line_input_propagates_failure_without_retry()
    {
        int reads = 0;
        var failure = new IOException("synthetic read failure");
        Assert.Same(failure, Assert.Throws<IOException>(() => SecretInput.ReadNonEmptyLine(() =>
        {
            reads++;
            throw failure;
        }, "A secret is required.")));
        Assert.Equal(1, reads);
    }

    private static ConsoleKeyInfo Key(char character, ConsoleKey key = ConsoleKey.A) =>
        new(character, key, false, false, false);
}
