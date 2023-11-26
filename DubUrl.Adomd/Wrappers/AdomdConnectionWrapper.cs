using DubUrl.Extensions;
using Microsoft.AnalysisServices.AdomdClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Wrappers;

internal class AdomdConnectionWrapper : DbConnection, IDbConnection, IDubUrlConnectionWrapper
{
    protected IDbConnection InnerConnection { get; }
    public AdomdConnectionWrapper()
        => InnerConnection = new AdomdConnection();

    public IDbConnection Connection => InnerConnection;

    [AllowNull()]
    [DefaultValue("")]
    public override string ConnectionString 
    { 
        get => Connection.ConnectionString; 
        set => Connection.ConnectionString = value; 
    }

    public override string Database => Connection.Database;

    public override string DataSource => throw new NotImplementedException();

    public override string ServerVersion
        => (InnerConnection as AdomdConnection)?.ServerVersion
                ?? throw new NotSupportedException();

    public override ConnectionState State => Connection.State;

    public override void ChangeDatabase(string databaseName) => Connection.ChangeDatabase(databaseName);
    public override void Close() => Connection.Close();
    public override void Open() => Connection.Open();
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotImplementedException();
    protected override DbCommand CreateDbCommand() => new AdomdCommandWrapper(InnerConnection);
}
