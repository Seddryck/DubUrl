using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.DriverLocating
{
    internal class PostgresqlDriverLocator : BaseDriverLocator
    {
        private const string REGEX_PATTERN = "^\\bPostgreSQL \\b(\\bANSI\\b|\\bUnicode\\b)\\(?(\\bx64\\b)?\\)?$";
        private readonly List<string> Candidates = new();
        private Encoding Encoding { get; }
        private Architecture Architecture { get; }

        public PostgresqlDriverLocator(Encoding encoding = Encoding.Unspecified, Architecture architecture = Architecture.Unspecified)
           : base(REGEX_PATTERN) => (Encoding, Architecture) = (encoding, architecture);
        internal PostgresqlDriverLocator(DriverLister driverLister, Encoding encoding = Encoding.Unspecified, Architecture architecture = Architecture.Unspecified)
            : base(REGEX_PATTERN, driverLister) => (Encoding, Architecture) = (encoding, architecture);

        protected override void AddCandidate(string driver, MatchCollection matches)
        {
            var encoding = (Encoding)Enum.Parse(typeof(Encoding), matches[0].Groups[1].Value);
            var architecture = (Architecture)Enum.Parse(typeof(Architecture), matches[0].Groups.Count > 1 && !string.IsNullOrEmpty(matches[0].Groups[2].Value) ? matches[0].Groups[2].Value : "x86");

            if (Encoding != Encoding.Unspecified && encoding != Encoding)
                return;
            if (Architecture != Architecture.Unspecified && architecture != Architecture)
                return;

            Candidates.Add(driver);
        }

        protected override List<string> RankCandidates()
            => Candidates.ToList();
    }
}
