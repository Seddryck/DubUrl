using DubUrl.Locating.RegexUtils;
using DubUrl.Mapping.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver.Implementation
{
    [Driver<MariaDbDriverRegex, MariaDbConnectorMapper>(
        new[] { typeof(VersionOption) }
    )]
    internal class MariaDbDriverLocator : BaseDriverLocator
    {
        internal class MariaDbDriverRegex : CompositeRegex, IRegexDriver
        {
            public MariaDbDriverRegex()
                : base(new BaseRegex[]
                {
                    new WordMatch("MariaDB ODBC"),
                    new SpaceMatch(),
                    new VersionCapture(),
                    new SpaceMatch(),
                    new WordMatch("Driver"),
                })
            { }
        }

        private readonly Dictionary<string, decimal> Candidates = new();
        internal EncodingOption Encoding { get; }

        public MariaDbDriverLocator()
            : base(GetNamePattern<MariaDbDriverLocator>()) { }
        internal MariaDbDriverLocator(DriverLister driverLister)
            : base(GetNamePattern<MariaDbDriverLocator>(), driverLister) { }

        protected override void AddCandidate(string driver, string[] matches)
        {
            var version = decimal.Parse
            (
                matches[GetOptionPosition<MariaDbDriverLocator>(typeof(VersionOption))]
                , System.Globalization.NumberStyles.AllowDecimalPoint
                , System.Globalization.CultureInfo.InvariantCulture.NumberFormat
            );
            Candidates.Add(driver, version);
        }

        protected override List<string> RankCandidates()
            => Candidates.OrderByDescending(x => x.Value).Select(x => x.Key).ToList();

        
    }
}