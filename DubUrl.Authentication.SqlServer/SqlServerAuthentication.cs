using System.Data.Common;
using DubUrl.Authentication;
using Microsoft.Data.SqlClient;

namespace DubUrl.Authentication.SqlServer;

/// <summary>Native request fields and native expiration are preserved without a universal token abstraction.</summary>
public interface ISqlServerTokenSource
{
    ValueTask<SqlAuthenticationToken> GetTokenAsync(SqlAuthenticationParameters request, CancellationToken cancellationToken = default);
}

public static class SqlServerAuthentication
{
    public static ConnectionAuthentication TokenCallback(ISqlServerTokenSource source)
        => new TokenAuthentication(source ?? throw new ArgumentNullException(nameof(source)));

    private sealed class TokenAuthentication : ConnectionAuthentication
    {
        private readonly Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> callback;
        public TokenAuthentication(ISqlServerTokenSource source)
        {
            // Exactly one delegate per strategy: SqlClient includes delegate identity in pool keys.
            callback = async (request, cancellationToken) =>
            {
                try
                {
                    var token = await source.GetTokenAsync(request, cancellationToken).ConfigureAwait(false);
                    if (token is null || token.ExpiresOn <= DateTimeOffset.UtcNow)
                        throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed);
                    return token;
                }
                catch (OperationCanceledException) { throw; }
                catch (ConnectionAuthenticationException) { throw; }
                catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed); }
            };
        }
        public override ConnectionAuthenticationKind Kind => ConnectionAuthenticationKind.NativeToken;
        public override UsernamePasswordCredentials? Resolve(AuthenticationContext context)
        {
            if (context.ProviderInvariantName != "Microsoft.Data.SqlClient")
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
            return null;
        }
        public override void Configure(DbConnection connection, AuthenticationContext context)
        {
            if (connection is not SqlConnection sql || context.ProviderInvariantName != "Microsoft.Data.SqlClient")
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
            var builder = new SqlConnectionStringBuilder(sql.ConnectionString)
            { IntegratedSecurity = false, TrustServerCertificate = false };
            if (builder.Encrypt == SqlConnectionEncryptOption.Optional) builder.Encrypt = SqlConnectionEncryptOption.Mandatory;
            builder.Remove("User ID");
            builder.Remove("Password");
            builder.Remove("Authentication");
            sql.ConnectionString = builder.ConnectionString;
            sql.AccessTokenCallback = callback;
        }
    }
}
