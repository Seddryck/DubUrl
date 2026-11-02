using DubUrl.Mapping;
namespace DubUrl.ProviderTesting;
public abstract class AuthenticationQaContract
{
    protected abstract string AuthenticationConnectionUrl { get; }
    protected abstract ISchemeRegistry AuthenticationRegistry { get; }
}
