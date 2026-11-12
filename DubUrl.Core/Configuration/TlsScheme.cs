using DubUrl.Mapping;
using DubUrl.Parsing;

namespace DubUrl;

internal static class TlsScheme
{
    internal static ConnectionConfigurationRequest DefaultRequest { get; } =
        new(ConnectionConfigurationOption.Encryption, ConnectionEncryption.Tls(), false);

    internal static void ValidateTransport(SchemeKey scheme, ConnectionConfigurationCapability? capability)
    {
        if (scheme.RequiresHttpsTransport && capability?.SupportsHttpsTransport != true)
            throw new NotSupportedException("HTTPS scheme modifiers are unsupported for the selected provider route.");
    }

    internal static ResolvedConnection Resolve(ConnectionUri uri, ISchemeRegistry registry,
        Func<ConnectionUri, ResolvedConnection> resolve)
    {
        if (!uri.Scheme.RequiresTls) return resolve(uri);

        var providerUri = uri.WithProviderScheme();
        var resolved = resolve(providerUri);
        registry.TryGetCapability<ConnectionConfigurationCapability>(providerUri.Scheme, out var capability);
        ValidateTransport(uri.Scheme, capability);
        if (capability?.Supports(DefaultRequest) != true)
            throw new NotSupportedException("Verified TLS is unsupported for the selected provider route.");

        var builder = resolved.ProviderFactory.CreateConnectionStringBuilder()
            ?? throw new NotSupportedException("The selected provider does not supply a connection-string builder for TLS.");
        builder.ConnectionString = resolved.ConnectionString;
        capability.Apply(builder, DefaultRequest);
        return resolved with { ConnectionString = builder.ConnectionString };
    }
}
