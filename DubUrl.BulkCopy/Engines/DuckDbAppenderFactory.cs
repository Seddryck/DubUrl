using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Antlr.Runtime.Misc;

namespace DubUrl.BulkCopy.Engines;
internal class DuckDbAppenderFactory
{
    private MethodInfo? createRowMethod;
    private MethodInfo? closeMethod;
    private MethodInfo? appendNullMethod;
    private MethodInfo? endRowMethod;
    private Dictionary<Type, MethodInfo>? appendMethods;

    public virtual DuckDbAppenderProxy CreateAppender(IDbConnection connection, string tableName)
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();

        var connectionType = connection.GetType();

        // DuckDB method: CreateAppender(string)
        var createAppenderMethod = connectionType.GetMethod("CreateAppender", new[] { typeof(string) })
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
            .Where(m => m.Name == "AppendValue" && m.GetParameters().Count() == 1)
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
                        if (appendMethods.TryGetValue(value.GetType(), out var method))
                            method.Invoke(row, [value]);
                        else
                        {
                            var type = value.GetType();
                            var nullableType = type.IsValueType ? typeof(Nullable<>).MakeGenericType(type) : type;
                            if (!appendMethods.TryGetValue(nullableType, out method))
                                throw new InvalidOperationException($"No suitable AppendValue method found for type {type.Name}.");
                            method.Invoke(row, [CastToNullable(nullableType, value)]);
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
