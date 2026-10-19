using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation;

internal class TrinoRewriter : ConnectionStringRewriter, DubUrl.Conversion.IReverseConnectionMapper
{
    public virtual ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
        => DubUrl.Conversion.Implementation.TrinoConnectionStringReverser.Convert(scheme, source);

    protected internal const string HOST_KEYWORD = "Host";
    protected internal const string HOST_DEFAULT_VALUE = "localhost";
    protected internal const string PORT_KEYWORD = "Port";
    protected internal const int PORT_DEFAULT_VALUE = 8080;
    protected internal const string CATALOG_KEYWORD = "Catalog";
    protected internal const string SCHEMA_KEYWORD = "Schema";
    protected internal const string USERNAME_KEYWORD = "User";
    protected internal const string PASSWORD_KEYWORD = "Password";
    protected internal const string HEADERS_KEYWORD = "TrinoHeaders";
    protected internal const int HEADERS_DEFAULT_VALUE = 1;

    public TrinoRewriter(DbConnectionStringBuilder csb)
        : base(new UniqueAssignmentSpecificator(csb),
              [
                new HostTokenMapper(HOST_KEYWORD, HOST_DEFAULT_VALUE),
                new PortTokenMapper(PORT_KEYWORD, PORT_DEFAULT_VALUE),
                new SegmentTokenMapper(CATALOG_KEYWORD, 0),
                new SegmentTokenMapper(SCHEMA_KEYWORD, 1),
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
                Specificator.Execute(USERNAME_KEYWORD, urlInfo.Username);
            if (!string.IsNullOrEmpty(urlInfo.Password))
                Specificator.Execute(PASSWORD_KEYWORD, urlInfo.Password);
        }
    }

    internal class OptionsMapper : Tokening.OptionsMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            if (!urlInfo.Options.ContainsKey(HEADERS_KEYWORD))
                Specificator.Execute(HEADERS_KEYWORD, HEADERS_DEFAULT_VALUE);
            base.Execute(urlInfo);
        }
    }
}
