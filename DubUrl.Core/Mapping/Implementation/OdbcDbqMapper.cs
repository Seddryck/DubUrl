﻿using DubUrl.Mapping.Connectivity;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping.Tokening;

namespace DubUrl.Mapping.Implementation
{
    [GenericMapper<OdbcConnectivity>(
        "System.Data.Odbc"
    )]
    internal class OdbcDbqMapper : BaseMapper, IOdbcMapper
    {
        protected internal const string SERVER_KEYWORD = "DBQ";
        protected internal const string USERNAME_KEYWORD = "Uid";
        protected internal const string PASSWORD_KEYWORD = "Pwd";
        protected internal const string DRIVER_KEYWORD = "Driver";

        public OdbcDbqMapper(DbConnectionStringBuilder csb, IDialect dialect) : this(csb, dialect, new DriverLocatorFactory()) { }
        public OdbcDbqMapper(DbConnectionStringBuilder csb, IDialect dialect, DriverLocatorFactory driverLocatorFactory)
            : base(csb,
                  dialect,
                  new SpecificatorStraight(csb),
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
            internal override void Execute(UrlInfo urlInfo)
            {
                var fullHost = new StringBuilder(urlInfo.Host);

                foreach (var segment in urlInfo.Segments)
                    fullHost.Append('\\').Append(segment);

                Specificator.Execute(SERVER_KEYWORD, fullHost.ToString());
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
                        if (AvailableOptions == null)
                            AvailableOptions = InitializeOptions();
                        var options = new Dictionary<Type, object>();
                        foreach (var scheme in remainingSchemes)
                        {
                            var remainingOptions = AvailableOptions.Where(x => !options.Keys.ToArray().Any(z => z == x));
                            foreach (var remainingOption in remainingOptions)
                            {
                                if (Enum.TryParse(remainingOption, scheme, out var value))
                                    options.Add(remainingOption, value ?? throw new ArgumentNullException());
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
    }
}