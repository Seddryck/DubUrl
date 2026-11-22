using System.Data.Common;

namespace DubUrl.Authentication;

public abstract partial class ConnectionAuthentication
{
    /// <summary>Uses the current process identity or existing ticket cache.</summary>
    public static ConnectionAuthentication OperatingSystemIdentity() => new OperatingSystemAuthentication();

    private sealed class OperatingSystemAuthentication : ConnectionAuthentication
    {
        public override ConnectionAuthenticationKind Kind => ConnectionAuthenticationKind.OperatingSystemIdentity;
        internal override bool UsesOperatingSystemIdentityAdapter => true;
        public override UsernamePasswordCredentials? Resolve(AuthenticationContext context)
        {
            if (context.OperatingSystemIdentityAdapter is null) OperatingSystemIdentityMappings.ValidateProvider(context);
            return null;
        }
        public override ValueTask<UsernamePasswordCredentials?> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Resolve(context));
        }
        public override void Configure(DbConnection connection, AuthenticationContext context)
        {
            if (context.OperatingSystemIdentityAdapter is null) OperatingSystemIdentityMappings.ValidateProvider(context);
            var builder = new DbConnectionStringBuilder(context.ProviderInvariantName == "System.Data.Odbc")
                { ConnectionString = connection.ConnectionString };
            if (context.OperatingSystemIdentityAdapter is { } adapter)
                adapter.Configure(builder);
            else
                OperatingSystemIdentityMappings.Configure(builder, context);
            connection.ConnectionString = builder.ConnectionString;
        }
    }
}
