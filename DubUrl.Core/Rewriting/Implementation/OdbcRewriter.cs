using DubUrl.Locating.OdbcDriver;
using DubUrl.Locating.Options;
using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation;

public class OdbcRewriter : ConnectionStringRewriter, IOdbcConnectionStringRewriter
{
    private const string EXCEPTION_DATABASE_NAME = "ODBC";
    protected internal const string SERVER_KEYWORD = "Server";
    protected internal const string DATABASE_KEYWORD = "Database";
    protected internal const string USERNAME_KEYWORD = "Uid";
    protected internal const string PASSWORD_KEYWORD = "Pwd";
    protected internal const string DRIVER_KEYWORD = "Driver";

    public OdbcRewriter(DbConnectionStringBuilder csb)
        : this (csb, new BuiltInDriverLocatorFactory()) { }
    public OdbcRewriter(DbConnectionStringBuilder csb, DriverLocatorFactory driverLocatorFactory)
        : this(csb, driverLocatorFactory, DefaultLocatorOptions) { }
    public OdbcRewriter(DbConnectionStringBuilder csb, DriverLocatorFactory driverLocatorFactory, IEnumerable<Type> locatorOptions)
        : base(new StraightSpecificator(csb),
              [
                new HostMapper(),
                new AuthentificationMapper(),
                new DatabaseMapper(),
                new DriverMapper(driverLocatorFactory, locatorOptions),
                new OptionsMapper(),
              ]
        )
    { }

    internal static Type[] DefaultLocatorOptions { get; } =
        [typeof(ArchitectureOption), typeof(EncodingOption), typeof(VersionOption)];

    protected OdbcRewriter(DbConnectionStringBuilder csb, BaseTokenMapper[] tokenMappers)
        : base(new StraightSpecificator(csb), tokenMappers) { }

    internal DriverLocatorFactory DriverLocatorFactory
        => (TokenMappers.Single(x => x is DriverMapper) as DriverMapper)?.DriverLocatorFactory
            ?? throw new NullReferenceException();

    protected internal class HostMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            var fullHost = new StringBuilder(urlInfo.Host);

            if (urlInfo.Segments.Count > 1)
                fullHost.Append('\\').Append(urlInfo.Segments.First());

            if (urlInfo.Port > 0)
                fullHost.Append(", ").Append(urlInfo.Port);

            Specificator.Execute(SERVER_KEYWORD, fullHost.ToString());
        }
    }

    protected internal class DriverMapper : BaseTokenMapper
    {
        internal DriverLocatorFactory DriverLocatorFactory { get; } = new BuiltInDriverLocatorFactory();
        private IReadOnlyList<Type> ConfiguredOptions { get; }
        private IList<Type>? AvailableOptions { get; set; }

        public DriverMapper(DriverLocatorFactory driverLocatorFactory)
            : this(driverLocatorFactory, DefaultLocatorOptions) { }

        public DriverMapper(DriverLocatorFactory driverLocatorFactory, IEnumerable<Type> locatorOptions)
        {
            DriverLocatorFactory = driverLocatorFactory;
            ConfiguredOptions = locatorOptions?.Distinct().ToArray()
                ?? throw new ArgumentNullException(nameof(locatorOptions));
            if (ConfiguredOptions.Any(type => !type.IsEnum))
                throw new ArgumentException("ODBC locator option types must be enums.", nameof(locatorOptions));
        }

        public override void Execute(ConnectionUri urlInfo)
        {
            if (!urlInfo.Options.ContainsKey(DRIVER_KEYWORD))
            {
                var otherSchemes = urlInfo.Scheme.Parts.Where(x => !x.Equals("odbc", StringComparison.OrdinalIgnoreCase));

                var secondScheme = string.Empty;
                switch (otherSchemes.Count(s => DriverLocatorFactory.GetValidAliases().Any(v => v == s)))
                {
                    case 0: throw new SchemeNotFoundException(otherSchemes.ToArray(), DriverLocatorFactory.GetValidAliases());
                    case 1: secondScheme = otherSchemes.First(s => DriverLocatorFactory.GetValidAliases().Any(v => v == s)); break;
                    case > 1: throw new MultipleSchemeFoundException(otherSchemes.Where(s => DriverLocatorFactory.GetValidAliases().Any(v => v == s)).ToArray());
                }
                var remainingSchemes = otherSchemes.Where(x => x != secondScheme);

                //Case of the full driver name is specified
                if (!remainingSchemes.Any())
                {
                    var driverLocator = DriverLocatorFactory.Instantiate(secondScheme);
                    Specificator.Execute(DRIVER_KEYWORD, "{" + driverLocator.Locate() + "}");
                }
                else if (remainingSchemes.Any(s => s.StartsWith("{") && s.EndsWith("}")))
                {
                    if (remainingSchemes.Count() > 1)
                        throw new UnexpectedSchemesWithDriverNameException(
                            otherSchemes.Where(s => !s.StartsWith("{") || !s.EndsWith("}")).ToArray()
                            , otherSchemes.First(s => s.StartsWith("{") && s.EndsWith("}"))
                        );

                    Specificator.Execute(DRIVER_KEYWORD, otherSchemes.First(s => s.StartsWith("{") && s.EndsWith("}")));
                }
                else
                {
                    AvailableOptions ??= InitializeOptions();
                    var options = new Dictionary<Type, object>();
                    foreach (var scheme in remainingSchemes)
                    {
                        var remainingOptions = AvailableOptions.Where(x => !options.Keys.ToArray().Any(z => z == x));
                        foreach (var remainingOption in remainingOptions)
                        {
                            if (Enum.TryParse(remainingOption, scheme, out var value))
                                options.Add(remainingOption, value ?? throw new InvalidConnectionUrlException($"Connection Url for ODBC is specifying an unexpected value 'null' for option '{remainingOption.Name}'"));
                        }
                    }
                    var driverLocator = DriverLocatorFactory.Instantiate(secondScheme, options);
                    Specificator.Execute(DRIVER_KEYWORD, "{" + driverLocator.Locate() + "}");
                }
            }
        }

        protected internal virtual List<Type> InitializeOptions() => [.. ConfiguredOptions];
        }

    protected internal class AuthentificationMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            if (!string.IsNullOrEmpty(urlInfo.Username))
                Specificator.Execute(USERNAME_KEYWORD, urlInfo.Username);
            if (!string.IsNullOrEmpty(urlInfo.Password))
                Specificator.Execute(PASSWORD_KEYWORD, urlInfo.Password);
        }
    }

    protected internal class DatabaseMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            if (urlInfo.Segments.Count <= 2)
                Specificator.Execute(DATABASE_KEYWORD, urlInfo.Segments.Last());
            else
                throw new InvalidConnectionUrlTooManySegmentsException(EXCEPTION_DATABASE_NAME, urlInfo.Segments);
        }
    }

    protected internal class OptionsMapper : BaseTokenMapper
    {
        public override void Execute(ConnectionUri urlInfo)
        {
            foreach (var option in urlInfo.Options)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(option.Key, DRIVER_KEYWORD))
                {
                    if (!option.Value.StartsWith("{") || !option.Value.EndsWith("}"))
                        if (option.Value.StartsWith("{") ^ option.Value.EndsWith("}"))
                            throw new InvalidConnectionUrlException($"The value of the option '{DRIVER_KEYWORD}' must start with a '{{' and end with '}}' or both should be missing. The value was '{option.Value}'");
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
