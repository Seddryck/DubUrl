using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    internal class MsExcelDriverLocator : BaseDriverLocator
    {
        private const string STRING_PATTERN = "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)";
        private readonly List<string> Candidates = new();
        internal EncodingOption Encoding { get; }

        public MsExcelDriverLocator()
            : base(STRING_PATTERN) { }
        internal MsExcelDriverLocator(DriverLister driverLister)
            : base(STRING_PATTERN, driverLister) { }

        public override string Locate()
        {
            var allDrivers = Lister.List();
            foreach (var driver in allDrivers)
            {
                if (StringComparer.InvariantCultureIgnoreCase.Compare(RegexPattern, driver)==0)
                    AddCandidate(driver, new[] { driver });
            }
            var bestCandidates = RankCandidates();
            return bestCandidates.Any() ? bestCandidates.First() : string.Empty;
        }

        protected override void AddCandidate(string driver, string[] matches)
            => Candidates.Add(driver);

        protected override List<string> RankCandidates()
            => Candidates;
    }
}