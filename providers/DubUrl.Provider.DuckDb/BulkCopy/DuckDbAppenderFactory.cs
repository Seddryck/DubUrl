using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.BulkCopy.Engines;
internal class DuckDbAppenderFactory
{
    private MethodInfo? createRowMethod;
    private MethodInfo? closeMethod;
    private MethodInfo? appendNullMethod;
    private MethodInfo? endRowMethod;
    private Dictionary<Type, MethodInfo>? appendMethods;
    private readonly IBulkCopyDependencyResolver dependencies;
    private Func<DateOnly, object>? castDateOnly;
    private Func<TimeOnly, object>? castTimeOnly;

    public DuckDbAppenderFactory() : this(BulkCopyDependencyResolver.Default) { }

    public DuckDbAppenderFactory(IBulkCopyDependencyResolver dependencies)
        => this.dependencies = dependencies;

    private Func<T, object> CreateCast<T>(Assembly driverAssembly, string typeName)
    {
        var type = dependencies.ResolveType(driverAssembly, "DuckDB.NET.Bindings", typeName);
        var cast = ReflectionHelper.GetCaster(type, typeof(T), type)
            ?? throw new InvalidOperationException($"Conversion from {typeof(T).Name} to {typeName} was not found in DuckDB.NET.Bindings.");
        return (T value) => cast.Invoke(null, [value])!;
    }

    public virtual DuckDbAppenderProxy CreateAppender(IDbConnection connection, string tableName)
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();

        var connectionType = connection.GetType();

        // DuckDB method: CreateAppender(string)
        var createAppenderMethod = connectionType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .SingleOrDefault(method =>
                method.Name == "CreateAppender"
                && !method.IsGenericMethod
                && method.GetParameters() is [{ ParameterType: var parameterType }]
                && parameterType == typeof(string))
            ?? throw new InvalidOperationException("CreateAppender method not found on connection.");
        object? appender = null;
        try
        {
            appender = createAppenderMethod.Invoke(connection, [tableName]);
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException!;
        }
        var appenderType = appender?.GetType() ?? throw new InvalidOperationException("Failed to create DuckDB appender.");

        createRowMethod ??= appenderType.GetMethod("CreateRow")!;
        closeMethod ??= appenderType.GetMethod("Close")!;

        //Try to get the output of the createRowMethod
        var appenderRowType = createRowMethod.ReturnType;

        appendMethods ??= appenderRowType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == "AppendValue" && m.GetParameters().Length == 1)
            .Select(m => new KeyValuePair<Type, MethodInfo>(m.GetParameters().First().ParameterType, m))
            .ToDictionary();
        appendNullMethod ??= appenderRowType.GetMethod("AppendNullValue")!;
        endRowMethod ??= appenderRowType.GetMethod("EndRow")!;

        return new DuckDbAppenderProxy
        (
            () =>
            {
                var row = createRowMethod.Invoke(appender, null);

                return new DuckDbAppenderRowProxy(
                    () => appendNullMethod.Invoke(appender, null),
                    value =>
                    {
                        var dbValue = value switch
                        {
                            DateOnly date => (castDateOnly ??= CreateCast<DateOnly>(connectionType.Assembly, "DuckDB.NET.Native.DuckDBDateOnly"))(date),
                            TimeOnly time => (castTimeOnly ??= CreateCast<TimeOnly>(connectionType.Assembly, "DuckDB.NET.Native.DuckDBTimeOnly"))(time),
                            _ => value
                        };
                        if (appendMethods.TryGetValue(dbValue.GetType(), out var method))
                            method.Invoke(row, [dbValue]);
                        else
                        {
                            var type = dbValue.GetType();
                            var nullableType = type.IsValueType ? typeof(Nullable<>).MakeGenericType(type) : type;
                            if (!appendMethods.TryGetValue(nullableType, out method))
                                throw new InvalidOperationException($"No suitable AppendValue method found for type {type.Name}.");
                            method.Invoke(row, [CastToNullable(nullableType, dbValue)]);
                        }
                    },
                    () => endRowMethod.Invoke(row, null)
                );
            },
            () => closeMethod.Invoke(appender, null)
        );
    }

    private static object? CastToNullable(Type nullableType, object value)
    {
        if (!nullableType.IsGenericType || nullableType.GetGenericTypeDefinition() != typeof(Nullable<>))
            throw new ArgumentException("Type must be Nullable<T>");

        if (value == null || value == DBNull.Value)
            return null;

        var underlyingType = Nullable.GetUnderlyingType(nullableType)!;
        return Convert.ChangeType(value, underlyingType);
    }
}
