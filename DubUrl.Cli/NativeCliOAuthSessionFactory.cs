using System.Data.Common;
using System.Runtime.Loader;
using DubUrl.Authentication;
using DubUrl.Authentication.Snowflake;
using DubUrl.Authentication.SqlServer;

namespace DubUrl.Cli;

internal sealed class NativeCliOAuthSessionFactory : ICliOAuthSessionFactory
{
    public IInteractiveOAuthSession Create(DbConnection connection, CliOAuthRequest request, ICliConsole console)
    {
        var sql = request.Provider == "Microsoft.Data.SqlClient";
        var adapter = sql ? typeof(EntraInteractiveOAuthSession) : typeof(SnowflakeInteractiveOAuthSession);
        var context = AssemblyLoadContext.GetLoadContext(connection.GetType().Assembly) ?? AssemblyLoadContext.Default;
        if (context != AssemblyLoadContext.Default)
        {
            // Keep native type identity inside the installed connector's existing isolated context.
            var assembly = context.Assemblies.SingleOrDefault(assembly => assembly.GetName().Name == adapter.Assembly.GetName().Name)
                ?? context.LoadFromAssemblyPath(adapter.Assembly.Location);
            adapter = assembly.GetType(adapter.FullName!, throwOnError: true)!;
        }
        Func<string, string, DateTimeOffset, CancellationToken, Task> prompt = (url, code, expires, token) => PresentDeviceCodeAsync(console, url, code, expires, token);
        object?[] arguments = sql ? [request.Flow == "device-code", prompt] : [request.Account, false];
        return (IInteractiveOAuthSession)(Activator.CreateInstance(adapter, arguments)
            ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication));
    }

    internal static async Task PresentDeviceCodeAsync(ICliConsole console, string verificationUrl, string userCode, DateTimeOffset expires, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Uri.TryCreate(verificationUrl, UriKind.Absolute, out var uri) || !SafeVerificationUri(uri)
            || !SafeUserCode(userCode) || expires <= DateTimeOffset.UtcNow)
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed);
        // The user code is intentionally displayed only here. Never print MSAL's raw message/device_code or serialize it into command results.
        await console.Error.WriteLineAsync($"OAuth sign-in: open {uri.AbsoluteUri} and enter code {userCode}. Expires at {expires:HH:mm:ss} UTC.").ConfigureAwait(false);
        await console.Error.FlushAsync(token).ConfigureAwait(false);
    }
    private static bool SafeVerificationUri(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort
        && uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && (uri.Host is "microsoft.com" or "www.microsoft.com" or "login.microsoftonline.com" or "login.microsoftonline.us" or "login.chinacloudapi.cn");
    private static bool SafeUserCode(string code) => code.Length is >= 4 and <= 32
        && code.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
}
