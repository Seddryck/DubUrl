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

public class OdbcRewriter : ConnectionStringRewriter, IOdbcConnectionStringRewriter, DubUrl.Conversion.IReverseConnectionMapper
{
    public virtual ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new DubUrl.Conversion.ConnectionStringParts(source);
        p.TakeHost("Server");
        p.Segments.Add(p.Take("Database"));
        p.TakeCredentials("Uid", "Pwd");
        return p.ToUri(scheme);
    }

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
        => (this.TokenMappers.Single(x => x is DriverMapper) as DriverMapper)?.DriverLocatorFactory
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
            if (urlInfo.Options.ContainsKey(DRIVER_KEYWORD))
                return;

            var schemes = urlInfo.Scheme.Parts.Where(x => !x.Equals("odbc", StringComparison.OrdinalIgnoreCase)).ToArray();
            var database = GetDatabaseScheme(schemes);
            var remaining = schemes.Where(s => s != database).ToArray();
            var namedDriver = remaining.FirstOrDefault(IsDriverName);
            if (namedDriver is not null)
            {
                if (remaining.Length > 1)
                    throw new UnexpectedSchemesWithDriverNameException(schemes.Where(s => !IsDriverName(s)).ToArray(), namedDriver);
                Specificator.Execute(DRIVER_KEYWORD, namedDriver);
                return;
            }

            var locator = remaining.Length == 0
                ? DriverLocatorFactory.Instantiate(database)
                : DriverLocatorFactory.Instantiate(database, ParseOptions(remaining));
            Specificator.Execute(DRIVER_KEYWORD, "{" + locator.Locate() + "}");
        }

        private string GetDatabaseScheme(string[] schemes)
        {
            var aliases = DriverLocatorFactory.GetValidAliases();
            var matches = schemes.Where(aliases.Contains).ToArray();
            return matches.Length switch
            {
                0 => throw new SchemeNotFoundException(schemes, aliases),
                1 => matches[0],
                _ => throw new MultipleSchemeFoundException(matches),
            };
        }

        private static bool IsDriverName(string scheme) => scheme.StartsWith('{') && scheme.EndsWith('}');

        private Dictionary<Type, object> ParseOptions(IEnumerable<string> schemes)
        {
            AvailableOptions ??= InitializeOptions();
            var options = new Dictionary<Type, object>();
            foreach (var scheme in schemes)
            {
                foreach (var optionType in AvailableOptions.Where(type => !options.ContainsKey(type)))
                {
                    if (Enum.TryParse(optionType, scheme, out var value))
                        options.Add(optionType, value);
                }
            }
            return options;
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
