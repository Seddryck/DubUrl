using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl
{
    public interface IDatabaseUrl
    {
        IQueryLogger QueryLogger { get; }
        IDialect Dialect { get; }

        #region Scalar

        object? ReadScalar(string query);
        object? ReadScalar(ICommandProvider commandProvider);
        object ReadScalarNonNull(string query);
        object ReadScalarNonNull(ICommandProvider commandProvider);
        T? ReadScalar<T>(string query);
        T? ReadScalar<T>(string template, IDictionary<string, object?> parameters);
        T? ReadScalar<T>(ICommandProvider query);
        T ReadScalarNonNull<T>(string query);
        T ReadScalarNonNull<T>(string template, IDictionary<string, object?> parameters);
        T ReadScalarNonNull<T>(ICommandProvider query);

        #endregion

        #region Single

        object? ReadSingle(string query);
        object? ReadSingle(ICommandProvider commandProvider);
        object ReadSingleNonNull(string query);
        object ReadSingleNonNull(ICommandProvider commandProvider);

        #endregion

        #region First 

        object? ReadFirst(string query);
        object? ReadFirst(ICommandProvider commandProvider);
        object ReadFirstNonNull(string query);
        object ReadFirstNonNull(ICommandProvider commandProvider);

        #endregion

        #region Multiple

        IEnumerable<object> ReadMultiple(string query);
        IEnumerable<object> ReadMultiple(ICommandProvider commandProvider);

        #endregion

        #region ExecuteReader

        IDataReader ExecuteReader(string query);
        IDataReader ExecuteReader(ICommandProvider commandProvider);

        #endregion
    }
}