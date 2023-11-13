using DubUrl.Mapping;
using DubUrl.MicroOrm;
using DubUrl.Querying;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
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

namespace DubUrl.MicroOrm
{
    public class DatabaseUrl : DubUrl.DatabaseUrl
    {
        private IReflectionCache ReflectionCache { get; set; } = new ReflectionCache();

        public DatabaseUrl(ConnectionUrl connectionUrl, CommandProvisionerFactory commandProvisionerFactory, IReflectionCache reflectionCache, IQueryLogger logger)
            : base(connectionUrl, commandProvisionerFactory, logger)
        { ReflectionCache = reflectionCache; }

        public DatabaseUrl WithoutCache()
        {
            ReflectionCache = new NoneReflectionCache();
            return this;
        }

        #region Scalar

       
        #endregion

        #region Single

        public T? ReadSingle<T>(string query) where T : new()
            => ReadSingle<T>(new InlineSqlProvider(query, QueryLogger));

        public T? ReadSingle<T>(ICommandProvider commandProvider) where T : new()
        {
            using var dr = PrepareCommand(commandProvider).ExecuteReader();
            if (!dr.Read())
                return (T?)(object?)null;

            var entity = dr.ToEntity<T>(ReflectionCache);
            return !dr.Read() ? entity : throw new InvalidOperationException();
        }

        public T? ReadSingleNonNull<T>(string query) where T : new()
            => ReadSingle<T>(query);

        public T? ReadSingleNonNull<T>(ICommandProvider commandProvider) where T : new()
            => ReadSingle<T>(commandProvider) ?? throw new InvalidOperationException();

        #endregion

        #region First 

        public T? ReadFirst<T>(string query) where T : new()
            => ReadFirst<T>(new InlineSqlProvider(query, QueryLogger));

        public T? ReadFirst<T>(ICommandProvider commandProvider) where T : new()
        {
            using var dr = PrepareCommand(commandProvider).ExecuteReader();
            if (!dr.Read())
                return (T?)(object?)null;

            return dr.ToEntity<T>(ReflectionCache);
        }

        public T ReadFirstNonNull<T>(ICommandProvider commandProvider) where T : new()
            => ReadFirst<T>(commandProvider) ?? throw new InvalidOperationException();

        #endregion

        #region Multiple

        public IEnumerable<T> ReadMultiple<T>(string query) where T : new()
            => ReadMultiple<T>(new InlineSqlProvider(query, QueryLogger));

        public IEnumerable<T> ReadMultiple<T>(ICommandProvider commandProvider) where T : new()
        {
            using var cmd = PrepareCommand(commandProvider);
            var dr = cmd.ExecuteReader();
            while (dr.Read())
                yield return dr.ToEntity<T>(ReflectionCache);
            dr?.Close();
        }

        #endregion

    }
}

