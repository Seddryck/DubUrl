using DubUrl.Provider.Oracle.Conversion;
using DubUrl.Rewriting;
using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Oracle.Rewriting;

internal class OracleManagedDataAccessRewriter : ConnectionStringRewriter, DubUrl.Conversion.IReverseConnectionMapper
{
    public virtual ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
        => DubUrl.Provider.Oracle.Conversion.OracleConnectionStringReverser.Convert(scheme, source);

    private const string EXCEPTION_DATABASE_NAME = "Oracle Managed Data Access";
    protected internal const string DATASOURCE_KEYWORD = "DATA SOURCE";
    protected internal const string SERVER_KEYWORD = "HOST";
    protected internal const string PORT_KEYWORD = "PORT";
    protected internal const string DATABASE_KEYWORD = "SERVICE_NAME";
    protected internal const string USERNAME_KEYWORD = "USER ID";
    protected internal const string PASSWORD_KEYWORD = "PASSWORD";

    public OracleManagedDataAccessRewriter(DbConnectionStringBuilder csb)
        : base(new Specificator(csb),
              [
                new DsnMapper(),
                new AuthentificationMapper(),
                new OptionsMapper(),
              ]
        )
    { }

    internal class AuthentificationMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            if (!string.IsNullOrEmpty(urlInfo.Username))
            {
                Specificator.Execute(USERNAME_KEYWORD, urlInfo.Username);
                if (!string.IsNullOrEmpty(urlInfo.Password))
                    Specificator.Execute(PASSWORD_KEYWORD, urlInfo.Password);
            }
            else
            {
                Specificator.Execute(USERNAME_KEYWORD, "/");
                Specificator.Execute(PASSWORD_KEYWORD, string.Empty);
            }
        }
    }

    internal class DsnMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            //If only host is specified, it's the TNS name
            if (urlInfo.Segments.Count == 0 && urlInfo.Port == 0)
                Specificator.Execute("DATA SOURCE", urlInfo.Host);

            //If segment is specified then it's the ConnectDescriptor
            else if (urlInfo.Segments.Count == 1)
                Specificator.Execute("DATA SOURCE",
                    $"(DESCRIPTION=(ADDRESS=(PROTOCOL=tcp)" +
                    $"({SERVER_KEYWORD}={urlInfo.Host})({PORT_KEYWORD}={(urlInfo.Port > 0 ? urlInfo.Port : 1521)}))(CONNECT_DATA=" +
                    $"({DATABASE_KEYWORD}={urlInfo.Segments.First()})))");
            else
                throw new InvalidConnectionUrlTooManySegmentsException(EXCEPTION_DATABASE_NAME, urlInfo.Segments);
        }
    }
}
