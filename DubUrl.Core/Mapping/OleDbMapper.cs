using DubUrl.Locating.OleDbProvider;
using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    internal class OleDbMapper : BaseMapper
    {
        protected internal const string PROVIDER_KEYWORD = "Provider";
        protected internal const string SERVER_KEYWORD = "Data Source";
        protected internal const string DATABASE_KEYWORD = "Database";
        protected internal const string USERNAME_KEYWORD = "User Id";
        protected internal const string PASSWORD_KEYWORD = "Password";
        protected internal const string SSPI_KEYWORD = "Integrated Security";

        public OleDbMapper(DbConnectionStringBuilder csb) : this(csb, new ProviderLocatorFactory()) { }
        public OleDbMapper(DbConnectionStringBuilder csb, ProviderLocatorFactory providerLocatorFactory) : base(csb,
                  new SpecificatorStraight(csb),
                  new BaseTokenMapper[] {
                    new DataSourceMapper(),
                    new AuthentificationMapper(),
                    new InitialCatalogMapper(),
                    new ProviderMapper(providerLocatorFactory),
                    new SelectMapper(providerLocatorFactory)
                  }
            )
        { }

        internal class DataSourceMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
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

            internal override void Execute(UrlInfo urlInfo)
            {

                if (!urlInfo.Options.ContainsKey(PROVIDER_KEYWORD))
                {
                    var otherScheme = urlInfo.Schemes.SkipWhile(x => x == "oledb").First();
                    var providerLocator = ProviderLocatorFactory.Instantiate(otherScheme);
                    var provider = providerLocator.Locate();
                    urlInfo.Options.Add(PROVIDER_KEYWORD, provider);
                }
            }
        }

        internal class AuthentificationMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (!string.IsNullOrEmpty(urlInfo.Username))
                    Specificator.Execute(USERNAME_KEYWORD, urlInfo.Username);
                if (!string.IsNullOrEmpty(urlInfo.Password))
                    Specificator.Execute(PASSWORD_KEYWORD, urlInfo.Password);

                if (string.IsNullOrEmpty(urlInfo.Username) && string.IsNullOrEmpty(urlInfo.Password))
                    Specificator.Execute(SSPI_KEYWORD, "SSPI");
            }
        }

        internal class InitialCatalogMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length == 1)
                    Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.First());
                else
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal class SelectMapper : BaseTokenMapper
        {
            internal ProviderLocatorFactory ProviderLocatorFactory { get; } = new ProviderLocatorFactory();

            public SelectMapper(ProviderLocatorFactory ProviderLocatorFactory)
                => this.ProviderLocatorFactory = ProviderLocatorFactory;

            internal override void Execute(UrlInfo urlInfo)
            {
                var otherScheme = urlInfo.Schemes.SkipWhile(x => x == "oledb").FirstOrDefault();
                var optionsMapper = (string.IsNullOrEmpty(otherScheme)) ? new OptionsMapper() : ProviderLocatorFactory.Instantiate(otherScheme).OptionsMapper;
                optionsMapper.Accept(Specificator);
                optionsMapper.Execute(urlInfo);
            }
        }
    }
}
