using DubUrl.Mapping;
using DubUrl.Querying;
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
    public class Database
    {
        protected ConnectionUrl ConnectionUrl { get; }
        protected CommandFactory CommandFactory { get; }

        public Database(string url)
        : this(new ConnectionUrl(url, new SchemeMapperBuilder())
              , new CommandFactory(new EmbeddedResourceCommandReader(Assembly.GetCallingAssembly()))) 
        { }

        internal Database(ConnectionUrl connectionUrl, CommandFactory commandFactory)
        {
            ConnectionUrl = connectionUrl;
            CommandFactory = commandFactory;
        }

        public IDbConnection Connect()
            => ConnectionUrl.Connect();

        public IDbConnection Open()
            => ConnectionUrl.Open();

        public object? ExecuteScalar(string queryId)
        {
            using var conn = ConnectionUrl.Open();
            using var cmd = CommandFactory.Execute(conn, queryId, ConnectionUrl.Dialects);
            return cmd.ExecuteScalar();
        }

        public T? ExecuteScalar<T>(string queryId)
        {
            var result = ExecuteScalar(queryId);
            return (T?)(result == DBNull.Value ? null : result);
        }

        public T ExecuteNonNullScalar<T>(string queryId)
        {
            var result = ExecuteScalar(queryId);
            var typedResult = (T?)(result == DBNull.Value ? null : result);
            return typedResult ?? throw new NullReferenceException();
        }

        public IDataReader ExecuteReader(string queryId)
        {
            using var conn = ConnectionUrl.Open();
            using var cmd = CommandFactory.Execute(conn, queryId, ConnectionUrl.Dialects);
            return cmd.ExecuteReader();
        }
    }
}

