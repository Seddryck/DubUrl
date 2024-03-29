﻿using DubUrl.Mapping;
using DubUrl.MicroOrm;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using DubUrl.Querying.Templating;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl;

public partial class DatabaseUrl : IDatabaseUrl
{
    protected ConnectionUrl ConnectionUrl { get; }
    protected CommandProvisionerFactory CommandProvisionerFactory { get; }

    protected ICaster[] Casters { get; } = [];

    public IQueryLogger QueryLogger { get; }

    public IDialect Dialect { get => ConnectionUrl.Dialect; }

    public DatabaseUrl(string url)
        : this(new ConnectionUrlFactory(new SchemeMapperBuilder()).Instantiate(url), new(), NullQueryLogger.Instance)
    { }

    public DatabaseUrl(ConnectionUrlFactory factory, string url)
        : this(factory.Instantiate(url), new(), NullQueryLogger.Instance)
    { }

    internal DatabaseUrl(ConnectionUrlFactory factory, CommandProvisionerFactory commandProvisionerFactory, string url)
        : this(factory.Instantiate(url), commandProvisionerFactory, NullQueryLogger.Instance)
    { }

    public DatabaseUrl(ConnectionUrl connectionUrl, CommandProvisionerFactory commandProvisionerFactory, IQueryLogger logger)
        => (ConnectionUrl, CommandProvisionerFactory, QueryLogger) = (connectionUrl, commandProvisionerFactory, logger);

    protected virtual IDbCommand PrepareCommand(ICommandProvider commandProvider)
    {
        var conn = ConnectionUrl.Open();
        var cmd = conn.CreateCommand();
        var provisioners = CommandProvisionerFactory.Instantiate(commandProvider, ConnectionUrl);
        foreach (var provisioner in provisioners)
            provisioner.Execute(cmd);
        return cmd;
    }

    #region Scalar

    public object? ReadScalar(string query)
        => ReadScalar(new InlineCommand(query, QueryLogger));

    public object? ReadScalar(ICommandProvider commandProvider)
        => PrepareCommand(commandProvider).ExecuteScalar();

    public object ReadScalarNonNull(string query)
        => ReadScalarNonNull(new InlineCommand(query, QueryLogger));

    public object ReadScalarNonNull(ICommandProvider commandProvider)
    {
        var result = ReadScalar(commandProvider);
        var typedResult = result == DBNull.Value ? null : result;
        return typedResult ?? throw new NullReferenceException();
    }

    public T? ReadScalar<T>(string query)
      => ReadScalar<T>(new InlineCommand(query, QueryLogger));

    public T? ReadScalar<T>(string template, IDictionary<string, object?> parameters)
       => ReadScalar<T>(new InlineTemplateCommand(template, parameters, QueryLogger));

    public T? ReadScalar<T>(ICommandProvider query)
    {
        var result = ReadScalar(query);
        return (T?)(result == DBNull.Value ? null : result);
    }

    public T ReadScalarNonNull<T>(string query)
       => ReadScalarNonNull<T>(new InlineCommand(query, QueryLogger));

    public T ReadScalarNonNull<T>(string template, IDictionary<string, object?> parameters)
       => ReadScalarNonNull<T>(new InlineTemplateCommand(template, parameters, QueryLogger));

    public T ReadScalarNonNull<T>(ICommandProvider query)
    {
        var result = ReadScalarNonNull(query);
        if (result is T t)
            return t;

        return NormalizeReturnType<T>(result) ?? throw new NullReferenceException();
    }

    #endregion

    #region Single

    protected (object?, IDataReader) ReadInternal(ICommandProvider commandProvider)
    {
        using var dr = PrepareCommand(commandProvider).ExecuteReader();
        if (!dr.Read())
            return (null, dr);
        return (dr.ToExpandoObject(), dr);
    }

    public object? ReadSingle(string query)
        => ReadSingle(new InlineCommand(query, QueryLogger));

    public object? ReadSingle(ICommandProvider commandProvider)
    {
        (var dyn, var dr) = ReadInternal(commandProvider);
        return !dr.Read() ? dyn : throw new InvalidOperationException();
    }

    public object ReadSingleNonNull(string query)
        => ReadSingleNonNull(new InlineCommand(query, QueryLogger));

    public object ReadSingleNonNull(ICommandProvider commandProvider)
        => ReadSingle(commandProvider) ?? throw new InvalidOperationException();

    #endregion

    #region First 

    public object? ReadFirst(string query)
        => ReadFirst(new InlineCommand(query, QueryLogger));

    public object? ReadFirst(ICommandProvider commandProvider)
    {
        (var dyn, _) = ReadInternal(commandProvider);
        return dyn;
    }

    public object ReadFirstNonNull(string query)
        => ReadFirstNonNull(new InlineCommand(query, QueryLogger));

    public object ReadFirstNonNull(ICommandProvider commandProvider)
        => ReadFirst(commandProvider) ?? throw new InvalidOperationException();

    #endregion

    #region Multiple

    public IEnumerable<object> ReadMultiple(string query)
        => ReadMultiple(new InlineCommand(query, QueryLogger));

    public IEnumerable<object> ReadMultiple(ICommandProvider commandProvider)
    {
        using var dr = PrepareCommand(commandProvider).ExecuteReader();
        while (dr.Read())
            yield return dr.ToExpandoObject();
        dr?.Close();
    }

    #endregion

    #region ExecuteReader

    public IDataReader ExecuteReader(string query)
       => ExecuteReader(new InlineCommand(query, QueryLogger));

    public IDataReader ExecuteReader(ICommandProvider commandProvider)
        => PrepareCommand(commandProvider).ExecuteReader();

    #endregion

    private T? NormalizeReturnType<T>(object obj)
    {
        var caster = ConnectionUrl.Dialect.Casters
                        .FirstOrDefault(x => x.CanCast(obj.GetType(), typeof(T)))
                        ?? throw new InvalidOperationException($"Cannot find any caster to transform from type '{obj.GetType().Name}' to the type '{typeof(T).Name}'");
        return (T?)caster.Cast(obj);
    }
}

