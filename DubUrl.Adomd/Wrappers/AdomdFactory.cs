using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Extensions;
using Microsoft.AnalysisServices.AdomdClient;

namespace DubUrl.Adomd.Wrappers
{
    /// <summary>
    /// A factory to create instances of various ADOMD.NET objects.
    /// </summary>
    [Serializable]
    [ProviderInvariantName("Microsoft.AnalysisServices.AdomdClient")]
    public sealed class AdomdFactory : DbProviderFactory, IDubUrlProviderFactoryWrapper
    {
        /// <summary>
        /// Gets an instance of the <see cref="AdomdFactory"/>.
        /// This can be used to retrieve strongly typed data objects.
        /// </summary>
        public static readonly AdomdFactory Instance = new();

        AdomdFactory() { }

        public override DbCommand CreateCommand() => throw new NotImplementedException();

        public override DbConnection CreateConnection() => new AdomdConnectionWrapper();

        public override DbParameter CreateParameter() => throw new NotImplementedException();

        public override DbConnectionStringBuilder CreateConnectionStringBuilder() => new ();

        public override DbCommandBuilder CreateCommandBuilder() => throw new NotImplementedException();

        public override DbDataAdapter CreateDataAdapter() => throw new NotImplementedException();

#if !NETSTANDARD2_0
        public override bool CanCreateDataAdapter => false;

        public override bool CanCreateCommandBuilder => false;
#endif

#if NET6_0_OR_GREATER
        public override bool CanCreateBatch => false;

        public override DbBatch CreateBatch() => throw new NotImplementedException();

        public override DbBatchCommand CreateBatchCommand() => throw new NotImplementedException();
#endif

#if NET7_0_OR_GREATER
        public override DbDataSource CreateDataSource(string connectionString)
            => throw new NotImplementedException();
#endif
    }
}
