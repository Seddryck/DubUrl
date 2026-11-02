using DubUrl.Mapping;
namespace DubUrl.ProviderTesting;
public abstract class ConnectionQaContract : PasswordAuthenticationQaContract
{
    protected abstract string CliConnectionUrl { get; }
    protected virtual ISchemeRegistry CliRegistry => DefaultRegistry.Get();
    protected override string AuthenticationConnectionUrl => CliConnectionUrl;
    protected override ISchemeRegistry AuthenticationRegistry => CliRegistry;
    protected virtual string CliTableName => "Customer";
    protected virtual string? CliEmptyTableName => null;
    protected virtual bool CliCanCreateTable => true;
}
