using DubUrl.Locating.OdbcDriver;
using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    internal class OdbcMapper : BaseMapper
    {
        private const string SERVER_KEYWORD = "Server";
        private const string PORT_KEYWORD = "Port";
        private const string DATABASE_KEYWORD = "Database";
        private const string USERNAME_KEYWORD = "Uid";
        private const string PASSWORD_KEYWORD = "Pwd";
        private const string DRIVER_KEYWORD = "Driver";



        public OdbcMapper(DbConnectionStringBuilder csb) : this(csb, new DriverLocatorFactory()) { }
        public OdbcMapper(DbConnectionStringBuilder csb, DriverLocatorFactory driverLocatorFactory) : base(csb,
                  new SpecificatorStraight(csb),
                  new BaseTokenMapper[] {
                    new HostMapper(),
                    new AuthentificationMapper(),
                    new DatabaseMapper(),
                    new DriverMapper(driverLocatorFactory),
                    new OptionsMapper(),
                  }
            )
        { }

        internal DriverLocatorFactory DriverLocatorFactory
            => (TokenMappers.First(x => x is DriverMapper) as DriverMapper)?.DriverLocatorFactory 
                ?? throw new ArgumentNullException();

        internal class HostMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                Specificator.Execute(SERVER_KEYWORD, urlInfo.Host);
                if (urlInfo.Port > 0)
                    Specificator.Execute(PORT_KEYWORD, urlInfo.Port);
            }
        }

        internal class DriverMapper : BaseTokenMapper
        {
            internal DriverLocatorFactory DriverLocatorFactory { get; } = new DriverLocatorFactory();
            private IList<Type>? AvailableOptions { get; set; }

            public DriverMapper(DriverLocatorFactory driverLocatorFactory)
                => DriverLocatorFactory = driverLocatorFactory;

            internal override void Execute(UrlInfo urlInfo)
            {

                if (!urlInfo.Options.ContainsKey(DRIVER_KEYWORD))
                {
                    if (AvailableOptions == null)
                        AvailableOptions = InitializeOptions();
                    var options = new Dictionary<Type, object>();
                    urlInfo.Options.Where(x => x.Key.StartsWith(DRIVER_KEYWORD + "-")).ToList()
                        .ForEach(x => options.Add(
                            OptionMatch(AvailableOptions, x)
                            , Enum.TryParse(OptionMatch(AvailableOptions, x), x.Value, out var e)
                                ? e ?? throw new ArgumentNullException()
                                : throw new ArgumentOutOfRangeException()
                    ));

                    var otherScheme = urlInfo.Schemes.SkipWhile(x => x == "odbc").First();
                    var driverLocator = DriverLocatorFactory.Instantiate(otherScheme, options);
                    var driver = driverLocator.Locate();
                    urlInfo.Options.Add(DRIVER_KEYWORD, driver);
                }

                Type OptionMatch(IEnumerable<Type> options, KeyValuePair<string, string> keyValue)
                    => options.FirstOrDefault(t => t.Name[..^6] == keyValue.Key[7..]) ?? throw new ArgumentOutOfRangeException();
            }

            protected internal virtual List<Type> InitializeOptions()
            {
                var types = new List<Type>();
                AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(assembly => assembly.GetTypes())
                    .Where(x => x.IsEnum && x.GetCustomAttributes(typeof(DriverLocatorOptionAttribute), true).Length > 0)
                    .ToList()
                    .ForEach(x => types.Add(x));
                return types;
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
            }
        }

        internal class DatabaseMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                if (urlInfo.Segments.Length == 1)
                    Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.First());
                else
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal new class OptionsMapper : BaseTokenMapper
        {
            internal override void Execute(UrlInfo urlInfo)
            {
                foreach (var option in urlInfo.Options)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(option.Key, DRIVER_KEYWORD))
                    {
                        if (!option.Value.StartsWith("{") || !option.Value.EndsWith("}"))
                            if (option.Value.StartsWith("{") ^ option.Value.EndsWith("}"))
                                throw new ArgumentOutOfRangeException($"The value of the option '{DRIVER_KEYWORD}' must start with a '{{' and end with '}}' or both should be missing. The value was '{option.Value}'");
                            else
                                Specificator.Execute(option.Key, $"{{{option.Value}}}");
                        else
                            Specificator.Execute(option.Key, $"{option.Value}");
                    }
                    else
                        Specificator.Execute(option.Key, option.Value);
                }
            }
        }
    }
}
