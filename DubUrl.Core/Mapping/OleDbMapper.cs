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
        private const string SERVER_KEYWORD = "Data Source";
        private const string DATABASE_KEYWORD = "Database";
        private const string USERNAME_KEYWORD = "User Id";
        private const string PASSWORD_KEYWORD = "Password";
        private const string PROVIDER_KEYWORD = "Provider";
        private const string SSPI_KEYWORD = "Integrated Security";

        public OleDbMapper(DbConnectionStringBuilder csb) : this(csb, new ProviderLocatorFactory()) { }
        public OleDbMapper(DbConnectionStringBuilder csb, ProviderLocatorFactory providerLocatorFactory) : base(csb,
                  new SpecificatorStraight(csb),
                  new BaseTokenMapper[] {
                    new DataSourceMapper(),
                    new AuthentificationMapper(),
                    new InitialCatalogMapper(),
                    new ProviderMapper(providerLocatorFactory)
                  }
            )
        { }

        internal ProviderLocatorFactory ProviderLocatorFactory
            => (TokenMappers.First(x => x is ProviderMapper) as ProviderMapper)?.ProviderLocatorFactory
                ?? throw new ArgumentNullException();

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
                    var otherScheme = urlInfo.Schemes.SkipWhile(x => x == "odbc").First();
                    var driverLocator = ProviderLocatorFactory.Instantiate(otherScheme);
                    var driver = driverLocator.Locate();
                    urlInfo.Options.Add(PROVIDER_KEYWORD, driver);
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

        internal class ExtendedPropertiesMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                foreach (var option in urlInfo.Options)
                    Specificator.Execute(option.Key, option.Value);
            }
        }
    }
}
