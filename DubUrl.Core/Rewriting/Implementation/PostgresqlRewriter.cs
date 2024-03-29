﻿using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation;

internal class PostgresqlRewriter : ConnectionStringRewriter
{
    private const string EXCEPTION_DATABASE_NAME = "Postgresql";
    protected internal const string SERVER_KEYWORD = "Host";
    protected internal const string PORT_KEYWORD = "Port";
    protected internal const string DATABASE_KEYWORD = "Database";
    protected internal const string USERNAME_KEYWORD = "Username";
    protected internal const string PASSWORD_KEYWORD = "Password";
    protected internal const string SSPI_KEYWORD = "Integrated Security";

    public PostgresqlRewriter(DbConnectionStringBuilder csb)
        : base(   new Specificator(csb),
                  [
                    new HostMapper(),
                    new PortMapper(),
                    new AuthentificationMapper(),
                    new DatabaseMapper(),
                    new OptionsMapper(),
                  ]
        )
    { }

    protected PostgresqlRewriter(ISpecificator specificator, BaseTokenMapper[] tokenMappers)
        : base(specificator, tokenMappers) { }

    internal bool IsIntegratedSecurityAllowed
        => (TokenMappers.Single(x => x is AuthentificationMapper) as AuthentificationMapper)
                ?.IsIntegratedSecurityAllowed
                ?? throw new InvalidOperationException();

    internal class HostMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
            => Specificator.Execute(SERVER_KEYWORD, urlInfo.Host);
    }

    internal class PortMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            if (urlInfo.Port > 0)
                Specificator.Execute(PORT_KEYWORD, urlInfo.Port);
        }
    }

    internal class AuthentificationMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            if (!string.IsNullOrEmpty(urlInfo.Username))
                Specificator.Execute(USERNAME_KEYWORD, urlInfo.Username);
            if (!string.IsNullOrEmpty(urlInfo.Password))
                Specificator.Execute(PASSWORD_KEYWORD, urlInfo.Password);

            if (IsIntegratedSecurityAllowed)
            {
                if (string.IsNullOrEmpty(urlInfo.Username) && string.IsNullOrEmpty(urlInfo.Password))
                    Specificator.Execute(SSPI_KEYWORD, true);
                else
                    Specificator.Execute(SSPI_KEYWORD, false);
            }
        }

        public virtual bool IsIntegratedSecurityAllowed
            => Specificator.AcceptKey(SSPI_KEYWORD);
    }

    internal class DatabaseMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            if (urlInfo.Segments==null || !urlInfo.Segments.Any())
                throw new InvalidConnectionUrlMissingSegmentsException(EXCEPTION_DATABASE_NAME);
            else if (urlInfo.Segments.Length == 1)
                Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.First());
            else
                throw new InvalidConnectionUrlTooManySegmentsException(EXCEPTION_DATABASE_NAME, urlInfo.Segments);
        }
    }
}
