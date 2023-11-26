using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.AnalysisServices.AdomdClient;

namespace DubUrl.Adomd.Wrappers;

internal class AdomdCommandWrapper : DbCommand
{
    public IDbCommand InnerCommand { get; }
    private new AdomdParameterCollectionWrapper Parameters { get; }

    public AdomdCommandWrapper()
    {
        InnerCommand = new AdomdCommand();
        Parameters = new AdomdParameterCollectionWrapper(InnerCommand);
    }

    public AdomdCommandWrapper(IDbConnection connection)
        : this() { InnerCommand.Connection = connection; }

    public override void Cancel()
        => InnerCommand.Cancel();

    protected override DbParameter CreateDbParameter()
        => new AdomdParameterWrapper();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        => new AdomdDataReaderWrapper(InnerCommand.ExecuteReader(behavior));

    public override int ExecuteNonQuery()
        => InnerCommand.ExecuteNonQuery();

    public override object? ExecuteScalar()
    {
        using var reader = ExecuteReader();
        if (reader.Read() && reader.FieldCount > 0)
            return reader.GetValue(0);
        return null;
    }
    public override void Prepare()
        => InnerCommand.Prepare();

    [AllowNull]
    [DefaultValue("")]
    public override string CommandText
    {
        get => InnerCommand.CommandText;
        set => InnerCommand.CommandText = value;
    }

    public override int CommandTimeout
    {
        get => InnerCommand.CommandTimeout;
        set => InnerCommand.CommandTimeout = value;
    }

    public override CommandType CommandType
    {
        get => InnerCommand.CommandType;
        set => InnerCommand.CommandType = value;
    }

    protected override DbConnection? DbConnection
    {
        get => Connection;
        set => Connection = value switch
        {
            AdomdConnectionWrapper wrapper => (DbConnection)wrapper.Connection,
            DbConnection conn => conn,
            _ => throw new NotSupportedException()
        };
    }

    protected override DbParameterCollection DbParameterCollection
        => Parameters;

    protected override DbTransaction? DbTransaction 
    { 
        get => throw new NotSupportedException(); 
        set => throw new NotSupportedException(); 
    }

    public override bool DesignTimeVisible
    {
        get => false;
        set { if (value) throw new NotImplementedException(); } 
    }

    public override UpdateRowSource UpdatedRowSource 
    { 
        get => throw new NotSupportedException(); 
        set => throw new NotSupportedException(); 
    }
}
