using System.Data.Common;
using DubUrl.Provider.DuckDb.Connectivity;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Provider.DuckDb.Rewriting;
using DubUrl.Mapping;

namespace DubUrl.Provider.DuckDb.Mapping;

[WrapperMapper<AdbcConnectivity, PositionalParametrizer>("DubUrl.Provider.DuckDb.Adbc")]
public class DuckdbAdbcMapper : BaseMapper, IFileBasedMapper
{
    public DuckdbAdbcMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer, string rootPath)
        : base(new DuckdbRewriter(csb, rootPath, "path"), dialect, parametrizer) { }
}
