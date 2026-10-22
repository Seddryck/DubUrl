using System.Data.Common;
using DubUrl.Discovery;
using DubUrl.Provider.SqlServer.Discovery;
using DubUrl.Provider.SqlServer.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Schema.Testing.Contracts;
using Microsoft.Data.SqlClient;

namespace DubUrl.Provider.MsSqlServer.QA;

public sealed class SqlServerDiscoveryTests : DiscoveryContract
{
    protected override DbConnection? CreateConfiguredConnection() => Environment.GetEnvironmentVariable("DUBURL_SQLSERVER_DISCOVERY_QA_CONNECTION_STRING") is { Length: > 0 } value ? new SqlConnection(value) : null;
    protected override ISchemaMetadataReader CreateReader() => new SqlServerMetadataReader();
    protected override bool IsSqlServer => true;
    protected override IDialect CreateDialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["mssql"]);
        return builder.Build().Get<TSqlDialect>();
    }
}
