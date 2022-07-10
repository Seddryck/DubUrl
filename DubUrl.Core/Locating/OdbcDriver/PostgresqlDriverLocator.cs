using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    internal class PostgresqlDriverLocator : BaseDriverLocator
    {
        private const string REGEX_PATTERN = "^\\bPostgreSQL \\b(\\bANSI\\b|\\bUnicode\\b)\\(?(\\bx64\\b)?\\)?$";
        private readonly List<string> Candidates = new();
        internal EncodingOption Encoding { get; }
        internal ArchitectureOption Architecture { get; }

        public PostgresqlDriverLocator()
            : this(EncodingOption.Unspecified, ArchitectureOption.Unspecified) { }
        public PostgresqlDriverLocator(EncodingOption encoding, ArchitectureOption architecture)
            : base(REGEX_PATTERN) => (Encoding, Architecture) = (encoding, architecture);
        internal PostgresqlDriverLocator(DriverLister driverLister, EncodingOption encoding = EncodingOption.Unspecified, ArchitectureOption architecture = ArchitectureOption.Unspecified)
            : base(REGEX_PATTERN, driverLister) => (Encoding, Architecture) = (encoding, architecture);

        protected override void AddCandidate(string driver, string[] matches)
        {
            var encoding = (EncodingOption)Enum.Parse(typeof(EncodingOption), matches[0]);
            var architecture = (ArchitectureOption)Enum.Parse(typeof(ArchitectureOption), matches.Length > 1 && !string.IsNullOrEmpty(matches[1]) ? matches[1] : "x86");

            if (Encoding != EncodingOption.Unspecified && encoding != Encoding)
                return;
            if (Architecture != ArchitectureOption.Unspecified && architecture != Architecture)
                return;

            Candidates.Add(driver);
        }

        protected override List<string> RankCandidates()
            => Candidates.ToList();
    }
}
