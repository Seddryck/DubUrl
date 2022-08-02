using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    [Driver(
        "MariaDB"
        , new[] { "maria", "mariadb" }
        , "^\\bMariaDB ODBC \\b([0-9]*\\.[0-9]*)\\b Driver\\b$"
        , new[] { typeof(VersionOption) }
        , 0
    )]
    internal class MariaDbDriverLocator : BaseDriverLocator
    {
        
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
            => Candidates.OrderByDescending(x => x.Value).Select(x=> x.Key).ToList();
    }
}