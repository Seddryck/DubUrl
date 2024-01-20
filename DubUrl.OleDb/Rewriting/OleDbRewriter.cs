using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Rewriting.Tokening;
using DubUrl.Rewriting;

namespace DubUrl.OleDb.Mapping;

public class OleDbRewriter : ConnectionStringRewriter, IOleDbConnectionStringRewriter
{
    protected internal const string PROVIDER_KEYWORD = "Provider";
    protected internal const string SERVER_KEYWORD = "Data Source";
    protected internal const string DATABASE_KEYWORD = "Database";
    protected internal const string USERNAME_KEYWORD = "User Id";
    protected internal const string PASSWORD_KEYWORD = "Password";
    protected internal const string SSPI_KEYWORD = "Integrated Security";

    public OleDbRewriter(DbConnectionStringBuilder csb) 
        : this(csb, new ProviderLocatorFactory()) { }
    public OleDbRewriter(DbConnectionStringBuilder csb, ProviderLocatorFactory providerLocatorFactory)
        : base(
              new StraightSpecificator(csb),
              [
                new AuthentificationMapper(),
                new ProviderMapper(providerLocatorFactory),
                new AdditionalMappers(providerLocatorFactory)
              ]
        )
    { }

    internal class DataSourceMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            var segments = new List<string>();
            if (string.IsNullOrEmpty(urlInfo.Host) && urlInfo.Segments.Length > 1 && string.IsNullOrEmpty(urlInfo.Segments[0]))
                segments = urlInfo.Segments.Skip(1).ToList();
            else
            {
                if (!(
                        string.IsNullOrEmpty(urlInfo.Host)
                        || StringComparer.InvariantCultureIgnoreCase.Compare(urlInfo.Host, "localhost") == 0 
                        || StringComparer.InvariantCultureIgnoreCase.Compare(urlInfo.Host, ".") == 0)
                    )
                    segments.Add(urlInfo.Host);
                segments.AddRange(urlInfo.Segments);
            }

            Specificator.Execute(SERVER_KEYWORD, BuildPath(segments));
        }

        private static string BuildPath(IEnumerable<string> segments)
        {
            if (segments == null || !segments.Any())
                throw new ArgumentException("A minimum of one segment is expected.", nameof(segments));

            var path = new StringBuilder();
            foreach (var segment in segments)
                if (!string.IsNullOrEmpty(segment))
                    path.Append(segment).Append(Path.DirectorySeparatorChar);
            path.Remove(path.Length - 1, 1);
            return path.ToString();
        }
    }

    internal class ServerMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            var fullHost = new StringBuilder();
            fullHost.Append(urlInfo.Host);
            if (urlInfo.Segments.Length == 2)
                fullHost.Append('\\').Append(urlInfo.Segments.First());
            if (urlInfo.Port != 0)
                fullHost.Append(',').Append(urlInfo.Port);

            Specificator.Execute(SERVER_KEYWORD, fullHost.ToString());
        }
    }

    internal class ProviderMapper : BaseTokenMapper
    {
        internal ProviderLocatorFactory ProviderLocatorFactory { get; } = new ProviderLocatorFactory();

        public ProviderMapper(ProviderLocatorFactory ProviderLocatorFactory)
            => this.ProviderLocatorFactory = ProviderLocatorFactory;

        public override void Execute(UrlInfo urlInfo)
        {

            if (!urlInfo.Options.ContainsKey(PROVIDER_KEYWORD))
            {
                var otherScheme = urlInfo.Schemes.SkipWhile(x => x == "oledb").First();
                var providerLocator = ProviderLocatorFactory.Instantiate(otherScheme);
                var provider = providerLocator.Locate();
                Specificator.Execute(PROVIDER_KEYWORD, provider);
            }
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
        }
    }

    internal class InitialCatalogMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            if (urlInfo.Segments.Length <= 2)
                Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.Last());
            else
                throw new ArgumentOutOfRangeException(nameof(urlInfo));
        }
    }

    internal class AdditionalMappers : BaseTokenMapper
    {
        internal ProviderLocatorFactory ProviderLocatorFactory { get; } = new ProviderLocatorFactory();

        public AdditionalMappers(ProviderLocatorFactory ProviderLocatorFactory)
            => this.ProviderLocatorFactory = ProviderLocatorFactory;

        public override void Execute(UrlInfo urlInfo)
        {
            var otherScheme = urlInfo.Schemes.SkipWhile(x => x == "oledb").FirstOrDefault();
            var additionalMappers = string.IsNullOrEmpty(otherScheme) ? new[] { new OptionsMapper() } : ProviderLocatorFactory.Instantiate(otherScheme).AdditionalMappers;
            foreach (var mapper in additionalMappers)
            {
                mapper.Accept(Specificator);
                mapper.Execute(urlInfo);
            }
        }
    }
}
