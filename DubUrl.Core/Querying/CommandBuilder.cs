using DubUrl.Querying.Dialecting;
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

namespace DubUrl.Querying
{
    public class CommandBuilder
    {
        private bool IsSetup { get; set; } = false;
        private IQueryProvider? QueryProvider { get; set; }
        private IDialect? Dialect { get; set; }

        public virtual void Setup(IQueryProvider queryProvider, IDialect dialect)
        {
            (QueryProvider, Dialect) = (queryProvider, dialect);
            IsSetup = true;
        }

        public virtual IDbCommand Execute(IDbConnection conn)
        {
            if (!IsSetup)
                throw new InvalidOperationException($"Invoke the method '{nameof(Setup)}' before going the method '{nameof(Execute)}'.");

            if (!QueryProvider!.Exists(Dialect!, true))
                throw new ArgumentException($"A query for the dialect '{Dialect!}' cannot be found.");

            var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = QueryProvider.Read(Dialect!);

            return cmd;
        }

        public virtual IDbCommand Execute(IDbConnection conn, IEnumerable<DubUrlParameter> parameters)
        {
            var cmd = Execute(conn);
            foreach (var parameter in parameters)
            {
                var param = CreateParameter(cmd, parameter);
                cmd.Parameters.Add(param);
            }
            return cmd;
        }

        protected internal IDbDataParameter CreateParameter(IDbCommand command, DubUrlParameter parameter)
        {
            var param = command.CreateParameter();
            param.ParameterName = parameter.Name;
            param.Direction = ParameterDirection.Input;
            switch (parameter)
            {
                case IParameterPrecisionable precParam:
                    param.Precision = precParam.Precision;
                    param.Scale = precParam.Scale;
                    break;
                case IParameterSizable sizeParam:
                    param.Size = sizeParam.Size;
                    break;
                default:
                    break;
            }

            param.DbType = parameter.GetType().GetCustomAttribute<TypeMappingAttribute>()?.DbType
                ?? throw new ArgumentException($"Parameter '{parameter.Name}' is of type '{parameter.GetType().Name}' but this type is not associated with an attribute of type '{nameof(TypeMappingAttribute)}'");
            param.Value = parameter.Value ?? DBNull.Value;
            return param;
        }
    }
}
