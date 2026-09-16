# Architecture

`Transiever.Common.Accounts` is the shared boundary between Transiever tool configuration and the operating system credential service:

```text
tool CLI -> account profile -> Accounts package -> platform credential service
```

The package owns account references, non-secret profile persistence, credential lifecycle status, and redaction.
Tools own command presentation, provider authentication, token renewal, and provider-side revocation.
ManageSieve and the provider-neutral SieveRuler library remain storage-free.
