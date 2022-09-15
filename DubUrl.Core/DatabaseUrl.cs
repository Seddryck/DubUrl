using DubUrl.Mapping;
using DubUrl.Querying;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl
{
    public class DatabaseUrl
    {
        protected ConnectionUrl ConnectionUrl { get; }
        protected CommandBuilder CommandBuilder { get; }

        public DatabaseUrl(string url)
        : this(new ConnectionUrlFactory(new SchemeMapperBuilder()), url)
        { }
        
        public DatabaseUrl(ConnectionUrlFactory factory, string url)
        : this(factory.Instantiate(url), new CommandBuilder())
        { }

        internal DatabaseUrl(ConnectionUrl connectionUrl, CommandBuilder commandFactory)
        {
            ConnectionUrl = connectionUrl;
            CommandBuilder = commandFactory;
        }

        public IDbConnection Connect()
            => ConnectionUrl.Connect();

        public IDbConnection Open()
            => ConnectionUrl.Open();

        public object? ReadScalar(string query)
            => ReadScalar(new InlineQuery(query));

        public object? ReadScalar(Querying.IQueryProvider queryProvider)
        {
            using var conn = ConnectionUrl.Open();
            CommandBuilder.Setup(queryProvider, ConnectionUrl.Dialect);
            var parameters = (queryProvider as IParametrizedQuery)?.Parameters;
            using var cmd = parameters == null ? CommandBuilder.Execute(conn) : CommandBuilder.Execute(conn, parameters);
            return cmd.ExecuteScalar();
        }

        public T? ReadScalar<T>(string query)
           => ReadScalar<T>(new InlineQuery(query));

        public T? ReadScalar<T>(Querying.IQueryProvider query)
        {
            var result = ReadScalar(query);
            return (T?)(result == DBNull.Value ? null : result);
        }

        public T ReadScalarNonNull<T>(string query)
           => ReadScalarNonNull<T>(new InlineQuery(query));

        public T ReadScalarNonNull<T>(Querying.IQueryProvider query)
        {
            var result = ReadScalar(query);
            var typedResult = (T?)(result == DBNull.Value ? null : result);
            return typedResult ?? throw new NullReferenceException();
        }

        public IDataReader ExecuteReader(string query)
           => ExecuteReader(new InlineQuery(query));

        public IDataReader ExecuteReader(Querying.IQueryProvider queryProvider)
        {
            using var conn = ConnectionUrl.Open();
            CommandBuilder.Setup(queryProvider, ConnectionUrl.Dialect);
            using var cmd = CommandBuilder.Execute(conn);
            return cmd.ExecuteReader();
        }
    }
}

