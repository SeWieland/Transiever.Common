using System.Text;

namespace Transiever.Common.Cli;

/// <summary>Stateless secret-input mechanics; callers own source selection and presentation.</summary>
public static class SecretInput
{
    /// <summary>Reads intercepted console keys without echoing or imposing an empty-value policy.</summary>
    public static string ReadHidden() => ReadHidden(() => Console.ReadKey(intercept: true));

    internal static string ReadHidden(Func<ConsoleKeyInfo> readKey)
    {
        var secret = new StringBuilder();
        while (true)
        {
            ConsoleKeyInfo key = readKey();
            if (key.Key == ConsoleKey.Enter)
                return secret.ToString();

            if (key.Key == ConsoleKey.Backspace)
            {
                if (secret.Length > 0)
                    secret.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                secret.Append(key.KeyChar);
            }
        }
    }

    /// <summary>Reads exactly one line, preserving whitespace, or throws with the supplied fixed diagnostic.</summary>
    public static string ReadNonEmptyLine(Func<string?> readLine, string emptyInputMessage) =>
        readLine() is { Length: > 0 } secret
            ? secret
            : throw new InvalidOperationException(emptyInputMessage);
}
