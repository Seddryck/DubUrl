# Configuration-backed credentials

`DubUrl.Extensions` provides `ConfigurationUsernamePasswordCredentialSource` in
`DubUrl.Extensions.Configuration`. Supply an IConfiguration root or section and
explicit string keys:

```csharp
var source = new ConfigurationUsernamePasswordCredentialSource(configuration,
    usernameKey: "DatabaseCredentials:Username", passwordKey: "DatabaseCredentials:Password");
var target = new ConnectionUrl("postgres://db.example.com/app")
    .WithAuthentication(ConnectionAuthentication.UsernamePassword(source));
await using var connection = await target.OpenAsync(cancellationToken);
```

For URL usernames pass `usernameKey: null`. Values are not trimmed, decoded,
interpolated or cached. Null is absent; empty is explicit. Construction/Parse do
not read credentials; each new connection resolves current configuration values.
Sync and async sources share synchronous IConfiguration reads, with cancellation
checked before reading. The adapter does not reload or dispose configuration or
fetch vault secrets itself. Separate reads are not atomic during reload: callers
needing a coherent pair should supply a source with appropriate synchronization.
Provider caches/refresh mean current configuration may lag the remote secret.

## Azure Key Vault application setup

Add application packages `Azure.Extensions.AspNetCore.Configuration.Secrets` and
`Azure.Identity` (outside core/the generic adapter):

```csharp
using var configuration = new ConfigurationBuilder()
    .AddAzureKeyVault(new Uri("https://<vault-name>.vault.azure.net/"),
        new DefaultAzureCredential()).Build();
```

Provision `DatabaseCredentials--Username` and `DatabaseCredentials--Password`;
the provider maps double hyphens to configuration colons. Use developer identity
locally and managed/workload identity in deployment with the provider's required
secret list/read permissions. Configure ReloadInterval when refresh is needed.
Configuration startup can fetch secrets before DubUrl is called; lazy credential
resolution does not make that startup lazy. See the
[provider's packages and key mapping](https://learn.microsoft.com/en-us/aspnet/core/security/key-vault-configuration).

## AWS Secrets Manager through Systems Manager

Add application package `Amazon.Extensions.Configuration.SystemsManager`:

```csharp
using var configuration = new ConfigurationBuilder()
    .AddSystemsManager("/aws/reference/secretsmanager/duburl/database").Build();
var source = new ConfigurationUsernamePasswordCredentialSource(configuration,
    usernameKey: "Username", passwordKey: "Password");
```

Store a JSON object with Username/Password properties in Secrets Manager under
`duburl/database`. The AWS extension's Secrets Manager-reference JSON processor
produces those configuration keys. Configure the SDK region/credential chain and
SSM GetParametersByPath, Secrets Manager GetSecretValue and applicable KMS decrypt
permissions. Prefer workload roles and configure reloadAfter when refresh is needed.
This is the SSM reference route, not a direct Secrets Manager provider; see the
[AWS extension documentation](https://github.com/aws/aws-dotnet-extensions-configuration).

In-memory/fake-provider tests cover roots/sections, lazy reads, rotation, exact
strings, null/empty, cancellation and sanitized errors. Cloud snippets follow the
documented application APIs; they were not compiled or exercised against live
Azure/AWS services and no cloud versions are claimed as validated dependencies.
No cloud SDK was added to core or this adapter. Caller owns provider lifetimes.
