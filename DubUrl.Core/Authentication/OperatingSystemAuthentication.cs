using System.Data.Common;

namespace DubUrl.Authentication;

public abstract partial class ConnectionAuthentication
{
    /// <summary>Uses the current process identity or existing ticket cache.</summary>
    public static ConnectionAuthentication OperatingSystemIdentity() => new OperatingSystemAuthentication();

    private sealed class OperatingSystemAuthentication : ConnectionAuthentication
    {
        public override ConnectionAuthenticationKind Kind => ConnectionAuthenticationKind.OperatingSystemIdentity;
        public override UsernamePasswordCredentials? Resolve(AuthenticationContext context)
        {
            OperatingSystemIdentityMappings.ValidateProvider(context);
            return null;
        }
        public override ValueTask<UsernamePasswordCredentials?> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Resolve(context));
        }
        public override void Configure(DbConnection connection, AuthenticationContext context)
        {
            OperatingSystemIdentityMappings.ValidateProvider(context);
            var builder = new DbConnectionStringBuilder(context.ProviderInvariantName == "System.Data.Odbc")
                { ConnectionString = connection.ConnectionString };
            OperatingSystemIdentityMappings.Configure(builder, context);
            connection.ConnectionString = builder.ConnectionString;
        }
    }
}
