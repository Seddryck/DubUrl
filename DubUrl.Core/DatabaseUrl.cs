using DubUrl.Mapping;
using DubUrl.Querying;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using System;
using System.Collections;
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

        public object? ReadScalar(string query)
            => ReadScalar(new InlineCommand(query));

        public object? ReadScalar(ICommandProvider commandProvider)
        {
            using var conn = ConnectionUrl.Open();
            using var cmd = conn.CreateCommand();
            var provisioners = CommandProvisionerFactory.Instantiate(commandProvider, ConnectionUrl);
            foreach (var provisioner in provisioners)
                provisioner.Execute(cmd);
            return cmd.ExecuteScalar();
        }

        public T? ReadScalar<T>(string query)
           => ReadScalar<T>(new InlineCommand(query));

        public T? ReadScalar<T>(ICommandProvider query)
        {
            var result = ReadScalar(query);
            return (T?)(result == DBNull.Value ? null : result);
        }

        public object ReadScalarNonNull(string query)
            => ReadScalarNonNull(new InlineCommand(query));

        public object ReadScalarNonNull(ICommandProvider commandProvider)
        {
            var result = ReadScalar(commandProvider);
            var typedResult = result == DBNull.Value ? null : result;
            return typedResult ?? throw new NullReferenceException();
        }

        public T ReadScalarNonNull<T>(string query)
           => ReadScalarNonNull<T>(new InlineCommand(query));

        public T ReadScalarNonNull<T>(ICommandProvider query)
            => (T)ReadScalarNonNull(query);

        public IDataReader ExecuteReader(string query)
           => ExecuteReader(new InlineCommand(query));

        public IDataReader ExecuteReader(ICommandProvider commandProvider)
        {
            using var conn = ConnectionUrl.Open();
            using var cmd = conn.CreateCommand();
            var provisioners = CommandProvisionerFactory.Instantiate(commandProvider, ConnectionUrl);
            foreach (var provisioner in provisioners)
                provisioner.Execute(cmd);
            return cmd.ExecuteReader();
        }
    }
}

