# Transiever.Common.Cli

Small, stateless .NET 10 secret-input mechanics for command-line tools.
The package uses only runtime libraries and supports trimming and Native AOT.
It has no dependency on `Transiever.Common.Accounts`.

This package is currently an unpublished candidate.
Use an explicit local package source for [candidate validation](https://github.com/SeWieland/Transiever.Common#development).
After publication, install it from NuGet.org:

```bash
dotnet add package Transiever.Common.Cli
```

## Hidden terminal input

The host selects one input source and writes its own prompt and newline.
For eligible terminal input:

```csharp
using Transiever.Common.Cli;

string hidden = SecretInput.ReadHidden();
```

`ReadHidden` intercepts console keys without echo or masking.
Enter returns the string; Backspace removes one UTF-16 code unit when present; other control characters are ignored.
An empty string is returned unchanged.
The host checks terminal eligibility and decides whether an empty secret is valid.

## Explicit line input

For a host-selected line-input source, instead of the hidden terminal reader:

```csharp
string line = SecretInput.ReadNonEmptyLine(
    Console.ReadLine, "Standard input did not contain a password.");
```

`ReadNonEmptyLine` calls the supplied reader once and preserves whitespace and final lines without terminators.
Null or empty input throws `InvalidOperationException` with the host's supplied fixed diagnostic.
It never retries, disposes the reader, or chooses another input source.
Read failures propagate to the host.

## Option assignment matching

```csharp
string argument = "--password=synthetic-secret";
bool forbidden = SecretOptions.IsAssignment(
    argument, "--password", "--password-stdin");
```

`IsAssignment` matches only a supplied option name immediately followed by `=`, using ordinal-ignore-case comparison.
It returns a classification, never an assigned value.
The host retains bare-option rejection, traversal, supported spellings, and redacted diagnostics.

## Host responsibilities

Hosts own configuration precedence, environment and account access, prompts, authentication timing, and exit behavior.
This package does not retain secrets in shared state or provide stronger memory erasure than its string-based callers.
See the [repository overview](https://github.com/SeWieland/Transiever.Common) for development and publication guidance.
