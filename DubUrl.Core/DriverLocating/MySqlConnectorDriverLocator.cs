using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.DriverLocating
{
    internal class MySqlConnectorDriverLocator : BaseDriverLocator
    {
        private const string REGEX_PATTERN = "^\\bMySQL ODBC ([0-9]*\\.[0-9]*)\\s(\\bANSI\\b|\\bUnicode\\b)\\b Driver\\b$";
        private readonly Dictionary<string, decimal> Candidates = new();
        internal Encoding Encoding { get; }

        public MySqlConnectorDriverLocator()
            : this(Encoding.Unspecified) { }
        public MySqlConnectorDriverLocator(Encoding encoding)
            : base(REGEX_PATTERN)  => (Encoding) = (encoding);
        internal MySqlConnectorDriverLocator(DriverLister driverLister, Encoding encoding = Encoding.Unspecified)
            : base(REGEX_PATTERN, driverLister) => (Encoding) = (encoding);

        protected override void AddCandidate(string driver, MatchCollection matches)
        {
            var version = decimal.Parse(matches[0].Groups[1].Value, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
            var encoding = (Encoding)Enum.Parse(typeof(Encoding), matches[0].Groups[2].Value);

            if (Encoding != Encoding.Unspecified && encoding != Encoding)
                return;

            Candidates.Add(driver, version);
        }

        protected override List<string> RankCandidates()
            => Candidates.OrderByDescending(x => x.Value).Select(x=> x.Key).ToList();
    }
}