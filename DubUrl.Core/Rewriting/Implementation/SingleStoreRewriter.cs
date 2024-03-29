﻿using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation;

internal class SingleStoreRewriter : ConnectionStringRewriter
{
    private const string EXCEPTION_DATABASE_NAME = "SingleStore";
    protected internal const string SERVER_KEYWORD = "Server";
    protected internal const string PORT_KEYWORD = "Port";
    protected internal const string DATABASE_KEYWORD = "Database";
    protected internal const string USERNAME_KEYWORD = "User ID";
    protected internal const string PASSWORD_KEYWORD = "Password";

    public SingleStoreRewriter(DbConnectionStringBuilder csb)
        : base(new UniqueAssignmentSpecificator(csb),
              [
                new HostMapper(),
                new AuthentificationMapper(),
                new DatabaseMapper(),
                new OptionsMapper(),
              ]
        )
    { }

    protected SingleStoreRewriter(Specificator specificator, BaseTokenMapper[] tokenMappers)
        : base(specificator, tokenMappers) { }

    internal class HostMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            Specificator.Execute(SERVER_KEYWORD, urlInfo.Host);
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

            if (string.IsNullOrEmpty(urlInfo.Username))
                throw new UsernameNotFoundException();
        }
    }

    internal class DatabaseMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            if (urlInfo.Segments==null || !urlInfo.Segments.Any())
                throw new InvalidConnectionUrlMissingSegmentsException(EXCEPTION_DATABASE_NAME);
            else if (urlInfo.Segments.Length == 1)
                Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.First());
            else if (urlInfo.Segments.Length > 1)
                throw new InvalidConnectionUrlTooManySegmentsException(EXCEPTION_DATABASE_NAME, urlInfo.Segments);
        }
    }
}
