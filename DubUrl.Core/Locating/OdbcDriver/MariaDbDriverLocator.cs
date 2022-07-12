using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    internal class MariaDbDriverLocator : BaseDriverLocator
    {
        private const string REGEX_PATTERN = "^\\bMariaDB ODBC \\b([0-9]*\\.[0-9]*)\\b Driver\\b$";
        private readonly Dictionary<string, decimal> Candidates = new();
        internal EncodingOption Encoding { get; }

        public MariaDbDriverLocator()
            : base(REGEX_PATTERN) { }
        internal MariaDbDriverLocator(DriverLister driverLister)
            : base(REGEX_PATTERN, driverLister) { }

        protected override void AddCandidate(string driver, string[] matches)
        {
            var version = decimal.Parse(matches[0], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
            Candidates.Add(driver, version);
        }

        protected override List<string> RankCandidates()
            => Candidates.OrderByDescending(x => x.Value).Select(x=> x.Key).ToList();
    }
}