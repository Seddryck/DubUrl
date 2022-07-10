using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DubUrl.Locating.OdbcDriver
{
    internal class MssqlDriverLocator : BaseDriverLocator
    {
        private const string REGEX_PATTERN = "^\\bODBC Driver \\b([0-9]*)\\b for SQL Server\\b$";
        private readonly Dictionary<string, int> Candidates = new();
        public MssqlDriverLocator()
            : base(REGEX_PATTERN) { }
        internal MssqlDriverLocator(DriverLister driverLister)
            : base(REGEX_PATTERN, driverLister) { }

        protected override void AddCandidate(string driver, MatchCollection matches)
            => Candidates.Add(driver, int.Parse(matches[0].Groups[1].Value));

        protected override List<string> RankCandidates()
            => Candidates.OrderByDescending(x => x.Value).Select(x=> x.Key).ToList();
    }
}