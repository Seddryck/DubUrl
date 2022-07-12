using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    internal class MySqlConnectorDriverLocator : BaseDriverLocator
    {
        private const string REGEX_PATTERN = "^\\bMySQL ODBC ([0-9]*\\.[0-9]*)\\s(\\bANSI\\b|\\bUnicode\\b)\\b Driver\\b$";
        private readonly Dictionary<string, decimal> Candidates = new();
        internal EncodingOption Encoding { get; }

        public MySqlConnectorDriverLocator()
            : this(EncodingOption.Unspecified) { }
        public MySqlConnectorDriverLocator(EncodingOption encoding)
            : base(REGEX_PATTERN)  => (Encoding) = (encoding);
        internal MySqlConnectorDriverLocator(DriverLister driverLister, EncodingOption encoding = EncodingOption.Unspecified)
            : base(REGEX_PATTERN, driverLister) => (Encoding) = (encoding);

        protected override void AddCandidate(string driver, string[] matches)
        {
            var version = decimal.Parse(matches[0], System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
            var encoding = (EncodingOption)Enum.Parse(typeof(EncodingOption), matches[1]);

            if (Encoding != EncodingOption.Unspecified && encoding != Encoding)
                return;

            Candidates.Add(driver, version);
        }

        protected override List<string> RankCandidates()
            => Candidates.OrderByDescending(x => x.Value).Select(x=> x.Key).ToList();
    }
}