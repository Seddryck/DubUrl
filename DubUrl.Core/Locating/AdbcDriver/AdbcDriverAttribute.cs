using DubUrl.Mapping;

namespace DubUrl.Locating.AdbcDriver;

/// <summary>Associates an ADBC URL mapper with a database.</summary>
public sealed class AdbcDriverAttribute<M, D> : LocatorAttribute
    where M : IMapper
    where D : IDatabase
{
    public AdbcDriverAttribute() : base(string.Empty, [], typeof(M), typeof(D)) { }
}
