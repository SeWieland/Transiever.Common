namespace Transiever.Common.Cli;

/// <summary>Classifies forbidden secret-option assignments without returning their values.</summary>
public static class SecretOptions
{
    /// <summary>Matches a specified option name followed immediately by '=', using ordinal-ignore-case comparison.</summary>
    public static bool IsAssignment(string argument, params ReadOnlySpan<string> optionNames)
    {
        foreach (string name in optionNames)
        {
            if (argument.Length > name.Length && argument[name.Length] == '=' &&
                argument.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
