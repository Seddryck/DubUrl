using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AnalysisServices.AdomdClient;

namespace DubUrl.Adomd.Wrappers;

internal class AdomdParameterWrapper : DbParameter, IDbDataParameter, IDataParameter
{
    internal IDbDataParameter InnerParameter { get; }
    private DbType dbType = DbType.Object;
    private int size = 0;
    private bool isNullable = false;

    public AdomdParameterWrapper()
        => InnerParameter = new AdomdParameter();

    public override DbType DbType 
    { 
        get => dbType; 
        set => dbType = value; 
    }
    public override ParameterDirection Direction 
    {
        get => InnerParameter.Direction;
        set => InnerParameter.Direction = value;
    }
    public override bool IsNullable
    {
        get => isNullable;
        set => isNullable = value;
    }
    [AllowNull]
    public override string ParameterName
    {
        get => InnerParameter.ParameterName;
        set => InnerParameter.ParameterName = value;
    }
    public override int Size
    {
        get => size;
        set => size = value;
    }
    [AllowNull]
    public override string SourceColumn
    {
        get => InnerParameter.SourceColumn;
        set => InnerParameter.SourceColumn = value;
    }
    public override bool SourceColumnNullMapping
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }
    public override object? Value
    {
        get => InnerParameter.Value;
        set => InnerParameter.Value = value;
    }

    public override void ResetDbType()
        => InnerParameter.DbType = DbType.Object;
}
