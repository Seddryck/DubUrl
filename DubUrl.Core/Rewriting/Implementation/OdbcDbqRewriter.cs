using DubUrl.Locating.OdbcDriver;
using DubUrl.Parsing;
using DubUrl.Locating.Options;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation;

internal class OdbcDbqRewriter : ConnectionStringRewriter, IOdbcConnectionStringRewriter
{
    protected internal const string SERVER_KEYWORD = "DBQ";
    protected internal const string USERNAME_KEYWORD = "Uid";
    protected internal const string PASSWORD_KEYWORD = "Pwd";
    protected internal const string DRIVER_KEYWORD = "Driver";

    public OdbcDbqRewriter(DbConnectionStringBuilder csb)
        : this(csb, new DriverLocatorFactory()) { }
    public OdbcDbqRewriter(DbConnectionStringBuilder csb, DriverLocatorFactory driverLocatorFactory)
        : base(new StraightSpecificator(csb),
              new BaseTokenMapper[] {
                new DbqMapper(),
                new AuthentificationMapper(),
                new DriverMapper(driverLocatorFactory),
                new OptionsMapper(),
              }
        )
    { }

    internal class DbqMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            var segments = new List<string>();
            if (string.IsNullOrEmpty(urlInfo.Host) && urlInfo.Segments.Length > 1 && string.IsNullOrEmpty(urlInfo.Segments[0]))
                segments = urlInfo.Segments.Skip(1).ToList();
            else
            {
                if (!(StringComparer.InvariantCultureIgnoreCase.Compare(urlInfo.Host, "localhost") == 0 || StringComparer.InvariantCultureIgnoreCase.Compare(urlInfo.Host, ".") == 0))
                    segments.Add(urlInfo.Host);
                segments.AddRange(urlInfo.Segments);
            }

            Specificator.Execute(SERVER_KEYWORD, BuildPath(segments));
        }

        private static string BuildPath(IEnumerable<string> segments)
        {
            if (segments == null || !segments.Any())
                throw new InvalidConnectionUrlMissingSegmentsException("ODBC DBQ");

            var path = new StringBuilder();
            foreach (var segment in segments)
                if (!string.IsNullOrEmpty(segment))
                    path.Append(segment).Append(Path.DirectorySeparatorChar);
            path.Remove(path.Length - 1, 1);
            return path.ToString();
        }
    }

    internal class DriverMapper : BaseTokenMapper
    {
        internal DriverLocatorFactory DriverLocatorFactory { get; } = new DriverLocatorFactory();
        private IList<Type>? AvailableOptions { get; set; }

        public DriverMapper(DriverLocatorFactory driverLocatorFactory)
            => DriverLocatorFactory = driverLocatorFactory;

        public override void Execute(UrlInfo urlInfo)
        {
            if (!urlInfo.Options.ContainsKey(DRIVER_KEYWORD))
            {
                var otherSchemes = urlInfo.Schemes.Where(x => x != "odbc");

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
                                options.Add(remainingOption, value ?? throw new InvalidConnectionUrlException($"Connection Url for ODBC DBQ is specifying an unexpected value 'null' for option '{remainingOption.Name}'"));
                        }
                    }
                    var driverLocator = DriverLocatorFactory.Instantiate(secondScheme, options);
                    Specificator.Execute(DRIVER_KEYWORD, "{" + driverLocator.Locate() + "}");
                }
            }
        }

            protected internal virtual List<Type> InitializeOptions()
            {
                var types = new List<Type>();
                AppDomain.CurrentDomain.GetAssemblies()
#if NET7_0_OR_GREATER
                    .SelectMany(assembly => assembly.GetExportedTypes())
#else
                    .SelectMany(assembly => assembly.GetTypes())
#endif
                    .Where(x => x.IsEnum && x.GetCustomAttributes(typeof(LocatorOptionAttribute), true).Length > 0)
                    .ToList()
                    .ForEach(x => types.Add(x));
                return types;
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
}
