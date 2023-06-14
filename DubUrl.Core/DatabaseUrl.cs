using DubUrl.Mapping;
using DubUrl.MicroOrm;
using DubUrl.Querying;
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

namespace DubUrl
{
    public partial class DatabaseUrl
    {
        protected ConnectionUrl ConnectionUrl { get; }
        private CommandProvisionerFactory CommandProvisionerFactory { get; }

        public DatabaseUrl(string url)
        : this(new ConnectionUrlFactory(new SchemeMapperBuilder()), url)
        { }

        public DatabaseUrl(ConnectionUrlFactory factory, string url)
            : this(factory.Instantiate(url), new CommandProvisionerFactory())
        { }

        public DatabaseUrl(ConnectionUrlFactory factory, CommandProvisionerFactory commandProvisionerFactory, string url)
            : this(factory.Instantiate(url), commandProvisionerFactory)
        { }

        internal DatabaseUrl(ConnectionUrl connectionUrl, CommandProvisionerFactory commandProvisionerFactory)
            => (ConnectionUrl, CommandProvisionerFactory) = (connectionUrl, commandProvisionerFactory);

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
            => ReadScalar(new InlineCommand(query));

        public object? ReadScalar(ICommandProvider commandProvider)
            => PrepareCommand(commandProvider).ExecuteScalar();

        public object ReadScalarNonNull(string query)
            => ReadScalarNonNull(new InlineCommand(query));

        public object ReadScalarNonNull(ICommandProvider commandProvider)
        {
            var result = ReadScalar(commandProvider);
            var typedResult = result == DBNull.Value ? null : result;
            return typedResult ?? throw new NullReferenceException();
        }

        public T? ReadScalar<T>(string query)
          => ReadScalar<T>(new InlineCommand(query));

        public T? ReadScalar<T>(string template, IDictionary<string, object?> parameters)
           => ReadScalar<T>(new InlineTemplateCommand(template, parameters));

        public T? ReadScalar<T>(ICommandProvider query)
        {
            var result = ReadScalar(query);
            return (T?)(result == DBNull.Value ? null : result);
        }

        public T ReadScalarNonNull<T>(string query)
           => ReadScalarNonNull<T>(new InlineCommand(query));

        public T ReadScalarNonNull<T>(string template, IDictionary<string, object?> parameters)
           => ReadScalarNonNull<T>(new InlineTemplateCommand(template, parameters));

        public T ReadScalarNonNull<T>(ICommandProvider query)
        {
            var result = ReadScalarNonNull(query);
            if (result is T)
                return (T)result;

            return NormalizeReturnType<T>(result);
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
            => ReadSingle(new InlineCommand(query));

        public object? ReadSingle(ICommandProvider commandProvider)
        {
            (var dyn, var dr) = ReadInternal(commandProvider);
            return !dr.Read() ? dyn : throw new InvalidOperationException();
        }

        public object ReadSingleNonNull(string query)
            => ReadSingleNonNull(new InlineCommand(query));

        public object ReadSingleNonNull(ICommandProvider commandProvider)
            => ReadSingle(commandProvider) ?? throw new InvalidOperationException();

        #endregion

        #region First 

        public object? ReadFirst(string query)
            => ReadFirst(new InlineCommand(query));

        public object? ReadFirst(ICommandProvider commandProvider)
        {
            (var dyn, _) = ReadInternal(commandProvider);
            return dyn;
        }

        public object ReadFirstNonNull(string query)
            => ReadFirstNonNull(new InlineCommand(query));

        public object ReadFirstNonNull(ICommandProvider commandProvider)
            => ReadFirst(commandProvider) ?? throw new InvalidOperationException();

        #endregion

        #region Multiple

        public IEnumerable<object> ReadMultiple(string query)
            => ReadMultiple(new InlineCommand(query));

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
           => ExecuteReader(new InlineCommand(query));

        public IDataReader ExecuteReader(ICommandProvider commandProvider)
            => PrepareCommand(commandProvider).ExecuteReader();

        #endregion


        private T NormalizeReturnType<T>(object obj)
        {
            if (typeof(T) == typeof(decimal))
                return (T)(object)Convert.ToDecimal(obj);

            if (typeof(T) == typeof(bool))
                return (T)(object)Convert.ToBoolean(obj);

            if (HasImplicitConversion(obj.GetType(), typeof(T)))
                return (T)(dynamic)obj;   

            return obj switch
            {
                string str => Parse<T>(str),
                DateTime dt => TruncateDateTime<T>(dt),
                TimeSpan ts => TimeSpanToTime<T>(ts),
                _ => throw new NotImplementedException($"Cannot normalize value from '{obj.GetType().Name}' to '{typeof(T).Name}'")
            };
        }

        private T Parse<T>(string value)
        {
#if NET7_0_OR_GREATER
                if (!(typeof(T).GetInterfaces().Any(c => c.IsGenericType && c.GetGenericTypeDefinition() == typeof(IParsable<>))))
                    throw new ArgumentOutOfRangeException($"Cannot normalize by parsing the string to type '{typeof(T).Name}' because it doesn't implement IParsable");
#endif
            var parse = typeof(T).GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(c => c.Name == "Parse"
                                        && c.GetParameters().Length == 1
                                        && c.GetParameters()[0].ParameterType == typeof(string)
                    );
            return parse == null
                ? throw new ArgumentOutOfRangeException($"Cannot normalize, by parsing the string to type '{typeof(T).Name}' because we can't find a method named Parse accepting two parameters.")
                : (T)(parse.Invoke(null, new[] { value }) ?? throw new ArgumentOutOfRangeException(nameof(value)));
        }

        private T TruncateDateTime<T>(DateTime dt)
        {
            if (typeof(T) == typeof(DateOnly))
                return (T)(object)DateOnly.FromDateTime(dt);

            if (typeof(T) == typeof(TimeOnly))
                return (T)(object)TimeOnly.FromDateTime(dt);

            throw new InvalidCastException();
        }

        private T TimeSpanToTime<T>(TimeSpan ts)
        {
            if (typeof(T) == typeof(TimeOnly))
                return (T)(object)TimeOnly.FromTimeSpan(ts);

            throw new InvalidCastException();
        }

        //https://stackoverflow.com/questions/32025201/how-can-i-determine-if-an-implicit-cast-exists-in-c
        public static bool HasImplicitConversion(Type baseType, Type targetType)
        {
            return baseType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(mi => mi.Name == "op_Implicit" && mi.ReturnType == targetType)
                .Any(mi => {
                    var pi = mi.GetParameters().FirstOrDefault();
                    return pi != null && pi.ParameterType == baseType;
                });
        }
    }
}

